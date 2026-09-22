using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Adjudge.Providers;

namespace Adjudge;

/// <summary>Turns definitions into callable decisions, and runs each evaluation against the provider. One engine is meant to be shared, and it is thread-safe.</summary>
public sealed class DecisionEngine
{
    private const string ReflectionMessage =
        "Serialising the context uses reflection. Use the constructor that takes a SerialiseContext, built with ContextSerializer.From.";

    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private readonly IDecisionProvider _provider;
    private readonly SerialiseContext _contextSerializer;
    private readonly bool _enableTelemetry;

    /// <summary>Creates an engine that serialises contexts by reflection, which is the convenient form for applications that are not trimmed.</summary>
    /// <param name="provider">The provider every evaluation goes to.</param>
    /// <param name="options">Tuning. Defaults are used when this is null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
    [RequiresUnreferencedCode(ReflectionMessage)]
    [RequiresDynamicCode(ReflectionMessage)]
    public DecisionEngine(IDecisionProvider provider, DecisionEngineOptions? options = null)
        : this(provider, options ??= new DecisionEngineOptions(), SerialiseByReflection(options))
    {
    }

    /// <summary>Creates an engine that serialises contexts through the supplied function, which keeps the whole path trimming and ahead-of-time safe.</summary>
    /// <param name="provider">The provider every evaluation goes to.</param>
    /// <param name="options">Tuning.</param>
    /// <param name="contextSerializer">Turns a context into JSON, typically through a source-generated serialiser context.</param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public DecisionEngine(
        IDecisionProvider provider,
        DecisionEngineOptions options,
        SerialiseContext contextSerializer)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(contextSerializer);

        _provider = provider;
        _contextSerializer = contextSerializer;
        _enableTelemetry = options.EnableTelemetry;
    }

    /// <summary>Validates the definition and returns a callable decision. Definition errors surface here rather than on the first call.</summary>
    /// <typeparam name="TDecision">The definition to instantiate.</typeparam>
    /// <typeparam name="TContext">The facts the decision takes.</typeparam>
    /// <typeparam name="TResult">The typed answers the decision produces.</typeparam>
    /// <exception cref="DecisionDefinitionException">The definition is invalid, or the provider cannot serve a question kind it uses.</exception>
    public IDecision<TContext, TResult> Create<TDecision, TContext, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TResult>()
        where TDecision : Decision<TContext, TResult>, new() =>
        Create(new TDecision());

    /// <summary>Validates an already-built definition and returns a callable decision.</summary>
    /// <typeparam name="TContext">The facts the decision takes.</typeparam>
    /// <typeparam name="TResult">The typed answers the decision produces.</typeparam>
    /// <exception cref="DecisionDefinitionException">The definition is invalid, or the provider cannot serve a question kind it uses.</exception>
    public IDecision<TContext, TResult> Create<TContext, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TResult>(
        Decision<TContext, TResult> decision)
    {
        var definition = DecisionDefinition<TContext, TResult>.Build(decision);

        var missing = definition.RequiredCapabilities & ~_provider.Capabilities;
        if (missing != DecisionCapabilities.None)
        {
            throw new DecisionDefinitionException(
                $"Provider '{_provider.Name}' does not support {missing}, which decision '{definition.Id}' requires.");
        }

        return new EngineDecision<TContext, TResult>(this, definition);
    }

    internal async Task<DecisionResult<TResult>> EvaluateAsync<TContext, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TResult>(
        DecisionDefinition<TContext, TResult> definition,
        TContext context,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ct.ThrowIfCancellationRequested();

        var json = _contextSerializer(context);
        var request = new ProviderRequest(DecisionContext.FromJson(context, json), definition.Questions, definition.Id);

        using var activity = _enableTelemetry
            ? AdjudgeTelemetry.Activities.StartActivity("adjudge.decide", ActivityKind.Client)
            : null;
        activity?.SetTag("definition.id", definition.Id);
        activity?.SetTag("provider", _provider.Name);

        var tags = new TagList
        {
            { "definition.id", definition.Id },
            { "provider", _provider.Name },
        };

        ProviderResponse response;
        try
        {
            response = await _provider.DecideAsync(request, ct).ConfigureAwait(false)
                ?? throw new ProviderResponseException($"Provider '{_provider.Name}' returned no response.", _provider.Name);
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            Count(tags, "error");
            throw;
        }

        if (_enableTelemetry)
        {
            activity?.SetTag("model", response.Model);

            if (response.Usage?.InputTokens is { } input)
            {
                AdjudgeTelemetry.InputTokens.Add(input, tags);
            }

            if (response.Usage?.OutputTokens is { } output)
            {
                AdjudgeTelemetry.OutputTokens.Add(output, tags);
            }
        }

        Count(tags, "ok");

        var answers = new object[definition.Bindings.Count];
        for (var index = 0; index < definition.Bindings.Count; index++)
        {
            var binding = definition.Bindings[index];
            if (response.Answers is null || !response.Answers.TryGetValue(binding.Name, out var answer) || answer is null)
            {
                throw new ProviderResponseException($"Question '{binding.Name}' is missing from the response.", _provider.Name);
            }

            var mapped = binding.Map(answer, _provider.Name);
            answers[index] = mapped.Value;

            if (_enableTelemetry && mapped.Confidence is { } confidence)
            {
                AdjudgeTelemetry.Confidence.Record(
                    confidence,
                    new TagList
                    {
                        { "definition.id", definition.Id },
                        { "question", binding.Name },
                        { "provider", _provider.Name },
                    });
            }
        }

        return new DecisionResult<TResult>(
            Guid.NewGuid(),
            definition.Id,
            _provider.Name,
            response.Model,
            definition.CreateResult(answers),
            response.Usage,
            DateTimeOffset.UtcNow,
            response.Metadata ?? EmptyMetadata);
    }

    [RequiresUnreferencedCode(ReflectionMessage)]
    [RequiresDynamicCode(ReflectionMessage)]
    private static SerialiseContext SerialiseByReflection(DecisionEngineOptions options) =>
        value => JsonSerializer.SerializeToElement(value, value.GetType(), options.JsonSerializerOptions);

    private void Count(TagList tags, string outcome)
    {
        if (!_enableTelemetry)
        {
            return;
        }

        var counted = tags;
        counted.Add("outcome", outcome);
        AdjudgeTelemetry.Decisions.Add(1, counted);
    }
}

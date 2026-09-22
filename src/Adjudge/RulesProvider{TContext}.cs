using System.Collections.Frozen;
using System.Globalization;

namespace Adjudge.Providers;

/// <summary>
/// A provider that answers from predicates you write in C# against your own context, so the cheap,
/// certain cases never reach a model. The rules are developer-owned code, not an auditable business
/// rules engine: there is no storage, no versioning, no audit trail and nothing a non-developer edits.
/// It is built once with <see cref="RulesProvider.Create{TContext}"/> and is immutable afterwards.
/// <para>
/// A rule that settles a question answers it with all the mass on one option and a
/// <see cref="ConfidenceSource.Heuristic"/> source. A question no rule settles is left out of the
/// response, which declines it, so a rules provider is meant to sit in front of a real one; handed
/// straight to the engine it fails any question its rules do not cover.
/// </para>
/// <para>
/// There is deliberately no Rate registration. Rating places a context on an ordered scale, and an
/// ordinal judgement is not something a keyword predicate can make honestly; rate questions are
/// declined and belong to the provider behind this one.
/// </para>
/// <para>
/// Predicates are run as written and their exceptions are not caught or wrapped: a rule that throws is
/// a bug in the rule, and it reaches the caller unchanged.
/// </para>
/// </summary>
/// <typeparam name="TContext">The context type the rules read, which every decision using this provider must supply.</typeparam>
public sealed class RulesProvider<TContext> : IDecisionProvider
    where TContext : class
{
    private readonly FrozenDictionary<string, RulesRule<TContext>> _rules;

    internal RulesProvider(IReadOnlyDictionary<string, RulesRule<TContext>> rules) =>
        _rules = rules.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Always <c>rules</c>, which is what results and telemetry report.</summary>
    public string Name => "rules";

    /// <summary>Classify and Assert. No Rate, no native confidence, and questions are answered one at a time.</summary>
    public DecisionCapabilities Capabilities => DecisionCapabilities.Classify | DecisionCapabilities.Assert;

    /// <summary>Answers every question its rules settle and omits the rest, so a provider behind it can take them on.</summary>
    /// <param name="request">The questions and the context, whose <see cref="DecisionContext.Value"/> must be a <typeparamref name="TContext"/>.</param>
    /// <param name="ct">Checked before any predicate runs; predicates themselves are synchronous.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="DecisionDefinitionException">The context is not a <typeparamref name="TContext"/>, or the decision's options are not the ones the rules were written over.</exception>
    public Task<ProviderResponse> DecideAsync(ProviderRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        if (request.Context.Value is not TContext context)
        {
            throw new DecisionDefinitionException(
                $"Rules were registered for context '{typeof(TContext).Name}', but decision '{request.DefinitionId}' " +
                $"supplied a '{request.Context.Value.GetType().Name}'.");
        }

        var answers = new Dictionary<string, AnswerSpec>(request.Questions.Count, StringComparer.Ordinal);
        foreach (var question in request.Questions)
        {
            if (_rules.TryGetValue(question.Name, out var rule) && rule(context, question) is { } answer)
            {
                answers[question.Name] = answer;
            }
        }

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["rules.matched"] = answers.Count.ToString(CultureInfo.InvariantCulture),
        };

        return Task.FromResult(new ProviderResponse(answers, Model: null, Usage: null, metadata));
    }
}

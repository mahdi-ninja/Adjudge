using System.ClientModel;
using System.ClientModel.Primitives;
using System.Globalization;
using Adjudge.Providers;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using Polly.Timeout;

namespace Adjudge.OpenAI;

/// <summary>
/// A provider for any OpenAI-compatible chat completions endpoint. It asks one question per chat call and
/// reads the distribution either from the first answer token's log probabilities or from repeated samples.
/// Safe to share across evaluations, and meant to be long-lived.
/// </summary>
public sealed class OpenAIProvider : IDecisionProvider, IDisposable
{
    private readonly HttpClient _client;
    private readonly OpenAIOptions _options;
    private readonly bool _ownsClient;

    /// <summary>Creates a provider over a client the caller owns, which is the form dependency injection uses.</summary>
    /// <param name="client">Its own timeout should be infinite, because the provider applies <see cref="OpenAIOptions.Timeout"/> per attempt.</param>
    /// <param name="options">The configuration, read once at construction.</param>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public OpenAIProvider(HttpClient client, IOptions<OpenAIOptions> options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);

        _client = client;
        _options = options.Value;
    }

    /// <summary>Creates a provider that owns a single <see cref="HttpClient"/>, so it should be long-lived rather than created per call.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="OpenAIException">The options are not valid.</exception>
    public OpenAIProvider(OpenAIOptions options)
        : this(options, new HttpClientHandler())
    {
    }

    internal OpenAIProvider(OpenAIOptions options, HttpMessageHandler innerHandler)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(innerHandler);

        var failures = OpenAIOptionsValidator.Failures(options);
        if (failures.Count > 0)
        {
            throw new OpenAIException(string.Join(" ", failures));
        }

        var handler = OpenAIResilience.CreateHandler(options);
        handler.InnerHandler = innerHandler;

        _options = options;
        _client = new HttpClient(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
        _ownsClient = true;
    }

    /// <summary>Always <c>openai</c>, which is what results and telemetry report.</summary>
    public string Name => OpenAIException.ProviderName;

    /// <summary>Every question kind. Confidence is derived by the library, and each question costs its own call.</summary>
    public DecisionCapabilities Capabilities =>
        DecisionCapabilities.Classify |
        DecisionCapabilities.Rate |
        DecisionCapabilities.Assert;

    /// <summary>Asks one chat call per question, up to <see cref="OpenAIOptions.MaxConcurrentCalls"/> at a time, and maps the replies back.</summary>
    /// <remarks>
    /// A question that fails propagates its exception and the whole evaluation fails with it, so the usage
    /// the calls that did complete reported is not reported anywhere.
    /// </remarks>
    /// <exception cref="OpenAIException">No API key or model was configured, or a call failed.</exception>
    /// <exception cref="DecisionDefinitionException">A question cannot be rendered as a labelled prompt.</exception>
    /// <exception cref="ProviderResponseException">A reply could not be mapped to the labels that were offered.</exception>
    public async Task<ProviderResponse> DecideAsync(ProviderRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var apiKey = _options.ResolveApiKey() ?? throw new OpenAIException(
            $"No OpenAI API key was configured. Set {nameof(OpenAIOptions)}.{nameof(OpenAIOptions.ApiKey)} " +
            $"or the {OpenAIOptions.ApiKeyVariable} environment variable.");

        var model = _options.ResolveModel() ?? throw new OpenAIException(
            $"No OpenAI model was configured. Set {nameof(OpenAIOptions)}.{nameof(OpenAIOptions.Model)} " +
            $"or the {OpenAIOptions.ModelVariable} environment variable.");

        var plans = Plan(request);
        var chat = CreateChatClient(apiKey, model);

        using var gate = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrentCalls));
        var outcomes = await Task.WhenAll(plans.Select(plan => AnswerAsync(chat, plan, gate, ct))).ConfigureAwait(false);

        var answers = new Dictionary<string, AnswerSpec>(outcomes.Length, StringComparer.Ordinal);
        long input = 0;
        long output = 0;
        var calls = 0;
        var reportedUsage = false;
        string? reportedModel = null;

        foreach (var outcome in outcomes)
        {
            answers[outcome.Answer.Name] = outcome.Answer;
            input += outcome.InputTokens;
            output += outcome.OutputTokens;
            calls += outcome.Calls;
            reportedUsage |= outcome.ReportedUsage;
            reportedModel ??= outcome.Model;
        }

        var usage = reportedUsage ? new Usage(input, output) : null;
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["strategy"] = Describe(_options.Strategy),
            ["calls"] = calls.ToString(CultureInfo.InvariantCulture),
        };

        return new ProviderResponse(answers, reportedModel, usage, metadata);
    }

    /// <summary>Disposes the client only when this provider created it.</summary>
    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }

    private static string Describe(ConfidenceStrategy strategy) =>
        strategy == ConfidenceStrategy.Sampling ? "sampling" : "log_probabilities";

    private static List<QuestionPlan> Plan(ProviderRequest request)
    {
        var state = request.Context.AsJson().GetRawText();
        var plans = new List<QuestionPlan>(request.Questions.Count);
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var question in request.Questions)
        {
            if (!names.Add(question.Name))
            {
                throw new DecisionDefinitionException($"Question '{question.Name}' was supplied more than once.");
            }

            plans.Add(OpenAIPromptBuilder.Build(question, state));
        }

        return plans;
    }

    private ChatClient CreateChatClient(string apiKey, string model)
    {
        var options = new OpenAIClientOptions
        {
            Endpoint = _options.ResolveBaseUrl(),
            Transport = new HttpClientPipelineTransport(_client),
            RetryPolicy = new ClientRetryPolicy(maxRetries: 0),
        };

        return new OpenAIClient(new ApiKeyCredential(apiKey), options).GetChatClient(model);
    }

    private async Task<QuestionOutcome> AnswerAsync(ChatClient chat, QuestionPlan plan, SemaphoreSlim gate, CancellationToken ct)
    {
        if (_options.Strategy == ConfidenceStrategy.Sampling)
        {
            var samples = await Task.WhenAll(Enumerable
                .Range(0, _options.Samples)
                .Select(_ => CompleteAsync(chat, plan, SamplingOptions(), gate, ct)))
                .ConfigureAwait(false);

            return Outcome(OpenAIAnswerMapper.FromSamples(plan, samples), samples);
        }

        var completion = await CompleteAsync(chat, plan, LogProbabilityOptions(), gate, ct).ConfigureAwait(false);
        return Outcome(OpenAIAnswerMapper.FromLogProbabilities(plan, completion), [completion]);
    }

    private ChatCompletionOptions LogProbabilityOptions() => new()
    {
        IncludeLogProbabilities = true,
        TopLogProbabilityCount = _options.TopLogProbabilities,
        MaxOutputTokenCount = 4,

        Temperature = _options.Temperature,
    };

    private ChatCompletionOptions SamplingOptions() => new()
    {
        MaxOutputTokenCount = 4,
        Temperature = (float)_options.SamplingTemperature,
    };

    private static QuestionOutcome Outcome(AnswerSpec answer, ChatCompletion[] completions)
    {
        long input = 0;
        long output = 0;
        var reported = false;
        string? model = null;

        foreach (var completion in completions)
        {
            if (completion.Usage is { } usage)
            {
                input += usage.InputTokenCount;
                output += usage.OutputTokenCount;
                reported = true;
            }

            model ??= string.IsNullOrWhiteSpace(completion.Model) ? null : completion.Model;
        }

        return new QuestionOutcome(answer, model, input, output, completions.Length, reported);
    }

    private static async Task<ChatCompletion> CompleteAsync(
        ChatClient chat,
        QuestionPlan plan,
        ChatCompletionOptions options,
        SemaphoreSlim gate,
        CancellationToken ct)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var result = await chat.CompleteChatAsync(
                [
                    ChatMessage.CreateSystemMessage(OpenAIPromptBuilder.SystemMessage),
                    ChatMessage.CreateUserMessage(plan.UserMessage),
                ],
                options,
                ct).ConfigureAwait(false);

            return result.Value;
        }
        catch (ClientResultException exception) when (!ct.IsCancellationRequested)
        {
            throw OpenAIErrorMapper.Map(exception);
        }
        catch (Exception exception) when (exception is HttpRequestException or TimeoutRejectedException)
        {
            throw new OpenAIException("The OpenAI request could not be completed.", isTransient: true, innerException: exception);
        }
        catch (OperationCanceledException exception) when (!ct.IsCancellationRequested)
        {
            throw new OpenAIException("The OpenAI request timed out.", isTransient: true, innerException: exception);
        }
        finally
        {
            gate.Release();
        }
    }

    private sealed record QuestionOutcome(AnswerSpec Answer, string? Model, long InputTokens, long OutputTokens, int Calls, bool ReportedUsage);
}

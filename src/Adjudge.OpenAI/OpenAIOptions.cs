namespace Adjudge.OpenAI;

/// <summary>
/// How the OpenAI provider is configured. The key, base URL and model each fall back to an environment
/// variable when left unset, so the same code runs against openai.com, Azure OpenAI or a local server.
/// </summary>
public sealed class OpenAIOptions
{
    /// <summary>The name the provider's <c>HttpClient</c> is registered under, for callers that want to configure it further.</summary>
    public const string HttpClientName = "Adjudge.OpenAI";

    /// <summary>The environment variable <see cref="ApiKey"/> falls back to.</summary>
    public const string ApiKeyVariable = "OPENAI_API_KEY";

    /// <summary>The environment variable <see cref="BaseUrl"/> falls back to.</summary>
    public const string BaseUrlVariable = "OPENAI_BASE_URL";

    /// <summary>The environment variable <see cref="Model"/> falls back to.</summary>
    public const string ModelVariable = "OPENAI_MODEL";

    /// <summary>The smallest <see cref="TopLogProbabilities"/> the provider accepts.</summary>
    public const int MinimumTopLogProbabilities = 1;

    /// <summary>The largest <see cref="TopLogProbabilities"/> the provider accepts, which is the ceiling openai.com allows.</summary>
    public const int MaximumTopLogProbabilities = 20;

    /// <summary>The public OpenAI endpoint, used when nothing else is configured.</summary>
    public static Uri DefaultBaseUrl { get; } = new("https://api.openai.com/v1");

    /// <summary>The bearer token. Left unset, the environment supplies it; missing altogether, the first call throws <see cref="OpenAIException"/>.</summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The root the chat completions path hangs off, such as <c>https://api.openai.com/v1</c> or
    /// <c>https://my-resource.services.ai.azure.com/openai/v1</c>. Include the version segment.
    /// </summary>
    public Uri? BaseUrl { get; set; }

    /// <summary>The model to ask for. There is no default, because no two endpoints host the same set.</summary>
    public string? Model { get; set; }

    /// <summary>Which strategy produces the distribution. Defaults to <see cref="ConfidenceStrategy.LogProbabilities"/>.</summary>
    public ConfidenceStrategy Strategy { get; set; } = ConfidenceStrategy.LogProbabilities;

    /// <summary>
    /// How many candidates <see cref="ConfidenceStrategy.LogProbabilities"/> asks for on the answer token. Defaults to 5,
    /// which every endpoint accepts: Azure OpenAI rejects anything above 5 on its v1 endpoint, while openai.com allows
    /// up to 20. Must be between 1 and 20.
    /// </summary>
    public int TopLogProbabilities { get; set; } = 5;

    /// <summary>
    /// The temperature <see cref="ConfidenceStrategy.LogProbabilities"/> asks for. Defaults to 0; set it to null to omit
    /// the parameter altogether, which is what models that reject a temperature require.
    /// </summary>
    public float? Temperature { get; set; } = 0f;

    /// <summary>How many calls <see cref="ConfidenceStrategy.Sampling"/> makes per question. Defaults to 5, and must be at least 2.</summary>
    public int Samples { get; set; } = 5;

    /// <summary>The temperature <see cref="ConfidenceStrategy.Sampling"/> asks for, which has to be above zero for the samples to differ. Defaults to 1.</summary>
    public double SamplingTemperature { get; set; } = 1.0;

    /// <summary>How long one attempt may take, retries aside. Defaults to 10 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>How many times a transient failure is retried, with jittered backoff. Defaults to 2.</summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>How many chat calls one evaluation may have in flight at once, across every question and sample. Defaults to 4.</summary>
    public int MaxConcurrentCalls { get; set; } = 4;

    internal string? ResolveApiKey() => Coalesce(ApiKey, ApiKeyVariable);

    internal string? ResolveModel() => Coalesce(Model, ModelVariable);

    internal Uri ResolveBaseUrl()
    {
        if (BaseUrl is not null)
        {
            return BaseUrl;
        }

        var configured = Coalesce(null, BaseUrlVariable);
        if (configured is null)
        {
            return DefaultBaseUrl;
        }

        if (!Uri.TryCreate(configured, UriKind.Absolute, out var parsed))
        {
            throw new OpenAIException(
                $"The {BaseUrlVariable} environment variable is not an absolute URL. It was '{configured}'.");
        }

        return parsed;
    }

    private static string? Coalesce(string? value, string variable)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var fromEnvironment = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(fromEnvironment) ? null : fromEnvironment;
    }
}

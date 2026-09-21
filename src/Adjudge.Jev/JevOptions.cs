namespace Adjudge.Jev;

/// <summary>How the Jev provider is configured. Each of the key, base URL and model falls back to an environment variable when left unset.</summary>
public sealed class JevOptions
{
    /// <summary>The name the provider's <c>HttpClient</c> is registered under, for callers that want to configure it further.</summary>
    public const string HttpClientName = "Adjudge.Jev";

    /// <summary>The model used when neither <see cref="Model"/> nor its environment variable is set.</summary>
    public const string DefaultModel = "jev-latest";

    /// <summary>The environment variable <see cref="ApiKey"/> falls back to.</summary>
    public const string ApiKeyVariable = "TYPESAFE_API_KEY";

    /// <summary>The environment variable <see cref="BaseUrl"/> falls back to.</summary>
    public const string BaseUrlVariable = "TYPESAFE_BASE_URL";

    /// <summary>The environment variable <see cref="Model"/> falls back to.</summary>
    public const string ModelVariable = "TYPESAFE_DEFAULT_MODEL";

    /// <summary>The service's public endpoint, used when nothing else is configured.</summary>
    public static Uri DefaultBaseUrl { get; } = new("https://api.typesafe.ai");

    /// <summary>The bearer token. Left unset, the environment supplies it; missing altogether, the first call throws <see cref="JevException"/>.</summary>
    public string? ApiKey { get; set; }

    /// <summary>The service root, without the path. Point it at a stub to test against one.</summary>
    public Uri? BaseUrl { get; set; }

    /// <summary>The model to ask for, such as <c>jev-latest</c>.</summary>
    public string? Model { get; set; }

    /// <summary>How long one attempt may take, retries aside. Defaults to 10 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>How many times a transient failure is retried, with jittered backoff. Defaults to 2.</summary>
    public int MaxRetries { get; set; } = 2;

    internal string? ResolveApiKey() => Coalesce(ApiKey, ApiKeyVariable);

    internal string ResolveModel() => Coalesce(Model, ModelVariable) ?? DefaultModel;

    internal Uri ResolveBaseUrl()
    {
        if (BaseUrl is not null)
        {
            return BaseUrl;
        }

        var configured = Coalesce(null, BaseUrlVariable);
        return configured is null ? DefaultBaseUrl : new Uri(configured, UriKind.Absolute);
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

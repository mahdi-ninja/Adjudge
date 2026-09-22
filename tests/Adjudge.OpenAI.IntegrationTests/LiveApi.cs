namespace Adjudge.OpenAI.IntegrationTests;

internal static class LiveApi
{
    public static string? ApiKey => Environment.GetEnvironmentVariable(OpenAIOptions.ApiKeyVariable);

    public static string? BaseUrl => Environment.GetEnvironmentVariable(OpenAIOptions.BaseUrlVariable);

    public static string? Model => Environment.GetEnvironmentVariable(OpenAIOptions.ModelVariable);

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(Model);
}

namespace Adjudge.Jev.IntegrationTests;

internal static class LiveApi
{
    private const string ApiKeyVariable = "TYPESAFE_API_KEY";

    public static string? ApiKey => Environment.GetEnvironmentVariable(ApiKeyVariable);

    public static bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    public static string? SkipReason => IsConfigured
        ? null
        : $"Set {ApiKeyVariable} to run the live Jev integration tests.";
}

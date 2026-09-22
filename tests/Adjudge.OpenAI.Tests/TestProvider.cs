namespace Adjudge.OpenAI.Tests;

internal static class TestProvider
{
    public static OpenAIOptions Options(Action<OpenAIOptions>? configure = null)
    {
        var options = new OpenAIOptions
        {
            ApiKey = "test-key",
            Model = "gpt-4o-mini",
            BaseUrl = new Uri("https://openai.test/v1"),
            MaxRetries = 0,
        };

        configure?.Invoke(options);
        return options;
    }

    public static OpenAIProvider Create(RecordingHandler handler, Action<OpenAIOptions>? configure = null) =>
        new(Options(configure), handler);
}

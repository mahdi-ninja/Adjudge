namespace Adjudge.Jev.Tests;

internal static class TestProvider
{
    public static JevOptions Options(Action<JevOptions>? configure = null)
    {
        var options = new JevOptions { ApiKey = "test-key", MaxRetries = 0 };
        configure?.Invoke(options);
        return options;
    }

    public static JevProvider Create(RecordingHandler handler, Action<JevOptions>? configure = null) =>
        new(Options(configure), handler);
}

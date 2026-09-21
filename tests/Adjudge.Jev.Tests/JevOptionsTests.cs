using Adjudge.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Adjudge.Jev.Tests;

[Collection(SharedEnvironment.Name)]
public sealed class JevOptionsTests
{
    [Fact]
    public void Defaults_WhenNothingIsConfigured_MatchTheDocumentedValues()
    {
        WithVariable(JevOptions.BaseUrlVariable, null, () => WithVariable(JevOptions.ModelVariable, null, () =>
        {
            var options = new JevOptions();

            options.Timeout.ShouldBe(TimeSpan.FromSeconds(10));
            options.MaxRetries.ShouldBe(2);
            options.ResolveBaseUrl().ShouldBe(JevOptions.DefaultBaseUrl);
            options.ResolveModel().ShouldBe("jev-latest");
        }));
    }

    [Fact]
    public void ResolveApiKey_WhenOnlyTheEnvironmentIsSet_UsesIt()
    {
        WithVariable(JevOptions.ApiKeyVariable, "from-environment", () =>
            new JevOptions().ResolveApiKey().ShouldBe("from-environment"));
    }

    [Fact]
    public void ResolveApiKey_WhenBothAreSet_PrefersTheOption()
    {
        WithVariable(JevOptions.ApiKeyVariable, "from-environment", () =>
            new JevOptions { ApiKey = "from-options" }.ResolveApiKey().ShouldBe("from-options"));
    }

    [Fact]
    public void ResolveBaseUrl_WhenOnlyTheEnvironmentIsSet_UsesIt()
    {
        WithVariable(JevOptions.BaseUrlVariable, "https://jev.test/", () =>
            new JevOptions().ResolveBaseUrl().ShouldBe(new Uri("https://jev.test/")));
    }

    [Fact]
    public void ResolveModel_WhenOnlyTheEnvironmentIsSet_UsesIt()
    {
        WithVariable(JevOptions.ModelVariable, "jev-1.13.0", () =>
            new JevOptions().ResolveModel().ShouldBe("jev-1.13.0"));
    }

    [Fact]
    public void Validate_WhenTimeoutIsNotPositive_Fails()
    {
        var result = new JevOptionsValidator().Validate(null, new JevOptions { Timeout = TimeSpan.Zero });

        result.Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenMaxRetriesIsNegative_Fails()
    {
        var result = new JevOptionsValidator().Validate(null, new JevOptions { MaxRetries = -1 });

        result.Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenTheOptionsAreSound_Succeeds()
    {
        new JevOptionsValidator().Validate(null, new JevOptions()).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void AddJev_WhenConfigured_RegistersTheProvider()
    {
        var services = new ServiceCollection();

        services.AddJev(options => options.ApiKey = "test-key");

        using var root = services.BuildServiceProvider();
        var provider = root.GetRequiredService<IDecisionProvider>();

        provider.Name.ShouldBe("jev");
        provider.Capabilities.ShouldBe(
            DecisionCapabilities.Classify |
            DecisionCapabilities.Rate |
            DecisionCapabilities.Assert |
            DecisionCapabilities.NativeConfidence |
            DecisionCapabilities.Batch);
    }

    [Fact]
    public void AddJev_WhenTheOptionsAreInvalid_FailsOnResolution()
    {
        var services = new ServiceCollection();

        services.AddJev(options => options.MaxRetries = -1);

        using var root = services.BuildServiceProvider();

        Should.Throw<OptionsValidationException>(() => root.GetRequiredService<IOptions<JevOptions>>().Value);
    }

    [Fact]
    public void AddJev_OnAnAdjudgeBuilder_RegistersTheProvider()
    {
        var services = new ServiceCollection();

        services.AddAdjudge(o => o.EnableTelemetry = false).AddJev(options => options.ApiKey = "test-key");

        using var root = services.BuildServiceProvider();

        root.GetRequiredService<IDecisionProvider>().Name.ShouldBe("jev");
    }

    private static void WithVariable(string name, string? value, Action assert)
    {
        var previous = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);

        try
        {
            assert();
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, previous);
        }
    }
}

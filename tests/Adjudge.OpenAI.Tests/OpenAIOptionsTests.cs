using Adjudge.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Adjudge.OpenAI.Tests;

[Collection(SharedEnvironment.Name)]
public sealed class OpenAIOptionsTests
{
    [Fact]
    public void Defaults_WhenNothingIsConfigured_MatchTheDocumentedValues()
    {
        WithVariable(OpenAIOptions.BaseUrlVariable, null, () => WithVariable(OpenAIOptions.ModelVariable, null, () =>
        {
            var options = new OpenAIOptions();

            options.Timeout.ShouldBe(TimeSpan.FromSeconds(10));
            options.MaxRetries.ShouldBe(2);
            options.Samples.ShouldBe(5);
            options.SamplingTemperature.ShouldBe(1.0);
            options.MaxConcurrentCalls.ShouldBe(4);
            options.TopLogProbabilities.ShouldBe(5);
            options.Temperature.ShouldBe(0f);
            options.Strategy.ShouldBe(ConfidenceStrategy.LogProbabilities);
            options.ResolveBaseUrl().ShouldBe(new Uri("https://api.openai.com/v1"));
            options.ResolveModel().ShouldBeNull();
        }));
    }

    [Fact]
    public void ResolveApiKey_WhenOnlyTheEnvironmentIsSet_UsesIt()
    {
        WithVariable(OpenAIOptions.ApiKeyVariable, "from-environment", () =>
            new OpenAIOptions().ResolveApiKey().ShouldBe("from-environment"));
    }

    [Fact]
    public void ResolveApiKey_WhenBothAreSet_PrefersTheOption()
    {
        WithVariable(OpenAIOptions.ApiKeyVariable, "from-environment", () =>
            new OpenAIOptions { ApiKey = "from-options" }.ResolveApiKey().ShouldBe("from-options"));
    }

    [Fact]
    public void ResolveBaseUrl_WhenOnlyTheEnvironmentIsSet_UsesIt()
    {
        WithVariable(OpenAIOptions.BaseUrlVariable, "https://my-resource.services.ai.azure.com/openai/v1", () =>
            new OpenAIOptions().ResolveBaseUrl().ShouldBe(new Uri("https://my-resource.services.ai.azure.com/openai/v1")));
    }

    [Fact]
    public void ResolveModel_WhenOnlyTheEnvironmentIsSet_UsesIt()
    {
        WithVariable(OpenAIOptions.ModelVariable, "gpt-4o-mini", () =>
            new OpenAIOptions().ResolveModel().ShouldBe("gpt-4o-mini"));
    }

    [Fact]
    public void Validate_WhenTimeoutIsNotPositive_Fails()
    {
        new OpenAIOptionsValidator().Validate(null, new OpenAIOptions { Timeout = TimeSpan.Zero }).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenMaxRetriesIsNegative_Fails()
    {
        new OpenAIOptionsValidator().Validate(null, new OpenAIOptions { MaxRetries = -1 }).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenFewerThanTwoSamplesAreAsked_Fails()
    {
        new OpenAIOptionsValidator().Validate(null, new OpenAIOptions { Samples = 1 }).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenConcurrencyIsBelowOne_Fails()
    {
        new OpenAIOptionsValidator().Validate(null, new OpenAIOptions { MaxConcurrentCalls = 0 }).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenTheOptionsAreSound_Succeeds()
    {
        new OpenAIOptionsValidator().Validate(null, new OpenAIOptions()).Succeeded.ShouldBeTrue();
    }


    [Fact]
    public void Validate_WhenTheSamplingTemperatureIsNegative_Fails()
    {
        new OpenAIOptionsValidator().Validate(null, new OpenAIOptions { SamplingTemperature = -0.1 }).Failed.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void Validate_WhenTopLogProbabilitiesIsOutOfRange_Fails(int top)
    {
        new OpenAIOptionsValidator().Validate(null, new OpenAIOptions { TopLogProbabilities = top }).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WhenTheBaseUrlIsRelative_Fails()
    {
        var options = new OpenAIOptions { BaseUrl = new Uri("/v1", UriKind.Relative) };

        new OpenAIOptionsValidator().Validate(null, options).Failed.ShouldBeTrue();
    }

    [Theory]
    [InlineData(nameof(OpenAIOptions.Samples))]
    [InlineData(nameof(OpenAIOptions.SamplingTemperature))]
    [InlineData(nameof(OpenAIOptions.MaxConcurrentCalls))]
    [InlineData(nameof(OpenAIOptions.TopLogProbabilities))]
    public void Constructor_WhenAnOptionIsInvalid_ThrowsWithoutADependencyInjectionContainer(string property)
    {
        var options = new OpenAIOptions { ApiKey = "test-key", Model = "gpt-4o-mini" };
        switch (property)
        {
            case nameof(OpenAIOptions.Samples):
                options.Samples = 1;
                break;
            case nameof(OpenAIOptions.SamplingTemperature):
                options.SamplingTemperature = -1;
                break;
            case nameof(OpenAIOptions.MaxConcurrentCalls):
                options.MaxConcurrentCalls = 0;
                break;
            default:
                options.TopLogProbabilities = 21;
                break;
        }

        var exception = Should.Throw<OpenAIException>(() => new OpenAIProvider(options));

        exception.IsTransient.ShouldBeFalse();
        exception.Message.ShouldContain(property);
    }

    [Fact]
    public void Constructor_WhenTheOptionsAreSound_DoesNotThrow()
    {
        using var provider = new OpenAIProvider(new OpenAIOptions { ApiKey = "test-key", Model = "gpt-4o-mini" });

        provider.Name.ShouldBe("openai");
    }

    [Fact]
    public void ResolveBaseUrl_WhenTheEnvironmentValueIsNotAUrl_ThrowsNamingTheVariable()
    {
        WithVariable(OpenAIOptions.BaseUrlVariable, "not a url", () =>
        {
            var exception = Should.Throw<OpenAIException>(() => new OpenAIOptions().ResolveBaseUrl());

            exception.Message.ShouldContain(OpenAIOptions.BaseUrlVariable);
            exception.IsTransient.ShouldBeFalse();
        });
    }

    [Fact]
    public void AddOpenAI_WhenConfigured_RegistersTheProvider()
    {
        var services = new ServiceCollection();

        services.AddOpenAI(options => options.ApiKey = "test-key");

        using var root = services.BuildServiceProvider();
        var provider = root.GetRequiredService<IDecisionProvider>();

        provider.Name.ShouldBe("openai");
        provider.Capabilities.ShouldBe(
            DecisionCapabilities.Classify | DecisionCapabilities.Rate | DecisionCapabilities.Assert);
    }

    [Fact]
    public void AddOpenAI_WhenTheOptionsAreInvalid_FailsOnResolution()
    {
        var services = new ServiceCollection();

        services.AddOpenAI(options => options.Samples = 0);

        using var root = services.BuildServiceProvider();

        Should.Throw<OptionsValidationException>(() => root.GetRequiredService<IOptions<OpenAIOptions>>().Value);
    }

    [Fact]
    public void AddOpenAI_OnAnAdjudgeBuilder_RegistersTheProvider()
    {
        var services = new ServiceCollection();

        services.AddAdjudge(o => o.EnableTelemetry = false).AddOpenAI(options => options.ApiKey = "test-key");

        using var root = services.BuildServiceProvider();

        root.GetRequiredService<IDecisionProvider>().Name.ShouldBe("openai");
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

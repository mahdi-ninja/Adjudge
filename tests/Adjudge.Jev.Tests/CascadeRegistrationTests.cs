using Adjudge.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace Adjudge.Jev.Tests;

public sealed class CascadeRegistrationTests
{
    [Fact]
    public void AddJev_AfterUseCascade_LeavesTheCascadeAsTheProvider()
    {
        var services = new ServiceCollection();
        services.AddAdjudge(o => o.EnableTelemetry = false)
            .UseCascade(cascade => cascade.Stage(new SilentProvider()))
            .AddJev(options => options.ApiKey = "test-key");

        using var root = services.BuildServiceProvider();

        root.GetRequiredService<IDecisionProvider>().ShouldBeOfType<CascadeProvider>();
    }

    [Fact]
    public void AddJev_BeforeUseCascade_StillResolvesTheConcreteProviderForAStage()
    {
        var services = new ServiceCollection();
        services.AddAdjudge(o => o.EnableTelemetry = false)
            .AddJev(options => options.ApiKey = "test-key")
            .UseCascade(cascade => cascade.Stage(new SilentProvider()).Stage<JevProvider>());

        using var root = services.BuildServiceProvider();

        root.GetRequiredService<IDecisionProvider>().ShouldBeOfType<CascadeProvider>();
    }

    private sealed class SilentProvider : IDecisionProvider
    {
        public string Name => "silent";

        public DecisionCapabilities Capabilities =>
            DecisionCapabilities.Classify | DecisionCapabilities.Rate | DecisionCapabilities.Assert;

        public Task<ProviderResponse> DecideAsync(ProviderRequest request, CancellationToken ct) =>
            Task.FromResult(new ProviderResponse(new Dictionary<string, AnswerSpec>()));
    }
}

using Adjudge.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace Adjudge.Tests.Core;

public sealed class ServiceCollectionExtensionsTests
{
    private static readonly Ticket Context = new("I was charged twice");

    [Fact]
    public void AddDecision_WhenResolved_ReturnsTheTypedDecision()
    {
        using var provider = Build();

        provider.GetRequiredService<IDecision<Ticket, Triage>>().ShouldNotBeNull();
    }

    [Fact]
    public void AddDecision_ResolvedTwice_ReturnsTheSameInstance()
    {
        using var provider = Build();

        provider.GetRequiredService<IDecision<Ticket, Triage>>()
            .ShouldBeSameAs(provider.GetRequiredService<IDecision<Ticket, Triage>>());
    }

    [Fact]
    public void AddDecision_RegisteredTwice_KeepsTheFirstRegistration()
    {
        var services = new ServiceCollection();
        services.AddAdjudge()
            .UseProvider<StubProvider>()
            .AddDecision<TriageDecision, Ticket, Triage>()
            .AddDecision<TriageDecision, Ticket, Triage>();

        services.Count(d => d.ServiceType == typeof(IDecision<Ticket, Triage>)).ShouldBe(1);
    }

    [Fact]
    public void UseProvider_WhenResolved_RegistersTheDecisionProvider()
    {
        using var provider = Build();

        provider.GetRequiredService<IDecisionProvider>().ShouldBeOfType<StubProvider>();
    }

    [Fact]
    public void UseProvider_WhenAProviderIsAlreadyRegistered_KeepsTheFirstOne()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDecisionProvider>(new StubProvider { Name = "first" });
        services.AddAdjudge().UseProvider<StubProvider>();

        using var root = services.BuildServiceProvider();

        root.GetRequiredService<IDecisionProvider>().Name.ShouldBe("first");
    }

    [Fact]
    public void AddAdjudge_CalledTwice_Throws()
    {
        var services = new ServiceCollection();
        services.AddAdjudge();

        Should.Throw<InvalidOperationException>(() => services.AddAdjudge());
    }

    [Fact]
    public async Task AddAdjudge_ConfiguredOptions_ReachTheEngine()
    {
        using var root = Build();
        var provider = (StubProvider)root.GetRequiredService<IDecisionProvider>();
        provider.Response = Answers.Triage();

        await root.GetRequiredService<IDecision<Ticket, Triage>>().DecideAsync(Context, TestContext.Current.CancellationToken);

        provider.Requests.Single().DefinitionId.ShouldBe("support.ticket-triage");
    }

    private static ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddAdjudge(o => o.EnableTelemetry = false)
            .UseProvider<StubProvider>()
            .AddDecision<TriageDecision, Ticket, Triage>();

        return services.BuildServiceProvider();
    }
}

using Adjudge.Providers;
using Adjudge.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Adjudge.Tests.Core;

[Collection(CascadeTestGroup.Name)]
public sealed class CascadeServiceCollectionExtensionsTests
{
    private static readonly Ticket Context = new("I was charged twice");

    [Fact]
    public void UseCascade_WithNoStages_Throws()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() => services.AddAdjudge().UseCascade(_ => { }));
    }

    [Fact]
    public void UseCascade_WhenResolved_RegistersTheCascadeAsTheProvider()
    {
        using var root = Build(cascade => cascade.Stage(new FakeDecisionProvider()));

        root.GetRequiredService<IDecisionProvider>().ShouldBeOfType<CascadeProvider>();
    }

    [Fact]
    public void UseCascade_AfterAProviderIsRegistered_ReplacesIt()
    {
        var services = new ServiceCollection();
        services.AddAdjudge(o => o.EnableTelemetry = false)
            .UseProvider<StubProvider>()
            .UseCascade(cascade => cascade.Stage(new FakeDecisionProvider()));

        using var root = services.BuildServiceProvider();

        root.GetRequiredService<IDecisionProvider>().ShouldBeOfType<CascadeProvider>();
    }

    [Fact]
    public async Task Stage_ByType_ResolvesTheProviderFromTheContainer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<StubProvider>();
        services.AddAdjudge(o => o.EnableTelemetry = false)
            .UseCascade(cascade => cascade.Stage<StubProvider>())
            .AddDecision<IntentOnlyDecision, Ticket, IntentOnly>();

        using var root = services.BuildServiceProvider();
        var stub = root.GetRequiredService<StubProvider>();
        stub.Response = Answers.Of(new ClassifyAnswerSpec(
            "intent",
            new Dictionary<string, double> { ["Billing"] = 0.8, ["Tracking"] = 0.15, ["Returns"] = 0.05 }));

        await root.GetRequiredService<IDecision<Ticket, IntentOnly>>().DecideAsync(Context, TestContext.Current.CancellationToken);

        stub.Requests.Single().DefinitionId.ShouldBe("test.intent");
    }

    [Fact]
    public async Task DecideAsync_ThroughTheEngine_AnswersEveryQuestionFromTheStageThatServedIt()
    {
        var result = await TriageAsync();

        (result.Value.Intent.Value, result.Value.Urgency.Nearest, result.Value.Abusive.Probability)
            .ShouldBe((Intent.Billing, Urgency.High, 0.8));
    }

    [Fact]
    public async Task DecideAsync_ThroughTheEngine_ReportsTheCascadeAsTheProvider()
    {
        var result = await TriageAsync();

        result.Provider.ShouldBe("cascade");
    }

    [Fact]
    public async Task DecideAsync_ThroughTheEngine_CarriesTheStageMetadataToTheCaller()
    {
        var result = await TriageAsync();

        (result.Metadata["question.intent.provider"], result.Metadata["question.urgency.stage"], result.Metadata["stage.2.provider"], result.Metadata["stages.called"])
            .ShouldBe(("fake", "1", "fake", "3"));
    }


    [Fact]
    public void UseCascade_CalledTwice_KeepsOnlyTheSecondCascade()
    {
        var first = new FakeDecisionProvider();
        var second = new FakeDecisionProvider();
        var services = new ServiceCollection();
        services.AddAdjudge(o => o.EnableTelemetry = false)
            .UseCascade(cascade => cascade.Stage(first))
            .UseCascade(cascade => cascade.Stage(second));

        using var root = services.BuildServiceProvider();

        root.GetServices<IDecisionProvider>().Count().ShouldBe(1);
    }

    [Fact]
    public void Stage_ByFactory_BuildsTheProviderFromTheContainer()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new FakeDecisionProvider());
        services.AddAdjudge(o => o.EnableTelemetry = false)
            .UseCascade(cascade => cascade.Stage(sp => sp.GetRequiredService<FakeDecisionProvider>()));

        using var root = services.BuildServiceProvider();

        root.GetRequiredService<IDecisionProvider>().ShouldBeOfType<CascadeProvider>();
    }

    private static async Task<DecisionResult<Triage>> TriageAsync()
    {
        var cheap = new FakeDecisionProvider().Classify("intent", "Billing").Declines("urgency").Declines("abusive");
        var middle = new FakeDecisionProvider().Rate("urgency", "High").Declines("abusive");
        var strong = new FakeDecisionProvider().Assert("abusive", 0.8);

        var services = new ServiceCollection();
        services.AddAdjudge(o => o.EnableTelemetry = false)
            .UseCascade(cascade => cascade.Stage(cheap).Stage(middle).Stage(strong))
            .AddDecision<TriageDecision, Ticket, Triage>();

        using var root = services.BuildServiceProvider();

        return await root.GetRequiredService<IDecision<Ticket, Triage>>()
            .DecideAsync(Context, TestContext.Current.CancellationToken);
    }

    private static ServiceProvider Build(Action<CascadeBuilder> configure)
    {
        var services = new ServiceCollection();
        services.AddAdjudge(o => o.EnableTelemetry = false).UseCascade(configure);

        return services.BuildServiceProvider();
    }
}

using Adjudge.Providers;

namespace Adjudge.Tests.Core;

public sealed class DecisionEngineTests
{
    private static readonly Ticket Context = new("I was charged twice");

    [Fact]
    public async Task DecideAsync_ClassifyAnswer_MapsTheTopOption()
    {
        var result = await TriageAsync(new StubProvider { Response = Answers.Triage() });

        result.Value.Intent.Value.ShouldBe(Intent.Billing);
    }

    [Fact]
    public async Task DecideAsync_ClassifyAnswer_MapsEveryProbabilityByMemberName()
    {
        var result = await TriageAsync(new StubProvider { Response = Answers.Triage() });

        result.Value.Intent.Distribution.Probabilities[Intent.Tracking].ShouldBe(0.15, 1e-9);
    }

    [Fact]
    public async Task DecideAsync_ProviderWithoutConfidence_DerivesConfidenceFromTheDistribution()
    {
        var result = await TriageAsync(new StubProvider { Response = Answers.Triage() });

        result.Value.Intent.Confidence.Value.ShouldBe(0.7, 1e-9);
    }

    [Fact]
    public async Task DecideAsync_ProviderWithoutConfidence_MarksTheSourceDerived()
    {
        var result = await TriageAsync(new StubProvider { Response = Answers.Triage() });

        (result.Value.Intent.Confidence.Source, result.Value.Intent.Confidence.ProviderReported)
            .ShouldBe((ConfidenceSource.Derived, null));
    }

    [Fact]
    public async Task DecideAsync_ProviderReportingConfidence_KeepsItAndMarksTheSourceNative()
    {
        var result = await TriageAsync(new StubProvider { Response = Answers.Triage(classifyConfidence: 0.42) });

        (result.Value.Intent.Confidence.Source, result.Value.Intent.Confidence.ProviderReported)
            .ShouldBe((ConfidenceSource.Native, 0.42));
    }

    [Fact]
    public async Task DecideAsync_RateAnswer_PlacesTheValueBetweenLevels()
    {
        var result = await TriageAsync(new StubProvider { Response = Answers.Triage() });

        result.Value.Urgency.Value.ShouldBe(1.5, 1e-9);
    }

    [Fact]
    public async Task DecideAsync_RateAnswer_RoundsToTheNearestLevel()
    {
        var result = await TriageAsync(new StubProvider { Response = Answers.Triage() });

        result.Value.Urgency.Nearest.ShouldBe(Urgency.High);
    }

    [Fact]
    public async Task DecideAsync_AssertAnswer_CarriesTheProbability()
    {
        var result = await TriageAsync(new StubProvider { Response = Answers.Triage(abusive: 0.75) });

        result.Value.Abusive.ShouldBe(new Assertion(0.75));
    }

    [Fact]
    public async Task DecideAsync_UnknownOptionKey_ThrowsProviderResponseException()
    {
        var provider = new StubProvider
        {
            Response = Answers.Of(new ClassifyAnswerSpec("intent", new Dictionary<string, double> { ["Unknown"] = 1.0 })),
        };
        var decision = new DecisionEngine(provider).Create<IntentOnlyDecision, Ticket, IntentOnly>();

        var decide = async () => await decision.DecideAsync(Context, TestContext.Current.CancellationToken);

        (await decide.ShouldThrowAsync<ProviderResponseException>()).Message.ShouldContain("Unknown");
    }

    [Fact]
    public async Task DecideAsync_MissingQuestion_ThrowsProviderResponseException()
    {
        var provider = new StubProvider { Response = Answers.Of() };
        var decision = new DecisionEngine(provider).Create<IntentOnlyDecision, Ticket, IntentOnly>();

        var decide = async () => await decision.DecideAsync(Context, TestContext.Current.CancellationToken);

        (await decide.ShouldThrowAsync<ProviderResponseException>()).Message.ShouldContain("missing from the response");
    }

    [Fact]
    public async Task DecideAsync_ProbabilitiesThatDoNotSumToOne_ThrowsProviderResponseException()
    {
        var provider = new StubProvider
        {
            Response = Answers.Of(new ClassifyAnswerSpec("intent", new Dictionary<string, double> { ["Billing"] = 0.5 })),
        };
        var decision = new DecisionEngine(provider).Create<IntentOnlyDecision, Ticket, IntentOnly>();

        var decide = async () => await decision.DecideAsync(Context, TestContext.Current.CancellationToken);

        (await decide.ShouldThrowAsync<ProviderResponseException>()).Message.ShouldContain("not close enough to 1");
    }

    [Fact]
    public async Task DecideAsync_AnswerOfTheWrongKind_ThrowsProviderResponseException()
    {
        var provider = new StubProvider { Response = Answers.Of(new AssertAnswerSpec("intent", 0.9)) };
        var decision = new DecisionEngine(provider).Create<IntentOnlyDecision, Ticket, IntentOnly>();

        var decide = async () => await decision.DecideAsync(Context, TestContext.Current.CancellationToken);

        await decide.ShouldThrowAsync<ProviderResponseException>();
    }

    [Fact]
    public void Create_ProviderMissingARequiredCapability_ThrowsDecisionDefinitionException()
    {
        var provider = new StubProvider { Capabilities = DecisionCapabilities.Classify };
        var engine = new DecisionEngine(provider);

        var create = () => engine.Create<TriageDecision, Ticket, Triage>();

        create.ShouldThrow<DecisionDefinitionException>().Message.ShouldContain("does not support");
    }

    [Fact]
    public async Task DecideAsync_Always_PassesTheCallersCancellationTokenToTheProvider()
    {
        var provider = new StubProvider { Response = Answers.Triage() };
        var decision = new DecisionEngine(provider).Create<TriageDecision, Ticket, Triage>();
        using var source = new CancellationTokenSource();

        await decision.DecideAsync(Context, source.Token);

        provider.LastToken.ShouldBe(source.Token);
    }

    [Fact]
    public async Task DecideAsync_Always_SendsTheContextAsJson()
    {
        var provider = new StubProvider { Response = Answers.Triage() };
        var decision = new DecisionEngine(provider).Create<TriageDecision, Ticket, Triage>();

        await decision.DecideAsync(Context, TestContext.Current.CancellationToken);

        provider.Requests.Single().Context.AsJson().GetProperty("message").GetString().ShouldBe(Context.Message);
    }

    [Fact]
    public async Task DecideAsync_Always_SendsTheDefinitionId()
    {
        var provider = new StubProvider { Response = Answers.Triage() };
        var decision = new DecisionEngine(provider).Create<TriageDecision, Ticket, Triage>();

        await decision.DecideAsync(Context, TestContext.Current.CancellationToken);

        provider.Requests.Single().DefinitionId.ShouldBe("support.ticket-triage");
    }

    [Fact]
    public async Task DecideAsync_Always_ReportsTheProviderNameModelAndUsage()
    {
        var usage = new Usage(11, 22);
        var provider = new StubProvider { Name = "acme", Response = Answers.Triage(model: "acme-2", usage: usage) };
        var decision = new DecisionEngine(provider).Create<TriageDecision, Ticket, Triage>();

        var result = await decision.DecideAsync(Context, TestContext.Current.CancellationToken);

        (result.Provider, result.Model, result.Usage).ShouldBe(("acme", "acme-2", usage));
    }

    [Fact]
    public async Task DecideAsync_Always_StampsTheResultWithTheCurrentTime()
    {
        var before = DateTimeOffset.UtcNow;

        var result = await TriageAsync(new StubProvider { Response = Answers.Triage() });

        result.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
    }

    [Fact]
    public async Task DecideAsync_TwoEvaluations_ProduceDifferentIds()
    {
        var provider = new StubProvider { Response = Answers.Triage() };
        var decision = new DecisionEngine(provider).Create<TriageDecision, Ticket, Triage>();

        var first = await decision.DecideAsync(Context, TestContext.Current.CancellationToken);
        var second = await decision.DecideAsync(Context, TestContext.Current.CancellationToken);

        first.Id.ShouldNotBe(second.Id);
    }

    [Fact]
    public async Task Create_DecisionInstance_BuildsTheSameDefinition()
    {
        var provider = new StubProvider { Response = Answers.Triage() };
        var decision = new DecisionEngine(provider).Create(new TriageDecision());

        var result = await decision.DecideAsync(Context, TestContext.Current.CancellationToken);

        result.DefinitionId.ShouldBe("support.ticket-triage");
    }

    [Fact]
    public async Task DecideAsync_PreCancelledToken_ThrowsBeforeTheProviderIsCalled()
    {
        var provider = new StubProvider { Response = Answers.Triage() };
        var decision = new DecisionEngine(provider).Create<TriageDecision, Ticket, Triage>();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        var decide = async () => await decision.DecideAsync(Context, source.Token);

        await decide.ShouldThrowAsync<OperationCanceledException>();
        provider.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task DecideAsync_ProbabilitiesWithinTheSumTolerance_AreNormalised()
    {
        var provider = new StubProvider
        {
            Response = Answers.Of(new ClassifyAnswerSpec(
                "intent",
                new Dictionary<string, double> { ["Billing"] = 0.719, ["Tracking"] = 0.3 })),
        };
        var decision = new DecisionEngine(provider).Create<IntentOnlyDecision, Ticket, IntentOnly>();

        var result = await decision.DecideAsync(Context, TestContext.Current.CancellationToken);

        result.Value.Intent.Distribution.Probabilities.Values.Sum().ShouldBe(1, 1e-9);
    }

    [Fact]
    public async Task DecideAsync_ProbabilitiesOutsideTheSumTolerance_ThrowsProviderResponseException()
    {
        var provider = new StubProvider
        {
            Response = Answers.Of(new ClassifyAnswerSpec(
                "intent",
                new Dictionary<string, double> { ["Billing"] = 0.73, ["Tracking"] = 0.3 })),
        };
        var decision = new DecisionEngine(provider).Create<IntentOnlyDecision, Ticket, IntentOnly>();

        var decide = async () => await decision.DecideAsync(Context, TestContext.Current.CancellationToken);

        await decide.ShouldThrowAsync<ProviderResponseException>();
    }

    private static async Task<DecisionResult<Triage>> TriageAsync(StubProvider provider)
    {
        var decision = new DecisionEngine(provider).Create<TriageDecision, Ticket, Triage>();
        return await decision.DecideAsync(Context, TestContext.Current.CancellationToken);
    }
}

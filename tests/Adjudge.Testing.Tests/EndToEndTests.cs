namespace Adjudge.Testing.Tests;

public sealed class EndToEndTests
{
    [Fact]
    public async Task DecideAsync_WithScriptedProvider_ReturnsTypedAnswers()
    {
        var provider = new FakeDecisionProvider()
            .Classify("intent", nameof(Intent.Tracking), 0.92)
            .Rate("urgency", nameof(Urgency.High), 0.7)
            .Assert("abusive", 0.1)
            .WithModel("fake-1");
        var decision = new DecisionEngine(provider).Create<TriageDecision, Ticket, Triage>();

        var result = await decision.DecideAsync(new Ticket("where is my order?"), TestContext.Current.CancellationToken);

        result.Provider.ShouldBe("fake");
        result.Model.ShouldBe("fake-1");
        result.Value.Intent.Value.ShouldBe(Intent.Tracking);
        result.Value.Intent.Confidence.Value.ShouldBe(0.92, 1e-9);
        result.Value.Urgency.Nearest.ShouldBe(Urgency.High);
        result.Value.Abusive.Probability.ShouldBe(0.1);
        provider.LastRequest.ShouldNotBeNull().Questions.Count.ShouldBe(3);
    }
}

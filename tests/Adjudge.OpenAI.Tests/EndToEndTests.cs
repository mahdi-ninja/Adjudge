using System.Net;

namespace Adjudge.OpenAI.Tests;

[Collection(SharedEnvironment.Name)]
public sealed class EndToEndTests
{
    [Fact]
    public async Task DecideAsync_WhenRunThroughTheEngine_ClassifiesTheTopOption()
    {
        var (result, _) = await DecideAsync();

        result.Value.Intent.Value.ShouldBe(Intent.Billing);
    }

    [Fact]
    public async Task DecideAsync_WhenRunThroughTheEngine_DerivesTheConfidenceAndMarksItHeuristic()
    {
        var (result, _) = await DecideAsync();

        result.Value.Intent.Confidence.Source.ShouldBe(ConfidenceSource.Heuristic);
        result.Value.Intent.Confidence.ProviderReported.ShouldBeNull();
    }

    [Fact]
    public async Task DecideAsync_WhenRunThroughTheEngine_RatesTheNearestLevel()
    {
        var (result, _) = await DecideAsync();

        result.Value.Urgency.Nearest.ShouldBe(Urgency.High);
    }

    [Fact]
    public async Task DecideAsync_WhenRunThroughTheEngine_CarriesTheAssertionProbability()
    {
        var (result, _) = await DecideAsync();

        result.Value.Abusive.Probability.ShouldBe(0.2, 0.0001);
    }

    [Fact]
    public async Task DecideAsync_WhenRunThroughTheEngine_MakesOneCallPerQuestion()
    {
        var (result, handler) = await DecideAsync();

        handler.Requests.Count.ShouldBe(3);
        result.Provider.ShouldBe("openai");
        result.Model.ShouldBe(ChatResponses.DefaultModel);
    }

    private static async Task<(DecisionResult<Triage> Result, RecordingHandler Handler)> DecideAsync()
    {
        var handler = RecordingHandler.ForBodies(body => RecordingHandler.Json(HttpStatusCode.OK, Answer(body)));
        using var provider = TestProvider.Create(handler);

        var engine = new DecisionEngine(provider, new DecisionEngineOptions { EnableTelemetry = false });
        var decision = engine.Create<TriageDecision, Ticket, Triage>();

        var result = await decision.DecideAsync(
            new Ticket("I was charged twice and nobody replied"),
            TestContext.Current.CancellationToken);

        return (result, handler);
    }

    // Each question carries its own instructions, so the reply is chosen by what was asked rather than
    // by the order the calls happened to arrive in.
    private static string Answer(string body) => body switch
    {
        _ when body.Contains("What does the customer want?", StringComparison.Ordinal) =>
            ChatResponses.WithLogProbabilities("A", ChatResponses.Tops(("A", 0.8), ("B", 0.15), ("C", 0.05))),
        _ when body.Contains("How urgent is this?", StringComparison.Ordinal) =>
            ChatResponses.WithLogProbabilities("2", ChatResponses.Tops(("2", 0.7), ("1", 0.2), ("0", 0.1))),
        _ when body.Contains("Is the message abusive?", StringComparison.Ordinal) =>
            ChatResponses.WithLogProbabilities("no", ChatResponses.Tops(("no", 0.8), ("yes", 0.2))),
        _ => throw new InvalidOperationException($"No scripted answer for '{body}'."),
    };
}

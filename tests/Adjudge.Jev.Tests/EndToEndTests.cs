using System.Net;
using Adjudge.Providers;

namespace Adjudge.Jev.Tests;

[Collection(SharedEnvironment.Name)]
public sealed class EndToEndTests
{
    private const string Response = """
        {
          "model": "jev-1.13.0",
          "answers": {
            "intent": {
              "type": "choice",
              "choice": "Billing",
              "confidence": 0.81,
              "probabilities": { "Billing": 0.8, "Tracking": 0.15, "Returns": 0.05 }
            },
            "urgency": {
              "type": "score",
              "score": 1.7,
              "confidence": 0.44,
              "probabilities": { "0": 0.1, "1": 0.2, "2": 0.7 }
            },
            "abusive": { "type": "noul", "noul": 0.82 }
          },
          "usage": { "input_tokens": 412, "output_tokens": 57 }
        }
        """;

    [Fact]
    public async Task DecideAsync_WhenRunThroughTheEngine_SendsTheDocumentedWireShape()
    {
        var (_, handler) = await DecideAsync();

        await VerifyJson(handler.Bodies[0]);
    }

    [Fact]
    public async Task DecideAsync_WhenRunThroughTheEngine_ClassifiesTheTopOption()
    {
        var (result, _) = await DecideAsync();

        result.Value.Intent.Value.ShouldBe(Intent.Billing);
    }

    [Fact]
    public async Task DecideAsync_WhenTheProviderReportsConfidence_TheSourceIsNative()
    {
        var (result, _) = await DecideAsync();

        result.Value.Intent.Confidence.Source.ShouldBe(ConfidenceSource.Native);
        result.Value.Intent.Confidence.ProviderReported.ShouldBe(0.81);
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

        result.Value.Abusive.Probability.ShouldBe(0.82);
    }

    [Fact]
    public async Task DecideAsync_WhenRunThroughTheEngine_CarriesTheModelAndUsage()
    {
        var (result, _) = await DecideAsync();

        result.Model.ShouldBe("jev-1.13.0");
        result.Usage.ShouldBe(new Usage(412, 57));
    }

    [Fact]
    public async Task DecideAsync_WhenRunThroughTheEngine_CarriesTheRequestIdMetadata()
    {
        var (result, _) = await DecideAsync();

        result.Metadata["request_id"].ShouldBe("req_e2e");
    }

    private static async Task<(DecisionResult<Triage> Result, RecordingHandler Handler)> DecideAsync()
    {
        var handler = new RecordingHandler(RecordingHandler.Json(HttpStatusCode.OK, Response, "req_e2e"));
        using var provider = TestProvider.Create(handler);

        var engine = new DecisionEngine(provider, new DecisionEngineOptions { EnableTelemetry = false });
        var decision = engine.Create<TriageDecision, Ticket, Triage>();

        var result = await decision.DecideAsync(new Ticket("I was charged twice and nobody replied"), TestContext.Current.CancellationToken);

        return (result, handler);
    }
}

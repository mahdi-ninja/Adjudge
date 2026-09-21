using System.Net;
using Adjudge.Providers;

namespace Adjudge.Jev.Tests;

[Collection(SharedEnvironment.Name)]
public sealed class JevResponseTests
{
    private const string FullResponse = """
        {
          "model": "jev-1.13.0",
          "answers": {
            "intent": {
              "type": "choice",
              "choice": "Billing",
              "confidence": 0.78,
              "probabilities": { "Billing": 0.7, "Tracking": 0.3 }
            },
            "urgency": {
              "type": "score",
              "score": 1.4,
              "confidence": 0.6,
              "legend": { "0": "Can wait days", "1": "Should be handled today", "2": "Needs an immediate response" },
              "probabilities": { "0": 0.2, "1": 0.5, "2": 0.3 }
            },
            "abusive": { "type": "noul", "noul": 0.92 }
          },
          "usage": { "input_tokens": 392, "output_tokens": 65 }
        }
        """;

    [Fact]
    public async Task DecideAsync_WhenAChoiceIsAnswered_MapsProbabilitiesByOptionKey()
    {
        var answer = (ClassifyAnswerSpec)(await DecideAsync(FullResponse)).Answers["intent"];

        answer.Probabilities["Billing"].ShouldBe(0.7);
        answer.Probabilities["Tracking"].ShouldBe(0.3);
    }

    [Fact]
    public async Task DecideAsync_WhenAChoiceIsAnswered_CarriesTheReportedConfidence()
    {
        var answer = (ClassifyAnswerSpec)(await DecideAsync(FullResponse)).Answers["intent"];

        answer.Confidence.ShouldBe(0.78);
    }

    [Fact]
    public async Task DecideAsync_WhenAScoreIsAnswered_MapsPositionsBackToLevelKeys()
    {
        var answer = (RateAnswerSpec)(await DecideAsync(FullResponse)).Answers["urgency"];

        answer.Probabilities.ShouldBe(new Dictionary<string, double>
        {
            ["Low"] = 0.2,
            ["Medium"] = 0.5,
            ["High"] = 0.3,
        });
    }

    [Fact]
    public async Task DecideAsync_WhenAScoreIsAnswered_CarriesTheConfidence()
    {
        var answer = (RateAnswerSpec)(await DecideAsync(FullResponse)).Answers["urgency"];

        answer.Confidence.ShouldBe(0.6);
    }

    [Fact]
    public async Task DecideAsync_WhenANoulIsAnswered_MapsTheProbability()
    {
        var answer = (AssertAnswerSpec)(await DecideAsync(FullResponse)).Answers["abusive"];

        answer.Probability.ShouldBe(0.92);
    }

    [Fact]
    public async Task DecideAsync_WhenTheResponseIsMapped_CarriesTheModel()
    {
        (await DecideAsync(FullResponse)).Model.ShouldBe("jev-1.13.0");
    }

    [Fact]
    public async Task DecideAsync_WhenTheResponseIsMapped_CarriesUsage()
    {
        (await DecideAsync(FullResponse)).Usage.ShouldBe(new Usage(392, 65));
    }

    [Fact]
    public async Task DecideAsync_WhenTheResponseCarriesARequestId_ExposesItAsMetadata()
    {
        var response = await DecideAsync(FullResponse, requestId: "req_123");

        response.Metadata!["request_id"].ShouldBe("req_123");
    }

    [Fact]
    public async Task DecideAsync_WhenTheResponseCarriesNoRequestId_HasNoMetadata()
    {
        (await DecideAsync(FullResponse)).Metadata.ShouldBeNull();
    }

    [Fact]
    public async Task DecideAsync_WhenAnAnswerIsMissing_Throws()
    {
        var body = """{"model":"jev-1.13.0","answers":{"intent":{"type":"choice","probabilities":{"Billing":1}}}}""";

        var exception = await Should.ThrowAsync<ProviderResponseException>(() => DecideAsync(body));

        exception.Provider.ShouldBe("jev");
        exception.Message.ShouldContain("urgency");
    }

    [Fact]
    public async Task DecideAsync_WhenAnAnswerHasAnUnknownType_Throws()
    {
        var body = """{"model":"m","answers":{"abusive":{"type":"ordinal","noul":0.5}}}""";

        await Should.ThrowAsync<ProviderResponseException>(
            () => DecideAsync(body, TestRequest.For(TestRequest.Assert())));
    }

    [Fact]
    public async Task DecideAsync_WhenAScoreRefersToAnUnknownPosition_Throws()
    {
        var body = """{"model":"m","answers":{"urgency":{"type":"score","score":1,"probabilities":{"7":1}}}}""";

        await Should.ThrowAsync<ProviderResponseException>(
            () => DecideAsync(body, TestRequest.For(TestRequest.Rate())));
    }

    [Fact]
    public async Task DecideAsync_WhenTheBodyIsNotJson_Throws()
    {
        await Should.ThrowAsync<ProviderResponseException>(() => DecideAsync("not json at all"));
    }

    [Fact]
    public async Task DecideAsync_WhenTheBodyIsJsonNull_Throws()
    {
        await Should.ThrowAsync<ProviderResponseException>(() => DecideAsync("null"));
    }

    private static async Task<ProviderResponse> DecideAsync(string body, string? requestId = null) =>
        await DecideAsync(body, TestRequest.Mixed(), requestId);

    private static async Task<ProviderResponse> DecideAsync(string body, ProviderRequest request, string? requestId = null)
    {
        var handler = new RecordingHandler(RecordingHandler.Json(HttpStatusCode.OK, body, requestId));
        using var provider = TestProvider.Create(handler);

        return await provider.DecideAsync(request, CancellationToken.None);
    }
}

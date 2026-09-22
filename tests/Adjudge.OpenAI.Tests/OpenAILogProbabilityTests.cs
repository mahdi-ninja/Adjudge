using System.Net;
using Adjudge.Providers;

namespace Adjudge.OpenAI.Tests;

[Collection(SharedEnvironment.Name)]
public sealed class OpenAILogProbabilityTests
{
    [Fact]
    public async Task DecideAsync_WhenTheTopTokensAreLabels_NormalisesTheirProbabilities()
    {
        var answer = await ClassifyAsync("A", ChatResponses.Tops(("A", 0.6), (" B", 0.3), ("c", 0.1)));

        answer.Probabilities["Billing"].ShouldBe(0.6, 0.0001);
        answer.Probabilities["Tracking"].ShouldBe(0.3, 0.0001);
        answer.Probabilities["Returns"].ShouldBe(0.1, 0.0001);
    }

    [Fact]
    public async Task DecideAsync_WhenSomeTopTokensAreNotLabels_IgnoresThemAndRenormalises()
    {
        var answer = await ClassifyAsync("A", ChatResponses.Tops(("A", 0.5), ("Billing", 0.25), (" B", 0.25)));

        answer.Probabilities["Billing"].ShouldBe(2.0 / 3, 0.0001);
        answer.Probabilities["Tracking"].ShouldBe(1.0 / 3, 0.0001);
        answer.Probabilities.ShouldNotContainKey("Returns");
    }

    [Fact]
    public async Task DecideAsync_WhenTheChosenTokenIsMissingFromTheTopList_IncludesIt()
    {
        var handler = new RecordingHandler(RecordingHandler.Json(
            HttpStatusCode.OK,
            ChatResponses.WithLogProbabilities(
                "C",
                ChatResponses.Tops(("A", 0.5), ("B", 0.5)),
                chosen: ("C", Math.Log(0.5)))));
        using var provider = TestProvider.Create(handler);

        var result = await provider.DecideAsync(TestRequest.For(TestRequest.Classify()), TestContext.Current.CancellationToken);

        ((ClassifyAnswerSpec)result.Answers["intent"]).Probabilities.ShouldContainKey("Returns");
    }

    [Fact]
    public async Task DecideAsync_WhenNoTopTokenIsALabel_Throws()
    {
        var handler = new RecordingHandler(RecordingHandler.Json(
            HttpStatusCode.OK,
            ChatResponses.WithLogProbabilities("Billing", ChatResponses.Tops(("Billing", 0.9), ("Refund", 0.1)))));
        using var provider = TestProvider.Create(handler);

        var exception = await Should.ThrowAsync<ProviderResponseException>(
            () => provider.DecideAsync(TestRequest.For(TestRequest.Classify()), TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("Billing");
        exception.Provider.ShouldBe("openai");
    }

    [Fact]
    public async Task DecideAsync_WhenTheResponseCarriesNoLogProbabilities_Throws()
    {
        var handler = new RecordingHandler(RecordingHandler.Json(HttpStatusCode.OK, ChatResponses.Plain("A")));
        using var provider = TestProvider.Create(handler);

        await Should.ThrowAsync<ProviderResponseException>(
            () => provider.DecideAsync(TestRequest.For(TestRequest.Classify()), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DecideAsync_WhenRating_MapsLabelsBackToLevelKeysByPosition()
    {
        var handler = new RecordingHandler(RecordingHandler.Json(
            HttpStatusCode.OK,
            ChatResponses.WithLogProbabilities("2", ChatResponses.Tops(("2", 0.7), ("1", 0.2), ("0", 0.1)))));
        using var provider = TestProvider.Create(handler);

        var result = await provider.DecideAsync(TestRequest.For(TestRequest.Rate()), TestContext.Current.CancellationToken);

        var answer = result.Answers["urgency"].ShouldBeOfType<RateAnswerSpec>();
        answer.Probabilities["High"].ShouldBe(0.7, 0.0001);
        answer.Probabilities["Medium"].ShouldBe(0.2, 0.0001);
        answer.Probabilities["Low"].ShouldBe(0.1, 0.0001);
    }

    [Fact]
    public async Task DecideAsync_WhenAsserting_ReportsTheYesShareOfTheYesAndNoMass()
    {
        var handler = new RecordingHandler(RecordingHandler.Json(
            HttpStatusCode.OK,
            ChatResponses.WithLogProbabilities("no", ChatResponses.Tops(("no", 0.6), ("yes", 0.2), ("maybe", 0.2)))));
        using var provider = TestProvider.Create(handler);

        var result = await provider.DecideAsync(TestRequest.For(TestRequest.Assert()), TestContext.Current.CancellationToken);

        result.Answers["abusive"].ShouldBeOfType<AssertAnswerSpec>().Probability.ShouldBe(0.25, 0.0001);
    }

    [Fact]
    public async Task DecideAsync_WhenAnswering_ReportsNoNativeConfidence()
    {
        (await ClassifyAsync("A", ChatResponses.Tops(("A", 1.0)))).Confidence.ShouldBeNull();
    }

    [Fact]
    public async Task DecideAsync_WhenAnswering_CarriesTheModelUsageAndMetadata()
    {
        var handler = new RecordingHandler(RecordingHandler.Json(
            HttpStatusCode.OK,
            ChatResponses.WithLogProbabilities("A", ChatResponses.Tops(("A", 1.0)), input: 120, output: 2)));
        using var provider = TestProvider.Create(handler);

        var result = await provider.DecideAsync(TestRequest.For(TestRequest.Classify()), TestContext.Current.CancellationToken);

        result.Model.ShouldBe(ChatResponses.DefaultModel);
        result.Usage.ShouldBe(new Usage(120, 2));
        result.Metadata!["strategy"].ShouldBe("log_probabilities");
        result.Metadata["calls"].ShouldBe("1");
    }

    [Fact]
    public async Task DecideAsync_WhenAskingSeveralQuestions_SumsUsageAcrossCalls()
    {
        var handler = new RecordingHandler(_ => RecordingHandler.Json(
            HttpStatusCode.OK,
            ChatResponses.WithLogProbabilities("A", ChatResponses.Tops(("A", 1.0)), input: 100, output: 1)));
        using var provider = TestProvider.Create(handler);

        var result = await provider.DecideAsync(
            TestRequest.For(TestRequest.Classify(), new ClassifySpec("channel", "Which channel?", [new OptionSpec("Email", "Email"), new OptionSpec("Chat", "Chat")])),
            TestContext.Current.CancellationToken);

        result.Usage.ShouldBe(new Usage(200, 2));
        result.Metadata!["calls"].ShouldBe("2");
    }


    [Fact]
    public async Task DecideAsync_WhenTheFirstTokensAreWhitespace_ReadsTheFirstTokenThatCarriesText()
    {
        var handler = new RecordingHandler(RecordingHandler.Json(
            HttpStatusCode.OK,
            ChatResponses.WithLogProbabilities(
                "\nA",
                ChatResponses.Tops(("A", 0.7), ("B", 0.3)),
                leadingTokens: ["\n"])));
        using var provider = TestProvider.Create(handler);

        var result = await provider.DecideAsync(TestRequest.For(TestRequest.Classify()), TestContext.Current.CancellationToken);

        var answer = result.Answers["intent"].ShouldBeOfType<ClassifyAnswerSpec>();
        answer.Probabilities["Billing"].ShouldBe(0.7, 0.0001);
        answer.Probabilities["Tracking"].ShouldBe(0.3, 0.0001);
    }

    [Fact]
    public async Task DecideAsync_WhenEveryContentTokenIsWhitespace_Throws()
    {
        var handler = new RecordingHandler(RecordingHandler.Json(
            HttpStatusCode.OK,
            ChatResponses.WithLogProbabilities(" ", ChatResponses.Tops((" ", 1.0)))));
        using var provider = TestProvider.Create(handler);

        var exception = await Should.ThrowAsync<ProviderResponseException>(
            () => provider.DecideAsync(TestRequest.For(TestRequest.Classify()), TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("whitespace");
    }

    [Theory]
    [InlineData("**A**")]
    [InlineData("- A")]
    [InlineData("`A`")]
    [InlineData("(A)")]
    [InlineData("\"A\"")]
    public async Task DecideAsync_WhenTheLabelIsDecorated_StillParsesIt(string token)
    {
        var answer = await ClassifyAsync(token, ChatResponses.Tops((token, 1.0)));

        answer.Probabilities["Billing"].ShouldBe(1.0, 0.0001);
    }

    [Fact]
    public async Task DecideAsync_WhenAssertingAndOnlyOneAnswerIsInTheTopList_UsesTheResidualRatherThanSaturating()
    {
        var handler = new RecordingHandler(RecordingHandler.Json(
            HttpStatusCode.OK,
            ChatResponses.WithLogProbabilities("yes", ChatResponses.Tops(("yes", 0.9), ("maybe", 0.1)))));
        using var provider = TestProvider.Create(handler);

        var result = await provider.DecideAsync(TestRequest.For(TestRequest.Assert()), TestContext.Current.CancellationToken);

        result.Answers["abusive"].ShouldBeOfType<AssertAnswerSpec>().Probability.ShouldBe(0.9, 0.0001);
    }

    [Fact]
    public async Task DecideAsync_WhenAssertingAndTheAnswerIsAllButCertain_ClampsBelowOne()
    {
        var handler = new RecordingHandler(RecordingHandler.Json(
            HttpStatusCode.OK,
            ChatResponses.WithLogProbabilities("yes", ChatResponses.Tops(("yes", 1.0)))));
        using var provider = TestProvider.Create(handler);

        var result = await provider.DecideAsync(TestRequest.For(TestRequest.Assert()), TestContext.Current.CancellationToken);

        result.Answers["abusive"].ShouldBeOfType<AssertAnswerSpec>().Probability.ShouldBe(0.999, 0.0001);
    }

    [Fact]
    public async Task DecideAsync_WhenTheBudgetRanOutBeforeAnAnswer_SaysSo()
    {
        var handler = new RecordingHandler(RecordingHandler.Json(
            HttpStatusCode.OK,
            ChatResponses.WithoutLogProbabilities(string.Empty, finishReason: "length")));
        using var provider = TestProvider.Create(handler);

        var exception = await Should.ThrowAsync<ProviderResponseException>(
            () => provider.DecideAsync(TestRequest.For(TestRequest.Classify()), TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("token budget");
        exception.Message.ShouldContain("Sampling");
    }

    [Fact]
    public async Task DecideAsync_WhenNoCompletionReportsUsage_ReportsNoUsage()
    {
        var handler = new RecordingHandler(RecordingHandler.Json(
            HttpStatusCode.OK,
            ChatResponses.WithLogProbabilities("A", ChatResponses.Tops(("A", 1.0)), reportUsage: false)));
        using var provider = TestProvider.Create(handler);

        var result = await provider.DecideAsync(TestRequest.For(TestRequest.Classify()), TestContext.Current.CancellationToken);

        result.Usage.ShouldBeNull();
    }

    private static async Task<ClassifyAnswerSpec> ClassifyAsync(string content, (string Token, double LogProbability)[] top)
    {
        var handler = new RecordingHandler(RecordingHandler.Json(
            HttpStatusCode.OK,
            ChatResponses.WithLogProbabilities(content, top)));
        using var provider = TestProvider.Create(handler);

        var result = await provider.DecideAsync(TestRequest.For(TestRequest.Classify()), TestContext.Current.CancellationToken);

        return result.Answers["intent"].ShouldBeOfType<ClassifyAnswerSpec>();
    }
}

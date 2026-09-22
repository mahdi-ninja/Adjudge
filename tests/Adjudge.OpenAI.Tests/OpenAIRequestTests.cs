using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Adjudge.Providers;

namespace Adjudge.OpenAI.Tests;

[Collection(SharedEnvironment.Name)]
public sealed class OpenAIRequestTests
{
    [Fact]
    public async Task DecideAsync_WhenClassifying_SendsTheDocumentedWireShape()
    {
        await VerifySent(await CaptureAsync(TestRequest.Classify(), "A"));
    }

    [Fact]
    public async Task DecideAsync_WhenRating_SendsTheDocumentedWireShape()
    {
        await VerifySent(await CaptureAsync(TestRequest.Rate(), "0"));
    }

    [Fact]
    public async Task DecideAsync_WhenAsserting_SendsTheDocumentedWireShape()
    {
        await VerifySent(await CaptureAsync(TestRequest.Assert(), "yes"));
    }

    [Fact]
    public async Task DecideAsync_WhenARubricSpansSeveralLines_CollapsesItOntoOneLine()
    {
        var classify = new ClassifySpec(
            "intent",
            "What does the customer want?",
            [
                new OptionSpec("Billing", "Charges, invoices\nand refunds"),
                new OptionSpec("Returns", new OptionRubric("Sending\nsomething back", "Billing\nquestions", ["Wrong\nsize"])),
            ]);

        await VerifySent(await CaptureAsync(classify, "A"));
    }

    [Fact]
    public async Task DecideAsync_WhenSending_AsksTheModelToClassifyAndNothingElse()
    {
        var body = await CaptureAsync(TestRequest.Classify(), "A");

        SystemMessage(body).ShouldBe(
            "You are a classifier. You answer with a single label from the list you are given and nothing else. " +
            "No punctuation, no explanation, no restatement of the label's meaning.");
    }

    [Fact]
    public async Task DecideAsync_WhenSending_AsksForFiveTopLogProbabilitiesByDefault()
    {
        var body = await CaptureAsync(TestRequest.Classify(), "A");

        JsonDocument.Parse(body).RootElement.GetProperty("top_logprobs").GetInt32().ShouldBe(5);
    }

    [Fact]
    public async Task DecideAsync_WhenTheTemperatureIsNull_OmitsTheParameter()
    {
        var handler = Handler("A");
        using var provider = TestProvider.Create(handler, options => options.Temperature = null);

        await provider.DecideAsync(TestRequest.For(TestRequest.Classify()), TestContext.Current.CancellationToken);

        JsonDocument.Parse(handler.Bodies[0]).RootElement.TryGetProperty("temperature", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task DecideAsync_WhenAssertHasNoCriteria_OmitsTheAnswersBlock()
    {
        var body = await CaptureAsync(TestRequest.Assert(trueMeans: null, falseMeans: null), "yes");

        UserMessage(body).ShouldNotContain("Answers:");
        UserMessage(body).ShouldContain("Reply with exactly one label: yes, no.");
    }

    [Fact]
    public async Task DecideAsync_WhenSending_LimitsOutputWithMaxCompletionTokens()
    {
        var body = await CaptureAsync(TestRequest.Classify(), "A");
        var root = JsonDocument.Parse(body).RootElement;

        root.GetProperty("max_completion_tokens").GetInt32().ShouldBe(4);
        root.TryGetProperty("max_tokens", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task DecideAsync_WhenSending_PostsToTheConfiguredBaseUrl()
    {
        var handler = Handler("A");
        using var provider = TestProvider.Create(handler, options => options.BaseUrl = new Uri("https://ollama.test/v1"));

        await provider.DecideAsync(TestRequest.For(TestRequest.Classify()), TestContext.Current.CancellationToken);

        handler.Requests[0].RequestUri!.ToString().ShouldBe("https://ollama.test/v1/chat/completions");
    }

    [Fact]
    public async Task DecideAsync_WhenSending_SetsTheBearerAuthorizationHeader()
    {
        var handler = Handler("A");
        using var provider = TestProvider.Create(handler, options => options.ApiKey = "secret-key");

        await provider.DecideAsync(TestRequest.For(TestRequest.Classify()), TestContext.Current.CancellationToken);

        handler.Requests[0].Headers.Authorization!.Scheme.ShouldBe("Bearer");
        handler.Requests[0].Headers.Authorization!.Parameter.ShouldBe("secret-key");
    }

    [Fact]
    public async Task DecideAsync_WhenSampling_AsksForNoLogProbabilitiesAndTheSamplingTemperature()
    {
        var handler = new RecordingHandler(_ => RecordingHandler.Json(HttpStatusCode.OK, ChatResponses.Plain("A")));
        using var provider = TestProvider.Create(handler, options =>
        {
            options.Strategy = ConfidenceStrategy.Sampling;
            options.Samples = 2;
            options.SamplingTemperature = 0.7;
        });

        await provider.DecideAsync(TestRequest.For(TestRequest.Classify()), TestContext.Current.CancellationToken);

        var root = JsonDocument.Parse(handler.Bodies[0]).RootElement;
        root.TryGetProperty("logprobs", out _).ShouldBeFalse();
        root.GetProperty("temperature").GetDouble().ShouldBe(0.7, 0.001);
    }

    [Fact]
    public async Task DecideAsync_WhenMoreThanTwentySixOptionsAreDeclared_Throws()
    {
        var options = Enumerable.Range(0, 27)
            .Select(index => new OptionSpec($"O{index}", $"Option {index}"))
            .ToArray();

        var exception = await Should.ThrowAsync<DecisionDefinitionException>(
            () => CaptureAsync(new ClassifySpec("intent", "What?", options), "A"));

        exception.Message.ShouldContain("between 1 and 26 options");
    }

    [Fact]
    public async Task DecideAsync_WhenNoOptionsAreDeclared_Throws()
    {
        await Should.ThrowAsync<DecisionDefinitionException>(
            () => CaptureAsync(new ClassifySpec("intent", "What?", []), "A"));
    }

    [Fact]
    public async Task DecideAsync_WhenTooFewLevelsAreDeclared_Throws()
    {
        var rate = new RateSpec("urgency", "How urgent?", [new LevelSpec("Low", "Can wait")]);

        var exception = await Should.ThrowAsync<DecisionDefinitionException>(() => CaptureAsync(rate, "0"));

        exception.Message.ShouldContain("between 2 and 10 levels");
    }

    [Fact]
    public async Task DecideAsync_WhenTooManyLevelsAreDeclared_Throws()
    {
        var levels = Enumerable.Range(0, 11)
            .Select(index => new LevelSpec($"L{index}", $"Level {index}"))
            .ToArray();

        await Should.ThrowAsync<DecisionDefinitionException>(
            () => CaptureAsync(new RateSpec("urgency", "How urgent?", levels), "0"));
    }

    [Fact]
    public async Task DecideAsync_WhenTwoQuestionsShareAName_Throws()
    {
        var handler = Handler("yes");
        using var provider = TestProvider.Create(handler);

        await Should.ThrowAsync<DecisionDefinitionException>(
            () => provider.DecideAsync(TestRequest.For(TestRequest.Assert(), TestRequest.Assert()), TestContext.Current.CancellationToken));

        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task DecideAsync_WhenARubricIsNeitherStringNorOptionRubric_Throws()
    {
        var classify = new ClassifySpec("intent", "What?", [new OptionSpec("Billing", new Rubric("Insults"))]);

        var exception = await Should.ThrowAsync<DecisionDefinitionException>(() => CaptureAsync(classify, "A"));

        exception.Message.ShouldContain("intent.Billing");
    }

    internal static RecordingHandler Handler(string label) => new(_ => RecordingHandler.Json(
        HttpStatusCode.OK,
        ChatResponses.WithLogProbabilities(label, ChatResponses.Tops((label, 1.0)))));

    private static string UserMessage(string body) => Message(body, index: 1);

    private static string SystemMessage(string body) => Message(body, index: 0);

    private static string Message(string body, int index) =>
        JsonDocument.Parse(body).RootElement.GetProperty("messages")[index].GetProperty("content").GetString()!;

    // The system message is asserted once, explicitly, so the snapshots only carry what varies per
    // question: the user message and the parameters around it.
    private static Task VerifySent(string body)
    {
        var parameters = JsonNode.Parse(body)!.AsObject();
        parameters.Remove("messages");

        return Verify(new
        {
            Parameters = parameters.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            User = UserMessage(body),
        });
    }

    private static async Task<string> CaptureAsync(QuestionSpec question, string label)
    {
        var handler = Handler(label);
        using var provider = TestProvider.Create(handler);

        await provider.DecideAsync(TestRequest.For(question), TestContext.Current.CancellationToken);

        return handler.Bodies[0];
    }

    private sealed record Rubric(string Text);
}

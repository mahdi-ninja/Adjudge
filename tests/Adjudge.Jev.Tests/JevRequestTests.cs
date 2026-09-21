using System.Net;
using System.Text.Json;
using Adjudge.Providers;

namespace Adjudge.Jev.Tests;

[Collection(SharedEnvironment.Name)]
public sealed class JevRequestTests
{
    private const string EmptyResponse = """
        {"model":"jev-1.13.0","answers":{},"usage":{"input_tokens":1,"output_tokens":1}}
        """;

    [Fact]
    public async Task DecideAsync_WhenAskingEveryKind_SendsTheDocumentedWireShape()
    {
        var body = await CaptureAsync(TestRequest.Mixed());

        await VerifyJson(body);
    }

    [Fact]
    public async Task DecideAsync_WhenAssertHasNoCriteria_OmitsCriteria()
    {
        var body = await CaptureAsync(TestRequest.For(TestRequest.Assert()));

        Question(body, "abusive").TryGetProperty("criteria", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task DecideAsync_WhenAssertHasOnlyTrueMeans_SendsOnlyThatCriterion()
    {
        var body = await CaptureAsync(TestRequest.For(TestRequest.Assert(trueMeans: "Insults or threats")));

        var criteria = Question(body, "abusive").GetProperty("criteria");

        criteria.GetProperty("true").GetString().ShouldBe("Insults or threats");
        criteria.TryGetProperty("false", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task DecideAsync_WhenSending_SetsTheBearerAuthorizationHeader()
    {
        var handler = new RecordingHandler(RecordingHandler.Json(HttpStatusCode.OK, EmptyResponse));
        using var provider = TestProvider.Create(handler, options => options.ApiKey = "secret-key");

        await provider.DecideAsync(TestRequest.For(), CancellationToken.None);

        handler.Requests[0].Headers.Authorization!.Scheme.ShouldBe("Bearer");
        handler.Requests[0].Headers.Authorization!.Parameter.ShouldBe("secret-key");
    }

    [Fact]
    public async Task DecideAsync_WhenSending_PostsToTheSystemOneEndpoint()
    {
        var handler = new RecordingHandler(RecordingHandler.Json(HttpStatusCode.OK, EmptyResponse));
        using var provider = TestProvider.Create(handler, options => options.BaseUrl = new Uri("https://jev.test/"));

        await provider.DecideAsync(TestRequest.For(), CancellationToken.None);

        handler.Requests[0].RequestUri!.ToString().ShouldBe("https://jev.test/v1/systemone");
    }

    [Fact]
    public async Task DecideAsync_WhenSending_SendsTheConfiguredModel()
    {
        var body = await CaptureAsync(TestRequest.For());

        JsonDocument.Parse(body).RootElement.GetProperty("model").GetString().ShouldBe(JevOptions.DefaultModel);
    }

    [Fact]
    public async Task DecideAsync_WhenTooFewLevelsAreDeclared_Throws()
    {
        var rate = new RateSpec("urgency", "How urgent?", [new LevelSpec("Low", "Can wait")]);

        var exception = await Should.ThrowAsync<DecisionDefinitionException>(() => CaptureAsync(TestRequest.For(rate)));

        exception.Message.ShouldContain("between 2 and 10 levels");
    }

    [Fact]
    public async Task DecideAsync_WhenTooManyLevelsAreDeclared_Throws()
    {
        var levels = Enumerable.Range(0, 11)
            .Select(index => new LevelSpec($"L{index}", $"Level {index}"))
            .ToArray();

        await Should.ThrowAsync<DecisionDefinitionException>(
            () => CaptureAsync(TestRequest.For(new RateSpec("urgency", "How urgent?", levels))));
    }

    [Fact]
    public async Task DecideAsync_WhenTooManyOptionsAreDeclared_Throws()
    {
        var options = Enumerable.Range(0, 256)
            .Select(index => new OptionSpec($"O{index}", $"Option {index}"))
            .ToArray();

        var exception = await Should.ThrowAsync<DecisionDefinitionException>(
            () => CaptureAsync(TestRequest.For(new ClassifySpec("intent", "What?", options))));

        exception.Message.ShouldContain("between 1 and 255 options");
    }

    [Fact]
    public async Task DecideAsync_WhenNoOptionsAreDeclared_Throws()
    {
        await Should.ThrowAsync<DecisionDefinitionException>(
            () => CaptureAsync(TestRequest.For(new ClassifySpec("intent", "What?", []))));
    }

    [Fact]
    public async Task DecideAsync_WhenTwoQuestionsShareAName_Throws()
    {
        await Should.ThrowAsync<DecisionDefinitionException>(
            () => CaptureAsync(TestRequest.For(TestRequest.Assert(), TestRequest.Assert())));
    }

    [Fact]
    public async Task DecideAsync_WhenARubricIsNeitherStringNorOptionRubric_Throws()
    {
        var classify = new ClassifySpec("intent", "What?", [new OptionSpec("Billing", new Rubric("Insults"))]);

        var exception = await Should.ThrowAsync<DecisionDefinitionException>(() => CaptureAsync(TestRequest.For(classify)));

        exception.Message.ShouldContain("intent.Billing");
    }

    private static async Task<string> CaptureAsync(ProviderRequest request, Action<JevOptions>? configure = null)
    {
        var handler = new RecordingHandler(RecordingHandler.Json(HttpStatusCode.OK, EmptyResponse));
        using var provider = TestProvider.Create(handler, configure);

        try
        {
            await provider.DecideAsync(request, CancellationToken.None);
        }
        catch (ProviderResponseException)
        {
            // The canned response answers nothing; these tests only assert on what was sent.
        }

        return handler.Bodies[0];
    }

    private static JsonElement Question(string body, string name) =>
        JsonDocument.Parse(body).RootElement.GetProperty("questions").GetProperty(name);

    private sealed record Rubric(string Text);
}

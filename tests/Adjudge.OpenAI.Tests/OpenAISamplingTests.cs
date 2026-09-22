using System.Net;
using Adjudge.Providers;

namespace Adjudge.OpenAI.Tests;

[Collection(SharedEnvironment.Name)]
public sealed class OpenAISamplingTests
{
    [Fact]
    public async Task DecideAsync_WhenSampling_TurnsLabelCountsIntoProbabilities()
    {
        var (answer, _) = await ClassifyAsync("B", "B", "A", "B", "C");

        answer.Probabilities["Tracking"].ShouldBe(0.6, 0.0001);
        answer.Probabilities["Billing"].ShouldBe(0.2, 0.0001);
        answer.Probabilities["Returns"].ShouldBe(0.2, 0.0001);
    }

    [Fact]
    public async Task DecideAsync_WhenSampling_MakesOneCallPerSample()
    {
        var (_, handler) = await ClassifyAsync("A", "A", "A", "A", "A");

        handler.Requests.Count.ShouldBe(5);
    }

    [Fact]
    public async Task DecideAsync_WhenOneSampleIsUnparseable_IgnoresItAndRenormalises()
    {
        var (answer, _) = await ClassifyAsync("A", "A", "I cannot help with that", "B", "A");

        answer.Probabilities["Billing"].ShouldBe(0.75, 0.0001);
        answer.Probabilities["Tracking"].ShouldBe(0.25, 0.0001);
    }

    [Fact]
    public async Task DecideAsync_WhenEverySampleIsUnparseable_Throws()
    {
        var handler = Handler("sorry", "sorry", "sorry", "sorry", "sorry");
        using var provider = Provider(handler);

        var exception = await Should.ThrowAsync<ProviderResponseException>(
            () => provider.DecideAsync(TestRequest.For(TestRequest.Classify()), TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("sorry");
    }

    [Fact]
    public async Task DecideAsync_WhenSamplingAnAssertion_ReportsTheYesShare()
    {
        var handler = Handler("yes", "no", "yes", "yes", "no");
        using var provider = Provider(handler);

        var result = await provider.DecideAsync(TestRequest.For(TestRequest.Assert()), TestContext.Current.CancellationToken);

        result.Answers["abusive"].ShouldBeOfType<AssertAnswerSpec>().Probability.ShouldBe(0.6, 0.0001);
    }

    [Fact]
    public async Task DecideAsync_WhenSampling_ReportsTheStrategyAndCallCount()
    {
        var handler = Handler("A", "A", "A", "A", "A");
        using var provider = Provider(handler);

        var result = await provider.DecideAsync(TestRequest.For(TestRequest.Classify()), TestContext.Current.CancellationToken);

        result.Metadata!["strategy"].ShouldBe("sampling");
        result.Metadata["calls"].ShouldBe("5");
        result.Usage.ShouldBe(new Usage(50, 5));
    }


    [Theory]
    [InlineData("**A**")]
    [InlineData("- A")]
    [InlineData("`A`")]
    [InlineData("(A)")]
    [InlineData("\"A\"")]
    [InlineData("Answer: A")]
    [InlineData("A: the customer is asking about charges")]
    [InlineData("A customer wants billing")]
    public async Task DecideAsync_WhenASampleDecoratesOrIntroducesTheLabel_StillParsesIt(string content)
    {
        var handler = Handler(content, content);
        using var provider = Provider(handler, options => options.Samples = 2);

        var result = await provider.DecideAsync(TestRequest.For(TestRequest.Classify()), TestContext.Current.CancellationToken);

        result.Answers["intent"].ShouldBeOfType<ClassifyAnswerSpec>().Probabilities["Billing"].ShouldBe(1.0, 0.0001);
    }

    [Fact]
    public async Task DecideAsync_WhenASampleOnlyMentionsALabelInsideAWord_IgnoresIt()
    {
        var handler = Handler("Anything but that", "B");
        using var provider = Provider(handler, options => options.Samples = 2);

        var result = await provider.DecideAsync(TestRequest.For(TestRequest.Classify()), TestContext.Current.CancellationToken);

        var answer = result.Answers["intent"].ShouldBeOfType<ClassifyAnswerSpec>();
        answer.Probabilities["Tracking"].ShouldBe(1.0, 0.0001);
        answer.Probabilities.ShouldNotContainKey("Billing");
    }

    [Fact]
    public async Task DecideAsync_WhenSamplingAClassification_MarksTheSourceSampled()
    {
        var (answer, _) = await ClassifyAsync("A", "A", "A", "A", "A");

        answer.Source.ShouldBe(ConfidenceSource.Sampled);
    }

    [Fact]
    public async Task DecideAsync_WhenSamplingARating_MarksTheSourceSampled()
    {
        var handler = Handler("2", "2", "2", "1", "2");
        using var provider = Provider(handler);

        var result = await provider.DecideAsync(TestRequest.For(TestRequest.Rate()), TestContext.Current.CancellationToken);

        result.Answers["urgency"].ShouldBeOfType<RateAnswerSpec>().Source.ShouldBe(ConfidenceSource.Sampled);
    }

    private static RecordingHandler Handler(params string[] contents) =>
        new(index => RecordingHandler.Json(HttpStatusCode.OK, ChatResponses.Plain(contents[index])));

    private static OpenAIProvider Provider(RecordingHandler handler, Action<OpenAIOptions>? configure = null) =>
        TestProvider.Create(handler, options =>
        {
            options.Strategy = ConfidenceStrategy.Sampling;
            configure?.Invoke(options);
        });

    private static async Task<(ClassifyAnswerSpec Answer, RecordingHandler Handler)> ClassifyAsync(params string[] contents)
    {
        var handler = Handler(contents);
        using var provider = Provider(handler);

        var result = await provider.DecideAsync(TestRequest.For(TestRequest.Classify()), TestContext.Current.CancellationToken);

        return (result.Answers["intent"].ShouldBeOfType<ClassifyAnswerSpec>(), handler);
    }
}

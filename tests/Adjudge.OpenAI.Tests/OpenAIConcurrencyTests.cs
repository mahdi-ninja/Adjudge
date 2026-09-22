using System.Net;
using Adjudge.Providers;

namespace Adjudge.OpenAI.Tests;

[Collection(SharedEnvironment.Name)]
public sealed class OpenAIConcurrencyTests
{
    [Fact]
    public async Task DecideAsync_WhenAskingSixQuestions_MakesSixCalls()
    {
        var (_, handler) = await DecideAsync(maxConcurrent: 2);

        handler.Requests.Count.ShouldBe(6);
    }

    [Fact]
    public async Task DecideAsync_WhenAskingSixQuestions_KeepsTheConcurrencyLimitBusyWithoutExceedingIt()
    {
        var (_, handler) = await DecideAsync(maxConcurrent: 2);

        handler.MaxInFlight.ShouldBe(2);
    }

    [Fact]
    public async Task DecideAsync_WhenTheLimitIsFour_RunsQuestionsInParallel()
    {
        var (_, handler) = await DecideAsync(maxConcurrent: 4);

        handler.MaxInFlight.ShouldBeGreaterThan(1);
        handler.MaxInFlight.ShouldBeLessThanOrEqualTo(4);
    }

    [Fact]
    public async Task DecideAsync_WhenAskingSixQuestions_AnswersEveryOne()
    {
        var (response, _) = await DecideAsync(maxConcurrent: 3);

        response.Answers.Count.ShouldBe(6);
    }

    [Fact]
    public async Task DecideAsync_WhenTheTokenIsCancelledBeforeTheCall_Stops()
    {
        var handler = new RecordingHandler(
            _ => RecordingHandler.Json(HttpStatusCode.OK, ChatResponses.WithLogProbabilities("A", ChatResponses.Tops(("A", 1.0)))),
            TimeSpan.FromMilliseconds(200));
        using var provider = TestProvider.Create(handler, options => options.MaxConcurrentCalls = 1);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Should.ThrowAsync<OperationCanceledException>(
            () => provider.DecideAsync(Request(), cancellation.Token));
    }

    [Fact]
    public async Task DecideAsync_WhenTheTokenIsCancelledWhileTheRequestIsInFlight_Stops()
    {
        var handler = new RecordingHandler(
            _ => RecordingHandler.Json(HttpStatusCode.OK, ChatResponses.WithLogProbabilities("A", ChatResponses.Tops(("A", 1.0)))),
            TimeSpan.FromSeconds(5));
        using var provider = TestProvider.Create(handler, options =>
        {
            options.MaxConcurrentCalls = 1;
            options.Timeout = TimeSpan.FromSeconds(30);
        });
        using var cancellation = new CancellationTokenSource();

        var deciding = provider.DecideAsync(Request(), cancellation.Token);

        // Cancelling only once a request has arrived puts the cancellation mid-flight rather than before the send.
        while (handler.Requests.Count == 0)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => deciding);
    }

    private static ProviderRequest Request() => TestRequest.For([.. Enumerable.Range(0, 6).Select(index =>
        (QuestionSpec)new ClassifySpec(
            $"question{index}",
            $"Which option fits case {index}?",
            [new OptionSpec("Yes", "It fits"), new OptionSpec("No", "It does not")]))]);

    private static async Task<(ProviderResponse Response, RecordingHandler Handler)> DecideAsync(int maxConcurrent)
    {
        var handler = new RecordingHandler(
            _ => RecordingHandler.Json(HttpStatusCode.OK, ChatResponses.WithLogProbabilities("A", ChatResponses.Tops(("A", 1.0)))),
            TimeSpan.FromMilliseconds(40));
        using var provider = TestProvider.Create(handler, options => options.MaxConcurrentCalls = maxConcurrent);

        var response = await provider.DecideAsync(Request(), TestContext.Current.CancellationToken);

        return (response, handler);
    }
}

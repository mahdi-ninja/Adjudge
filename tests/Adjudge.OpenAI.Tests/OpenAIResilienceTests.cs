using System.Net;

namespace Adjudge.OpenAI.Tests;

[Collection(SharedEnvironment.Name)]
public sealed class OpenAIResilienceTests
{
    [Fact]
    public async Task DecideAsync_WhenTheFirstAttemptIsRateLimited_RetriesAndSucceeds()
    {
        var handler = new RecordingHandler(
            RecordingHandler.Json(HttpStatusCode.TooManyRequests, "{}"),
            RecordingHandler.Json(HttpStatusCode.OK, ChatResponses.WithLogProbabilities("A", ChatResponses.Tops(("A", 1.0)))));
        using var provider = TestProvider.Create(handler, options => options.MaxRetries = 2);

        var response = await provider.DecideAsync(TestRequest.For(TestRequest.Classify()), TestContext.Current.CancellationToken);

        response.Model.ShouldBe(ChatResponses.DefaultModel);
        handler.Requests.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(408)]
    [InlineData(503)]
    public async Task DecideAsync_WhenTheFirstAttemptFailsTransiently_RetriesAndSucceeds(int status)
    {
        var handler = new RecordingHandler(
            RecordingHandler.Json((HttpStatusCode)status, "{}"),
            RecordingHandler.Json(HttpStatusCode.OK, ChatResponses.WithLogProbabilities("A", ChatResponses.Tops(("A", 1.0)))));
        using var provider = TestProvider.Create(handler, options => options.MaxRetries = 1);

        await provider.DecideAsync(TestRequest.For(TestRequest.Classify()), TestContext.Current.CancellationToken);

        handler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task DecideAsync_WhenRetriesAreDisabled_SendsOnce()
    {
        var handler = new RecordingHandler(RecordingHandler.Json(HttpStatusCode.TooManyRequests, "{}"));
        using var provider = TestProvider.Create(handler, options => options.MaxRetries = 0);

        await Should.ThrowAsync<OpenAIException>(
            () => provider.DecideAsync(TestRequest.For(TestRequest.Classify()), TestContext.Current.CancellationToken));

        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task DecideAsync_WhenEveryAttemptFails_SendsAtMostOneMoreThanMaxRetries()
    {
        var handler = new RecordingHandler(_ => RecordingHandler.Json(HttpStatusCode.ServiceUnavailable, "{}"));
        using var provider = TestProvider.Create(handler, options => options.MaxRetries = 2);

        await Should.ThrowAsync<OpenAIException>(
            () => provider.DecideAsync(TestRequest.For(TestRequest.Classify()), TestContext.Current.CancellationToken));

        handler.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task DecideAsync_WhenTheAttemptExceedsTheTimeout_ThrowsATransientFailure()
    {
        var handler = new RecordingHandler(
            _ => RecordingHandler.Json(HttpStatusCode.OK, ChatResponses.Plain("A")),
            TimeSpan.FromSeconds(5));
        using var provider = TestProvider.Create(handler, options => options.Timeout = TimeSpan.FromMilliseconds(50));

        var exception = await Should.ThrowAsync<OpenAIException>(
            () => provider.DecideAsync(TestRequest.For(TestRequest.Classify()), TestContext.Current.CancellationToken));

        exception.IsTransient.ShouldBeTrue();
    }
}

using System.Diagnostics;
using System.Net;

namespace Adjudge.Jev.Tests;

[Collection(SharedEnvironment.Name)]
public sealed class JevResilienceTests
{
    private const string EmptyResponse = """{"model":"jev-1.13.0","answers":{}}""";

    [Fact]
    public async Task DecideAsync_WhenTheFirstAttemptIsRateLimited_RetriesAndSucceeds()
    {
        var handler = new RecordingHandler(
            RecordingHandler.Json(HttpStatusCode.TooManyRequests, "{}"),
            RecordingHandler.Json(HttpStatusCode.OK, EmptyResponse));
        using var provider = new JevProvider(TestProvider.Options(options => options.MaxRetries = 2), handler);

        var response = await provider.DecideAsync(TestRequest.For(), CancellationToken.None);

        response.Model.ShouldBe("jev-1.13.0");
        handler.Requests.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(408)]
    [InlineData(503)]
    [InlineData(529)]
    public async Task DecideAsync_WhenTheFirstAttemptFailsTransiently_RetriesAndSucceeds(int status)
    {
        var handler = new RecordingHandler(
            RecordingHandler.Json((HttpStatusCode)status, "{}"),
            RecordingHandler.Json(HttpStatusCode.OK, EmptyResponse));
        using var provider = new JevProvider(TestProvider.Options(options => options.MaxRetries = 1), handler);

        var response = await provider.DecideAsync(TestRequest.For(), CancellationToken.None);

        response.Model.ShouldBe("jev-1.13.0");
        handler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task DecideAsync_WhenTheResponseCarriesRetryAfter_WaitsForIt()
    {
        var rateLimited = RecordingHandler.Json(HttpStatusCode.TooManyRequests, "{}");
        rateLimited.Headers.TryAddWithoutValidation("Retry-After", "1");
        var handler = new RecordingHandler(rateLimited, RecordingHandler.Json(HttpStatusCode.OK, EmptyResponse));
        using var provider = new JevProvider(TestProvider.Options(options => options.MaxRetries = 1), handler);
        var started = Stopwatch.GetTimestamp();

        await provider.DecideAsync(TestRequest.For(), CancellationToken.None);

        Stopwatch.GetElapsedTime(started).ShouldBeGreaterThan(TimeSpan.FromMilliseconds(900));
    }

    [Fact]
    public async Task DecideAsync_WhenEveryAttemptFails_SendsAtMostOneMoreThanMaxRetries()
    {
        var handler = new RecordingHandler(
            RecordingHandler.Json(HttpStatusCode.ServiceUnavailable, "{}"),
            RecordingHandler.Json(HttpStatusCode.ServiceUnavailable, "{}"),
            RecordingHandler.Json(HttpStatusCode.ServiceUnavailable, "{}"));
        using var provider = new JevProvider(TestProvider.Options(options => options.MaxRetries = 2), handler);

        await Should.ThrowAsync<JevException>(() => provider.DecideAsync(TestRequest.For(), CancellationToken.None));

        handler.Requests.Count.ShouldBe(3);
    }

    [Fact]
    public async Task DecideAsync_WhenRetriesAreDisabled_SendsOnce()
    {
        var handler = new RecordingHandler(RecordingHandler.Json(HttpStatusCode.TooManyRequests, "{}"));
        using var provider = new JevProvider(TestProvider.Options(options => options.MaxRetries = 0), handler);

        await Should.ThrowAsync<JevException>(
            () => provider.DecideAsync(TestRequest.For(), CancellationToken.None));

        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task DecideAsync_WhenTheAttemptExceedsTheTimeout_ThrowsATransientFailure()
    {
        using var provider = new JevProvider(
            TestProvider.Options(options => options.Timeout = TimeSpan.FromMilliseconds(30)),
            new SlowHandler());

        var exception = await Should.ThrowAsync<JevException>(
            () => provider.DecideAsync(TestRequest.For(), CancellationToken.None));

        exception.IsTransient.ShouldBeTrue();
    }

    private sealed class SlowHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}

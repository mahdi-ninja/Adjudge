using System.Globalization;
using System.Net;

namespace Adjudge.Jev.Tests;

[Collection(SharedEnvironment.Name)]
public sealed class JevErrorTests
{
    [Theory]
    [InlineData(401, false)]
    [InlineData(422, false)]
    [InlineData(429, true)]
    [InlineData(529, true)]
    [InlineData(500, true)]
    [InlineData(503, true)]
    [InlineData(408, true)]
    [InlineData(400, false)]
    [InlineData(403, false)]
    [InlineData(404, false)]
    public async Task DecideAsync_WhenTheApiFails_ReportsTheStatusAndWhetherItIsTransient(int status, bool isTransient)
    {
        var exception = await ThrowAsync(status, """{"error":{"message":"nope"}}""");

        exception.IsTransient.ShouldBe(isTransient);
        exception.StatusCode.ShouldBe((HttpStatusCode)status);
    }

    [Fact]
    public async Task DecideAsync_WhenTheApiFails_CarriesTheProviderName()
    {
        (await ThrowAsync(500, "{}")).Provider.ShouldBe("jev");
    }

    [Fact]
    public async Task DecideAsync_WhenTheApiFails_CarriesTheRequestIdAndBody()
    {
        var exception = await ThrowAsync(422, """{"message":"bad question"}""", requestId: "req_9");

        exception.RequestId.ShouldBe("req_9");
        exception.ResponseBody.ShouldBe("""{"message":"bad question"}""");
    }

    [Fact]
    public async Task DecideAsync_WhenTheErrorBodyCarriesAMessage_IncludesItInTheMessage()
    {
        (await ThrowAsync(422, """{"error":{"message":"criteria must be ordered"}}""")).Message
            .ShouldContain("criteria must be ordered");
    }

    [Fact]
    public async Task DecideAsync_WhenTheErrorBodyIsNotJson_StillReports()
    {
        (await ThrowAsync(500, "<html>oops</html>")).Message.ShouldContain("500");
    }

    [Fact]
    public async Task DecideAsync_WhenRateLimitedWithDeltaSeconds_ParsesRetryAfter()
    {
        var exception = await ThrowAsync(429, "{}", retryAfter: "12");

        exception.RetryAfter.ShouldBe(TimeSpan.FromSeconds(12));
    }

    [Fact]
    public async Task DecideAsync_WhenRateLimitedWithAnHttpDate_ParsesRetryAfter()
    {
        var when = DateTimeOffset.UtcNow.AddMinutes(2);

        var exception = await ThrowAsync(
            429,
            "{}",
            retryAfter: when.ToString("R", CultureInfo.InvariantCulture));

        exception.RetryAfter!.Value.ShouldBeGreaterThan(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task DecideAsync_WhenRateLimitedWithoutRetryAfter_HasNoDelay()
    {
        (await ThrowAsync(429, "{}")).RetryAfter.ShouldBeNull();
    }

    [Fact]
    public async Task DecideAsync_WhenTheTransportFails_ThrowsATransientFailureWithNoStatus()
    {
        using var provider = new JevProvider(TestProvider.Options(), new FailingHandler());

        var exception = await Should.ThrowAsync<JevException>(
            () => provider.DecideAsync(TestRequest.For(), CancellationToken.None));

        exception.IsTransient.ShouldBeTrue();
        exception.StatusCode.ShouldBeNull();
        exception.InnerException.ShouldBeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task DecideAsync_WhenNoApiKeyIsConfigured_ThrowsBeforeSending()
    {
        var previous = Environment.GetEnvironmentVariable(JevOptions.ApiKeyVariable);
        Environment.SetEnvironmentVariable(JevOptions.ApiKeyVariable, null);
        var handler = new RecordingHandler();

        try
        {
            using var provider = new JevProvider(new JevOptions { MaxRetries = 0 }, handler);

            (await Should.ThrowAsync<JevException>(
                () => provider.DecideAsync(TestRequest.For(), CancellationToken.None))).IsTransient.ShouldBeFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable(JevOptions.ApiKeyVariable, previous);
        }

        handler.Requests.ShouldBeEmpty();
    }

    private static async Task<JevException> ThrowAsync(
        int status,
        string body,
        string? requestId = null,
        string? retryAfter = null)
    {
        var response = RecordingHandler.Json((HttpStatusCode)status, body, requestId);
        if (retryAfter is not null)
        {
            response.Headers.TryAddWithoutValidation("Retry-After", retryAfter);
        }

        var handler = new RecordingHandler(response);
        using var provider = TestProvider.Create(handler);

        return await Should.ThrowAsync<JevException>(
            () => provider.DecideAsync(TestRequest.For(), CancellationToken.None));
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("connection refused");
    }
}

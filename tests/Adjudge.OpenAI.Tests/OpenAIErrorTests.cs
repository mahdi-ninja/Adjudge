using System.Globalization;
using System.Net;

namespace Adjudge.OpenAI.Tests;

[Collection(SharedEnvironment.Name)]
public sealed class OpenAIErrorTests
{
    [Theory]
    [InlineData(400, false)]
    [InlineData(401, false)]
    [InlineData(403, false)]
    [InlineData(404, false)]
    [InlineData(422, false)]
    [InlineData(408, true)]
    [InlineData(429, true)]
    [InlineData(500, true)]
    [InlineData(503, true)]
    [InlineData(418, false)]
    public async Task DecideAsync_WhenTheApiFails_ReportsTheStatusAndWhetherItIsTransient(int status, bool isTransient)
    {
        var exception = await ThrowAsync(status, """{"error":{"message":"nope"}}""");

        exception.IsTransient.ShouldBe(isTransient);
        exception.StatusCode.ShouldBe((HttpStatusCode)status);
        exception.Provider.ShouldBe("openai");
    }

    [Fact]
    public async Task DecideAsync_WhenTheErrorBodyCarriesAMessage_IncludesItInTheMessage()
    {
        (await ThrowAsync(400, """{"error":{"message":"max_tokens is not supported"}}""")).Message
            .ShouldContain("max_tokens is not supported");
    }

    [Fact]
    public async Task DecideAsync_WhenTheApiFails_CarriesTheRequestIdAndBody()
    {
        var exception = await ThrowAsync(401, """{"error":{"message":"bad key"}}""", requestId: "req_9");

        exception.RequestId.ShouldBe("req_9");
        exception.ResponseBody!.ShouldContain("bad key");
    }

    [Fact]
    public async Task DecideAsync_WhenRateLimitedWithDeltaSeconds_ParsesRetryAfter()
    {
        (await ThrowAsync(429, "{}", retryAfter: "12")).RetryAfter.ShouldBe(TimeSpan.FromSeconds(12));
    }

    [Fact]
    public async Task DecideAsync_WhenRateLimitedWithAnHttpDate_ParsesRetryAfter()
    {
        var when = DateTimeOffset.UtcNow.AddMinutes(2);

        var exception = await ThrowAsync(429, "{}", retryAfter: when.ToString("R", CultureInfo.InvariantCulture));

        exception.RetryAfter!.Value.ShouldBeGreaterThan(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task DecideAsync_WhenRateLimitedWithoutRetryAfter_HasNoDelay()
    {
        (await ThrowAsync(429, "{}")).RetryAfter.ShouldBeNull();
    }

    [Fact]
    public async Task DecideAsync_WhenTheErrorBodyIsNotJson_StillReports()
    {
        (await ThrowAsync(500, "<html>oops</html>")).Message.ShouldContain("500");
    }

    [Fact]
    public async Task DecideAsync_WhenTheTransportFails_ThrowsATransientFailureWithNoStatus()
    {
        using var provider = new OpenAIProvider(TestProvider.Options(), new FailingHandler());

        var exception = await Should.ThrowAsync<OpenAIException>(
            () => provider.DecideAsync(TestRequest.For(TestRequest.Classify()), TestContext.Current.CancellationToken));

        exception.IsTransient.ShouldBeTrue();
        exception.StatusCode.ShouldBeNull();
    }

    [Fact]
    public async Task DecideAsync_WhenNoApiKeyIsConfigured_ThrowsBeforeSending()
    {
        var handler = new RecordingHandler();

        await WithVariablesAsync(async () =>
        {
            using var provider = new OpenAIProvider(new OpenAIOptions { Model = "gpt-4o-mini", MaxRetries = 0 }, handler);

            var exception = await Should.ThrowAsync<OpenAIException>(
                () => provider.DecideAsync(TestRequest.For(TestRequest.Classify()), TestContext.Current.CancellationToken));

            exception.IsTransient.ShouldBeFalse();
            exception.Message.ShouldContain(OpenAIOptions.ApiKeyVariable);
        });

        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task DecideAsync_WhenNoModelIsConfigured_ThrowsBeforeSending()
    {
        var handler = new RecordingHandler();

        await WithVariablesAsync(async () =>
        {
            using var provider = new OpenAIProvider(new OpenAIOptions { ApiKey = "test-key", MaxRetries = 0 }, handler);

            var exception = await Should.ThrowAsync<OpenAIException>(
                () => provider.DecideAsync(TestRequest.For(TestRequest.Classify()), TestContext.Current.CancellationToken));

            exception.IsTransient.ShouldBeFalse();
            exception.Message.ShouldContain(OpenAIOptions.ModelVariable);
        });

        handler.Requests.ShouldBeEmpty();
    }

    internal static async Task WithVariablesAsync(Func<Task> act)
    {
        var key = Environment.GetEnvironmentVariable(OpenAIOptions.ApiKeyVariable);
        var model = Environment.GetEnvironmentVariable(OpenAIOptions.ModelVariable);
        var baseUrl = Environment.GetEnvironmentVariable(OpenAIOptions.BaseUrlVariable);
        Environment.SetEnvironmentVariable(OpenAIOptions.ApiKeyVariable, null);
        Environment.SetEnvironmentVariable(OpenAIOptions.ModelVariable, null);
        Environment.SetEnvironmentVariable(OpenAIOptions.BaseUrlVariable, null);

        try
        {
            await act();
        }
        finally
        {
            Environment.SetEnvironmentVariable(OpenAIOptions.ApiKeyVariable, key);
            Environment.SetEnvironmentVariable(OpenAIOptions.ModelVariable, model);
            Environment.SetEnvironmentVariable(OpenAIOptions.BaseUrlVariable, baseUrl);
        }
    }

    private static async Task<OpenAIException> ThrowAsync(
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

        return await Should.ThrowAsync<OpenAIException>(
            () => provider.DecideAsync(TestRequest.For(TestRequest.Classify()), TestContext.Current.CancellationToken));
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("connection refused");
    }
}

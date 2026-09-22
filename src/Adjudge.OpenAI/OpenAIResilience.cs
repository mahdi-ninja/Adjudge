using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Timeout;

namespace Adjudge.OpenAI;

internal static class OpenAIResilience
{
    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(0.5);

    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(5);

    public static void Configure(ResiliencePipelineBuilder<HttpResponseMessage> builder, OpenAIOptions options)
    {
        if (options.MaxRetries > 0)
        {
            builder.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = options.MaxRetries,
                BackoffType = DelayBackoffType.Exponential,
                Delay = BaseDelay,
                MaxDelay = MaxDelay,
                UseJitter = true,
                ShouldRetryAfterHeader = true,
                ShouldHandle = arguments => ValueTask.FromResult(ShouldRetry(arguments.Outcome)),
            });
        }

        builder.AddTimeout(options.Timeout);
    }

    public static DelegatingHandler CreateHandler(OpenAIOptions options)
    {
        var builder = new ResiliencePipelineBuilder<HttpResponseMessage>();
        Configure(builder, options);
        return new ResilienceHandler(builder.Build());
    }

    private static bool ShouldRetry(Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is HttpRequestException or TimeoutRejectedException)
        {
            return true;
        }

        return outcome.Result is { } response && (int)response.StatusCode is 408 or 429 or >= 500;
    }
}

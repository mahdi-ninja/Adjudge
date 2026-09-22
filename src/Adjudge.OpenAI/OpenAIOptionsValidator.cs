using Microsoft.Extensions.Options;

namespace Adjudge.OpenAI;

internal sealed class OpenAIOptionsValidator : IValidateOptions<OpenAIOptions>
{
    public ValidateOptionsResult Validate(string? name, OpenAIOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = Failures(options);
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    public static List<string> Failures(OpenAIOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (options.Timeout <= TimeSpan.Zero)
        {
            failures.Add($"{nameof(OpenAIOptions.Timeout)} must be greater than zero.");
        }

        if (options.MaxRetries < 0)
        {
            failures.Add($"{nameof(OpenAIOptions.MaxRetries)} must be zero or greater.");
        }

        if (options.Samples < 2)
        {
            failures.Add($"{nameof(OpenAIOptions.Samples)} must be 2 or greater.");
        }

        if (options.SamplingTemperature < 0)
        {
            failures.Add($"{nameof(OpenAIOptions.SamplingTemperature)} must be zero or greater.");
        }

        if (options.MaxConcurrentCalls < 1)
        {
            failures.Add($"{nameof(OpenAIOptions.MaxConcurrentCalls)} must be 1 or greater.");
        }

        if (options.TopLogProbabilities is < OpenAIOptions.MinimumTopLogProbabilities or > OpenAIOptions.MaximumTopLogProbabilities)
        {
            failures.Add(
                $"{nameof(OpenAIOptions.TopLogProbabilities)} must be between {OpenAIOptions.MinimumTopLogProbabilities} " +
                $"and {OpenAIOptions.MaximumTopLogProbabilities}.");
        }

        if (options.BaseUrl is { IsAbsoluteUri: false })
        {
            failures.Add($"{nameof(OpenAIOptions.BaseUrl)} must be an absolute URL.");
        }

        return failures;
    }
}

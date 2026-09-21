using Microsoft.Extensions.Options;

namespace Adjudge.Jev;

internal sealed class JevOptionsValidator : IValidateOptions<JevOptions>
{
    public ValidateOptionsResult Validate(string? name, JevOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (options.Timeout <= TimeSpan.Zero)
        {
            failures.Add($"{nameof(JevOptions.Timeout)} must be greater than zero.");
        }

        if (options.MaxRetries < 0)
        {
            failures.Add($"{nameof(JevOptions.MaxRetries)} must be zero or greater.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

namespace Adjudge;

/// <summary>
/// The base for failures that came from a provider rather than from the library. Provider packages
/// derive their own exceptions from it.
/// </summary>
public class ProviderException : AdjudgeException
{
    /// <summary>Creates the exception with a default message supplied by the runtime.</summary>
    public ProviderException()
    {
    }

    /// <summary>Creates the exception with the given message.</summary>
    public ProviderException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with the given message, wrapping the failure that caused it.</summary>
    public ProviderException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates an attributed exception, which is the form provider packages are expected to use.</summary>
    /// <param name="message">The failure description.</param>
    /// <param name="provider">The name of the provider that failed.</param>
    /// <param name="isTransient">Whether retrying the same request could plausibly succeed.</param>
    /// <param name="innerException">The failure that caused this one, when there is one.</param>
    public ProviderException(string message, string provider, bool isTransient, Exception? innerException = null)
        : base(message, innerException)
    {
        Provider = provider;
        IsTransient = isTransient;
    }

    /// <summary>The name of the provider that failed, when the failure could be attributed to one.</summary>
    public string? Provider { get; }

    /// <summary>Whether retrying the same request could plausibly succeed.</summary>
    public bool IsTransient { get; }
}

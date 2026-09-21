namespace Adjudge;

/// <summary>
/// Thrown when a provider answered but the answer could not be mapped, for instance an unknown option
/// key, a missing question, or probabilities that do not sum to roughly 1.
/// </summary>
public sealed class ProviderResponseException : AdjudgeException
{
    /// <summary>Creates the exception with a default message supplied by the runtime.</summary>
    public ProviderResponseException()
    {
    }

    /// <summary>Creates the exception with the given message.</summary>
    public ProviderResponseException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with the given message, wrapping the failure that caused it.</summary>
    public ProviderResponseException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Creates the exception attributed to the provider that produced the response.</summary>
    public ProviderResponseException(string message, string provider, Exception? innerException = null)
        : base(message, innerException)
    {
        Provider = provider;
    }

    /// <summary>The provider that produced the response, when the failure could be attributed to one.</summary>
    public string? Provider { get; }
}

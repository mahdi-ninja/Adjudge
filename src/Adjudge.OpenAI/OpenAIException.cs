using System.Net;

namespace Adjudge.OpenAI;

/// <summary>Every failure the OpenAI provider reports, carrying the HTTP detail behind it.</summary>
public sealed class OpenAIException : ProviderException
{
    /// <summary>The name this provider reports, and the one carried on its exceptions.</summary>
    public const string ProviderName = "openai";

    /// <summary>Creates a non-transient failure with a default message naming the provider.</summary>
    public OpenAIException()
        : base("The OpenAI provider failed.", ProviderName, isTransient: false)
    {
    }

    /// <summary>Creates a non-transient failure with the given message.</summary>
    public OpenAIException(string message)
        : base(message, ProviderName, isTransient: false)
    {
    }

    /// <summary>Creates a non-transient failure with the given message, wrapping the failure that caused it.</summary>
    public OpenAIException(string message, Exception? innerException)
        : base(message, ProviderName, isTransient: false, innerException)
    {
    }

    /// <summary>Creates a failure with the full HTTP detail, which is the form the provider itself raises.</summary>
    /// <param name="message">The failure description.</param>
    /// <param name="isTransient">Whether retrying the same request could plausibly succeed.</param>
    /// <param name="statusCode">The status the service returned, or null when no response arrived.</param>
    /// <param name="retryAfter">How long the service asked the caller to wait, when it said so.</param>
    /// <param name="requestId">The service's own request identifier, when it sent one.</param>
    /// <param name="responseBody">The raw error body, when there was one.</param>
    /// <param name="innerException">The failure that caused this one, when there is one.</param>
    public OpenAIException(
        string message,
        bool isTransient,
        HttpStatusCode? statusCode = null,
        TimeSpan? retryAfter = null,
        string? requestId = null,
        string? responseBody = null,
        Exception? innerException = null)
        : base(message, ProviderName, isTransient, innerException)
    {
        StatusCode = statusCode;
        RetryAfter = retryAfter;
        RequestId = requestId;
        ResponseBody = responseBody;
    }

    /// <summary>The status the service returned, or null when the request never produced a response.</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>How long the server asked the caller to wait, when it sent a Retry-After header.</summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>The service's own request identifier, taken from <c>x-request-id</c> when it sent one.</summary>
    public string? RequestId { get; }

    /// <summary>The raw error body, kept unparsed for diagnostics.</summary>
    public string? ResponseBody { get; }
}

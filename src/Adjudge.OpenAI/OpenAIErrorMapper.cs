using System.ClientModel;
using System.ClientModel.Primitives;
using System.Globalization;
using System.Net;
using System.Text.Json;

namespace Adjudge.OpenAI;

internal static class OpenAIErrorMapper
{
    public const string RequestIdHeader = "x-request-id";

    private static readonly string[] MessageKeys = ["message", "detail", "error"];

    public static OpenAIException Map(ClientResultException exception)
    {
        var response = exception.GetRawResponse();
        if (response is null)
        {
            return new OpenAIException(
                "The OpenAI request could not be completed.",
                isTransient: true,
                innerException: exception);
        }

        var status = (HttpStatusCode)response.Status;
        var body = Body(response);
        var requestId = Header(response, RequestIdHeader);
        var message = Describe(status, body);

        return response.Status switch
        {
            429 => new OpenAIException(message, isTransient: true, status, RetryAfter(response), requestId, body, exception),
            408 or >= 500 => new OpenAIException(message, isTransient: true, status, requestId: requestId, responseBody: body, innerException: exception),
            _ => new OpenAIException(message, isTransient: false, status, requestId: requestId, responseBody: body, innerException: exception),
        };
    }

    private static TimeSpan? RetryAfter(PipelineResponse response)
    {
        var header = Header(response, "Retry-After");
        if (header is null)
        {
            return null;
        }

        if (double.TryParse(header, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            return TimeSpan.FromSeconds(seconds);
        }

        if (DateTimeOffset.TryParse(header, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
        {
            var remaining = date - DateTimeOffset.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        return null;
    }

    private static string? Header(PipelineResponse response, string name) =>
        response.Headers.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static string Body(PipelineResponse response)
    {
        try
        {
            return response.Content.ToString();
        }
        catch (InvalidOperationException)
        {
            return string.Empty;
        }
    }

    private static string Describe(HttpStatusCode status, string body)
    {
        var detail = Detail(body);
        var prefix = $"The OpenAI request failed with status {(int)status} ({status}).";
        return detail is null ? prefix : $"{prefix} {detail}";
    }

    private static string? Detail(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (document.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                return Text(error);
            }

            return Text(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement element)
    {
        foreach (var name in MessageKeys)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }
}

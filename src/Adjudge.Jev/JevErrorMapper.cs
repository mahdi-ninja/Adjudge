using System.Net;
using System.Text.Json;

namespace Adjudge.Jev;

internal static class JevErrorMapper
{
    private static readonly string[] MessageKeys = ["message", "detail", "error"];

    public static JevException Map(HttpResponseMessage response, string body)
    {
        var status = response.StatusCode;
        var requestId = JevHeaders.RequestId(response);
        var message = Describe(status, body);

        return (int)status switch
        {
            429 => new JevException(message, isTransient: true, status, RetryAfter(response), requestId, body),
            408 or 529 or >= 500 => new JevException(message, isTransient: true, status, requestId: requestId, responseBody: body),
            _ => new JevException(message, isTransient: false, status, requestId: requestId, responseBody: body),
        };
    }

    public static TimeSpan? RetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header is null)
        {
            return null;
        }

        if (header.Delta is { } delta)
        {
            return delta;
        }

        if (header.Date is { } date)
        {
            var remaining = date - DateTimeOffset.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        return null;
    }

    private static string Describe(HttpStatusCode status, string body)
    {
        var detail = Detail(body);
        var prefix = $"The Jev request failed with status {(int)status} ({status}).";
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

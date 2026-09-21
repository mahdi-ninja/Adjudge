namespace Adjudge.Jev;

internal static class JevHeaders
{
    public const string RequestIdHeader = "x-typesafe-request-id";

    public static string? RequestId(HttpResponseMessage response) =>
        response.Headers.TryGetValues(RequestIdHeader, out var values) ? values.FirstOrDefault() : null;
}

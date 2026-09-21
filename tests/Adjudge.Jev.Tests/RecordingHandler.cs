using System.Net;

namespace Adjudge.Jev.Tests;

internal sealed class RecordingHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses;

    public RecordingHandler(params HttpResponseMessage[] responses) =>
        _responses = new Queue<HttpResponseMessage>(responses);

    public List<HttpRequestMessage> Requests { get; } = [];

    public List<string> Bodies { get; } = [];

    public static HttpResponseMessage Json(HttpStatusCode status, string body, string? requestId = null)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
        };

        if (requestId is not null)
        {
            response.Headers.TryAddWithoutValidation(JevHeaders.RequestIdHeader, requestId);
        }

        return response;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("The handler ran out of scripted responses.");
        }

        return _responses.Dequeue();
    }
}

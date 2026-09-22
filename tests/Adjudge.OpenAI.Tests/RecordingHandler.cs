using System.Net;
using System.Text;

namespace Adjudge.OpenAI.Tests;

internal sealed class RecordingHandler : HttpMessageHandler
{
    private readonly Lock _gate = new();
    private readonly Queue<HttpResponseMessage> _responses;
    private readonly Func<int, HttpResponseMessage>? _factory;
    private readonly Func<string, HttpResponseMessage>? _byBody;
    private readonly TimeSpan _delay;
    private int _inFlight;

    public RecordingHandler(params HttpResponseMessage[] responses) =>
        _responses = new Queue<HttpResponseMessage>(responses);

    public RecordingHandler(Func<int, HttpResponseMessage> factory, TimeSpan delay = default)
    {
        _responses = new Queue<HttpResponseMessage>();
        _factory = factory;
        _delay = delay;
    }

    private RecordingHandler(Func<string, HttpResponseMessage> byBody, TimeSpan delay)
    {
        _responses = new Queue<HttpResponseMessage>();
        _byBody = byBody;
        _delay = delay;
    }

    // Answers by what was asked rather than by arrival order, so a test stays honest when the calls
    // race each other.
    public static RecordingHandler ForBodies(Func<string, HttpResponseMessage> byBody, TimeSpan delay = default) =>
        new(byBody, delay);

    public List<HttpRequestMessage> Requests { get; } = [];

    public List<string> Bodies { get; } = [];

    public int MaxInFlight { get; private set; }

    public static HttpResponseMessage Json(HttpStatusCode status, string body, string? requestId = null)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        if (requestId is not null)
        {
            response.Headers.TryAddWithoutValidation(OpenAIErrorMapper.RequestIdHeader, requestId);
        }

        return response;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        int index;

        lock (_gate)
        {
            Requests.Add(request);
            Bodies.Add(body);
            index = Requests.Count - 1;
            _inFlight++;
            MaxInFlight = Math.Max(MaxInFlight, _inFlight);
        }

        try
        {
            if (_delay > TimeSpan.Zero)
            {
                await Task.Delay(_delay, cancellationToken);
            }

            if (_byBody is not null)
            {
                return _byBody(body);
            }

            if (_factory is not null)
            {
                return _factory(index);
            }

            lock (_gate)
            {
                if (_responses.Count == 0)
                {
                    throw new InvalidOperationException("The handler ran out of scripted responses.");
                }

                return _responses.Dequeue();
            }
        }
        finally
        {
            lock (_gate)
            {
                _inFlight--;
            }
        }
    }
}

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Adjudge.Providers;
using Microsoft.Extensions.Options;
using Polly.Timeout;

namespace Adjudge.Jev;

/// <summary>The Jev provider. Safe to share across evaluations, and meant to be long-lived.</summary>
public sealed class JevProvider : IDecisionProvider, IDisposable
{
    private readonly HttpClient _client;
    private readonly JevOptions _options;
    private readonly Uri _endpoint;
    private readonly bool _ownsClient;

    /// <summary>Creates a provider over a client the caller owns, which is the form dependency injection uses.</summary>
    /// <param name="client">Its own timeout should be infinite, because the provider applies <see cref="JevOptions.Timeout"/> per attempt.</param>
    /// <param name="options">The configuration, read once at construction.</param>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public JevProvider(HttpClient client, IOptions<JevOptions> options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);

        _client = client;
        _options = options.Value;
        _endpoint = Endpoint(_options.ResolveBaseUrl());
    }

    /// <summary>Creates a provider that owns a single <see cref="HttpClient"/>, so it should be long-lived.</summary>
    public JevProvider(JevOptions options)
        : this(options, new HttpClientHandler())
    {
    }

    internal JevProvider(JevOptions options, HttpMessageHandler innerHandler)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(innerHandler);

        var handler = JevResilience.CreateHandler(options);
        handler.InnerHandler = innerHandler;

        _options = options;
        _endpoint = Endpoint(options.ResolveBaseUrl());
        _client = new HttpClient(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
        _ownsClient = true;
    }

    /// <summary>Always <c>jev</c>, which is what results and telemetry report.</summary>
    public string Name => JevException.ProviderName;

    /// <summary>Every question kind, plus native confidence and batching.</summary>
    public DecisionCapabilities Capabilities =>
        DecisionCapabilities.Classify |
        DecisionCapabilities.Rate |
        DecisionCapabilities.Assert |
        DecisionCapabilities.NativeConfidence |
        DecisionCapabilities.Batch;

    /// <summary>Sends every question in one call and maps the reply back.</summary>
    /// <exception cref="JevException">No API key was configured, or the call failed.</exception>
    /// <exception cref="ProviderResponseException">The reply could not be mapped.</exception>
    public async Task<ProviderResponse> DecideAsync(ProviderRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var apiKey = _options.ResolveApiKey() ?? throw new JevException(
            $"No Jev API key was configured. Set {nameof(JevOptions)}.{nameof(JevOptions.ApiKey)} " +
            $"or the {JevOptions.ApiKeyVariable} environment variable.");

        var payload = JevRequestMapper.Map(request, _options);
        var json = JsonSerializer.Serialize(payload, JevJsonContext.Default.JevRequestPayload);

        using var message = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await SendAsync(message, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw JevErrorMapper.Map(response, body);
        }

        JevResponsePayload? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize(body, JevJsonContext.Default.JevResponsePayload);
        }
        catch (JsonException exception)
        {
            throw new ProviderResponseException("The Jev response was not valid JSON.", JevException.ProviderName, exception);
        }

        if (parsed is null)
        {
            throw new ProviderResponseException("The Jev response was empty.", JevException.ProviderName);
        }

        return JevResponseMapper.Map(request, parsed, JevHeaders.RequestId(response));
    }

    /// <summary>Disposes the client only when this provider created it.</summary>
    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }

    private static Uri Endpoint(Uri baseUrl) => new($"{baseUrl.ToString().TrimEnd('/')}/v1/systemone");

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken ct)
    {
        try
        {
            return await _client.SendAsync(message, ct).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or TimeoutRejectedException)
        {
            throw new JevException("The Jev request could not be completed.", isTransient: true, innerException: exception);
        }
        catch (OperationCanceledException exception) when (!ct.IsCancellationRequested)
        {
            throw new JevException("The Jev request timed out.", isTransient: true, innerException: exception);
        }
    }
}

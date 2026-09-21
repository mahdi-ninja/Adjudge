using Adjudge.Providers;

namespace Adjudge.Tests.Core;

public sealed class StubProvider : IDecisionProvider
{
    private readonly List<ProviderRequest> _requests = [];

    public string Name { get; set; } = "stub";

    public DecisionCapabilities Capabilities { get; set; } =
        DecisionCapabilities.Classify | DecisionCapabilities.Rate | DecisionCapabilities.Assert | DecisionCapabilities.NativeConfidence;

    public ProviderResponse Response { get; set; } = new(new Dictionary<string, AnswerSpec>());

    public Exception? Throws { get; set; }

    public Action? OnDecide { get; set; }

    public IReadOnlyList<ProviderRequest> Requests => _requests;

    public CancellationToken LastToken { get; private set; }

    public Task<ProviderResponse> DecideAsync(ProviderRequest request, CancellationToken ct)
    {
        _requests.Add(request);
        LastToken = ct;
        OnDecide?.Invoke();

        return Throws is null ? Task.FromResult(Response) : Task.FromException<ProviderResponse>(Throws);
    }
}

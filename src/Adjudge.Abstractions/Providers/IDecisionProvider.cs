namespace Adjudge.Providers;

/// <summary>The extension seam. A provider translates shapes and nothing else; typing, confidence and validation stay in the engine.</summary>
public interface IDecisionProvider
{
    /// <summary>The name reported on results and telemetry, such as <c>jev</c>.</summary>
    string Name { get; }

    /// <summary>What this provider can serve, checked against each definition before the first call.</summary>
    DecisionCapabilities Capabilities { get; }

    /// <summary>Answers every question in the request. Implementations are called concurrently, so they must be thread-safe.</summary>
    /// <exception cref="ProviderException">The provider failed to produce a response.</exception>
    Task<ProviderResponse> DecideAsync(ProviderRequest request, CancellationToken ct);
}

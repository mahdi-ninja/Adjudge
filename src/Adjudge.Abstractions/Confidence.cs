namespace Adjudge;

/// <summary>
/// How sure an answer is, on a scale where 0 is an even spread over the options and 1 puts all the
/// mass on one. The library computes it the same way for every provider.
/// </summary>
/// <param name="Value">The library's own figure, from 0 to 1.</param>
/// <param name="Source">Where the provider's reported figure came from, when it sent one.</param>
/// <param name="ProviderReported">The provider's own confidence, kept as reported.</param>
public readonly record struct Confidence(double Value, ConfidenceSource Source, double? ProviderReported = null)
{
    /// <summary>The provider's own confidence, kept as reported and never used to compute <see cref="Value"/>.</summary>
    public double? ProviderReported { get; init; } = ProviderReported;
}

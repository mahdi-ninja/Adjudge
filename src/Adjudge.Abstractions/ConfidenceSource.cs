namespace Adjudge;

/// <summary>How the provider arrived at the confidence it reported, if it reported one at all.</summary>
public enum ConfidenceSource
{
    /// <summary>The library set this because the provider reported nothing, so only the figure derived from the probabilities is available.</summary>
    Derived,

    /// <summary>The provider set this because it supplied a calibrated confidence of its own.</summary>
    Native,

    /// <summary>The provider set this because it built the distribution by sampling the model repeatedly.</summary>
    Sampled,

    /// <summary>The provider set this because it used a proxy, such as token probabilities or fixed rules, rather than a calibrated figure.</summary>
    Heuristic,
}

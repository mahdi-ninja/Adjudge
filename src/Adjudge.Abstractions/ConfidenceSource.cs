namespace Adjudge;

/// <summary>How the provider arrived at the confidence it reported, if it reported one at all.</summary>
public enum ConfidenceSource
{
    /// <summary>The provider reported nothing, so only the library's figure is available.</summary>
    Derived,

    /// <summary>The provider reported a figure of its own.</summary>
    Native,

    /// <summary>The provider's figure came from repeated sampling.</summary>
    Sampled,

    /// <summary>The provider's figure came from a rule of thumb rather than the model.</summary>
    Heuristic,
}

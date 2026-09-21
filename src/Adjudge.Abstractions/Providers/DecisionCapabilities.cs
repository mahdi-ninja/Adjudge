namespace Adjudge.Providers;

/// <summary>What a provider can do. The engine checks a definition against this before it calls, and refuses a definition the provider cannot serve.</summary>
[Flags]
public enum DecisionCapabilities
{
    /// <summary>Nothing is supported, which is the safe default for a partially built provider.</summary>
    None = 0,

    /// <summary>Pick-one questions are supported.</summary>
    Classify = 1,

    /// <summary>Ordered-level questions are supported.</summary>
    Rate = 2,

    /// <summary>Propositions are supported.</summary>
    Assert = 4,

    /// <summary>The provider reports a confidence of its own alongside the distribution.</summary>
    NativeConfidence = 8,

    /// <summary>Several questions can be answered in one call rather than one call each.</summary>
    Batch = 16,
}

namespace Adjudge.Providers;

/// <summary>A provider's answer to a rating, as mass over the level keys it was given.</summary>
/// <param name="Name">The question name this answers, matching the name the request carried.</param>
/// <param name="Probabilities">Mass per level key, which should sum to roughly 1; the engine normalises small drift and rejects the rest.</param>
/// <param name="Confidence">The provider's own confidence, when it reports one. The library computes its own regardless.</param>
/// <param name="Source">Where the reported confidence came from, when the provider wants to say. Left null, the engine infers it from <paramref name="Confidence"/>.</param>
public sealed record RateAnswerSpec(
    string Name,
    IReadOnlyDictionary<string, double> Probabilities,
    double? Confidence = null,
    ConfidenceSource? Source = null)
    : AnswerSpec(Name);

namespace Adjudge.Providers;

/// <summary>A place-on-a-scale question put to a provider.</summary>
/// <param name="Name">The question name, in camelCase, which the answer has to come back under.</param>
/// <param name="Instructions">The question itself, in plain words.</param>
/// <param name="Levels">The levels from lowest to highest. Position carries the meaning, so the order is significant.</param>
public sealed record RateSpec(string Name, string Instructions, IReadOnlyList<LevelSpec> Levels)
    : QuestionSpec(Name, Instructions);

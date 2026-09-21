namespace Adjudge.Providers;

/// <summary>A yes or no proposition put to a provider.</summary>
/// <param name="Name">The question name, in camelCase, which the answer has to come back under.</param>
/// <param name="Instructions">The question itself, in plain words.</param>
/// <param name="TrueMeans">What counts as a yes, when the definition spelled it out.</param>
/// <param name="FalseMeans">What counts as a no, when the definition spelled it out.</param>
public sealed record AssertSpec(string Name, string Instructions, string? TrueMeans = null, string? FalseMeans = null)
    : QuestionSpec(Name, Instructions);

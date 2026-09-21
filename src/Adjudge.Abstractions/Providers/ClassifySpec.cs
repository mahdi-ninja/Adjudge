namespace Adjudge.Providers;

/// <summary>A pick-one question put to a provider.</summary>
/// <param name="Name">The question name, in camelCase, which the answer has to come back under.</param>
/// <param name="Instructions">The question itself, in plain words.</param>
/// <param name="Options">The options in ascending enum order, which is the order a provider should present them in.</param>
public sealed record ClassifySpec(string Name, string Instructions, IReadOnlyList<OptionSpec> Options)
    : QuestionSpec(Name, Instructions);

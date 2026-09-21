namespace Adjudge.Providers;

/// <summary>A provider's answer to a proposition.</summary>
/// <param name="Name">The question name this answers, matching the name the request carried.</param>
/// <param name="Probability">The mass on the proposition holding, in the range 0 to 1.</param>
public sealed record AssertAnswerSpec(string Name, double Probability) : AnswerSpec(Name);

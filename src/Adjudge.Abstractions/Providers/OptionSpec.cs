namespace Adjudge.Providers;

/// <summary>One option of a classification question.</summary>
/// <param name="Key">The enum member name as declared, which the answer has to use.</param>
/// <param name="Rubric">What the option covers. Always a <see cref="string"/> or an <see cref="OptionRubric"/>.</param>
public sealed record OptionSpec(string Key, object Rubric);

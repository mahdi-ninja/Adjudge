namespace Adjudge.Providers;

/// <summary>One level of a rating question. Position in the list is the level's order, so the list must not be reordered.</summary>
/// <param name="Key">The enum member name as declared, which the answer has to use.</param>
/// <param name="Rubric">What the context has to look like to sit here. Always a <see cref="string"/> or an <see cref="OptionRubric"/>.</param>
public sealed record LevelSpec(string Key, object Rubric);

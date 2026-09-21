namespace Adjudge;

/// <summary>
/// A structured description of one option, which providers render when they present the closed set.
/// The empty parts are left out of the wire shape.
/// </summary>
/// <param name="Description">What the option covers.</param>
/// <param name="NotFor">What the option is commonly mistaken for.</param>
/// <param name="Examples">Short illustrations that clearly belong to this option.</param>
public sealed record OptionRubric(string Description, string? NotFor = null, IReadOnlyList<string>? Examples = null);

using System.Text.Json;

namespace Adjudge;

/// <summary>Turns one context object into the JSON the provider is sent, without the engine having to reflect over its type.</summary>
/// <param name="context">The caller's facts, never null.</param>
/// <returns>The serialised form the provider reads.</returns>
public delegate JsonElement SerialiseContext(object context);

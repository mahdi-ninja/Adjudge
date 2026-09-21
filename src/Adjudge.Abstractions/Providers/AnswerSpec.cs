namespace Adjudge.Providers;

/// <summary>The untyped answer a provider hands back for one question. The engine turns it into a typed value.</summary>
/// <param name="Name">The question name this answers, matching the name the request carried.</param>
public abstract record AnswerSpec(string Name);

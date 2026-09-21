namespace Adjudge.Providers;

/// <summary>One question in provider-facing form: structured, but untyped. Typing happens in the engine.</summary>
/// <param name="Name">The question name, in camelCase, which the answer has to come back under.</param>
/// <param name="Instructions">The question itself, in plain words.</param>
public abstract record QuestionSpec(string Name, string Instructions);

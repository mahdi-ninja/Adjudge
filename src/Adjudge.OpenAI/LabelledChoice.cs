namespace Adjudge.OpenAI;

/// <summary>One offered answer: the single-token label the model replies with, and the key it maps back to.</summary>
internal sealed record LabelledChoice(string Label, string Key);

using Adjudge.Providers;

namespace Adjudge.OpenAI;

internal sealed record QuestionPlan(QuestionSpec Question, string UserMessage, IReadOnlyList<LabelledChoice> Labels);

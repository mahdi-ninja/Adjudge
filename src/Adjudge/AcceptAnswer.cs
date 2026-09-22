namespace Adjudge.Providers;

/// <summary>Whether an answer from a cascade stage is good enough to stand, so the question is never put to a later stage.</summary>
/// <param name="question">The question as it was put to the stage, which carries the option or level keys.</param>
/// <param name="answer">The answer the stage gave for that question.</param>
/// <returns>True to keep the answer, false to carry the question on to the next stage.</returns>
public delegate bool AcceptAnswer(QuestionSpec question, AnswerSpec answer);

namespace Adjudge.Providers;

/// <summary>Answers one question from the caller's own predicates, or returns null to decline it.</summary>
/// <typeparam name="TContext">The context type the rules were written against.</typeparam>
/// <param name="context">The caller's context, already checked to be a <typeparamref name="TContext"/>.</param>
/// <param name="question">The question as the engine specified it, which carries the keys an answer may use.</param>
/// <returns>The answer, or null when the rules do not settle the question.</returns>
internal delegate AnswerSpec? RulesRule<in TContext>(TContext context, QuestionSpec question);

using System.Globalization;
using Adjudge.Providers;

namespace Adjudge.Jev;

internal static class JevResponseMapper
{
    public static ProviderResponse Map(ProviderRequest request, JevResponsePayload payload, string? requestId)
    {
        var answers = new Dictionary<string, AnswerSpec>(request.Questions.Count, StringComparer.Ordinal);

        foreach (var question in request.Questions)
        {
            if (payload.Answers is null || !payload.Answers.TryGetValue(question.Name, out var answer) || answer is null)
            {
                throw Invalid($"The response contains no answer for question '{question.Name}'.");
            }

            answers[question.Name] = Map(question, answer);
        }

        var metadata = requestId is null
            ? null
            : new Dictionary<string, string>(StringComparer.Ordinal) { ["request_id"] = requestId };

        var usage = payload.Usage is null ? null : new Usage(payload.Usage.InputTokens, payload.Usage.OutputTokens);

        return new ProviderResponse(answers, payload.Model, usage, metadata);
    }

    private static AnswerSpec Map(QuestionSpec question, JevAnswerPayload answer) => answer.Type switch
    {
        "choice" => new ClassifyAnswerSpec(question.Name, Probabilities(question, answer), answer.Confidence),
        "score" => new RateAnswerSpec(question.Name, ScoreProbabilities(question, answer), answer.Confidence),
        "noul" => new AssertAnswerSpec(
            question.Name,
            answer.Noul ?? throw Invalid($"The answer for question '{question.Name}' carries no noul value.")),
        _ => throw Invalid($"The answer for question '{question.Name}' has unknown type '{answer.Type}'."),
    };

    private static Dictionary<string, double> Probabilities(QuestionSpec question, JevAnswerPayload answer) =>
        answer.Probabilities ?? throw Invalid($"The answer for question '{question.Name}' carries no probabilities.");

    private static Dictionary<string, double> ScoreProbabilities(QuestionSpec question, JevAnswerPayload answer)
    {
        if (question is not RateSpec rate)
        {
            throw Invalid($"Question '{question.Name}' was not asked as a score but was answered as one.");
        }

        var probabilities = Probabilities(question, answer);
        var mapped = new Dictionary<string, double>(probabilities.Count, StringComparer.Ordinal);

        // Score probabilities are keyed by the level's position in the request, not by its key.
        foreach (var (key, value) in probabilities)
        {
            if (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var position) ||
                position < 0 || position >= rate.Levels.Count)
            {
                throw Invalid($"The answer for question '{question.Name}' refers to level position '{key}', which was not requested.");
            }

            if (!mapped.TryAdd(rate.Levels[position].Key, value))
            {
                throw Invalid($"The answer for question '{question.Name}' reports level position '{key}' more than once.");
            }
        }

        return mapped;
    }

    private static ProviderResponseException Invalid(string message) =>
        new(message, JevException.ProviderName);
}

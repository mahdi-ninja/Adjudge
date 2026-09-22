using Adjudge.Providers;
using OpenAI.Chat;

namespace Adjudge.OpenAI;

internal static class OpenAIAnswerMapper
{
    private const double MinimumAssertion = 0.001;

    private const double MaximumAssertion = 0.999;

    private static readonly char[] Separators =
        [' ', '\t', '\r', '\n', ',', ';', ':', '.', '!', '?', '"', '\'', '`', '*', '_', '(', ')', '[', ']', '{', '}', '<', '>', '/', '\\', '|', '-'];

    public static AnswerSpec FromLogProbabilities(QuestionPlan plan, ChatCompletion completion)
    {
        var tokens = completion.ContentTokenLogProbabilities;
        if (tokens.Count == 0)
        {
            throw Invalid(plan, Text(completion), NoTokensReason(completion));
        }

        // Some endpoints emit a leading newline or space before the label, so the first token that
        // carries anything is the answer token.
        var answer = tokens.FirstOrDefault(token => !string.IsNullOrWhiteSpace(token.Token));
        if (answer is null)
        {
            throw Invalid(plan, Text(completion), "every content token the response carried was whitespace");
        }

        var weights = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var candidate in answer.TopLogProbabilities)
        {
            Accumulate(plan, weights, candidate.Token, candidate.LogProbability);
        }

        Accumulate(plan, weights, answer.Token, answer.LogProbability, onlyWhenMissing: true);

        if (weights.Count == 0)
        {
            throw Invalid(plan, Text(completion), "no top log probability matched a label that was offered");
        }

        return ToAnswer(plan, weights, assertionMasses: weights, ConfidenceSource.Heuristic);
    }

    public static AnswerSpec FromSamples(QuestionPlan plan, IReadOnlyList<ChatCompletion> completions)
    {
        var counts = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var completion in completions)
        {
            if (MatchSample(plan, Text(completion)) is { } label)
            {
                counts[label] = counts.GetValueOrDefault(label) + 1;
            }
        }

        if (counts.Count == 0)
        {
            var sampled = string.Join(" | ", completions.Select(Text));
            throw Invalid(plan, sampled, "no sample carried a label that was offered");
        }

        var shares = Normalise(counts);
        return ToAnswer(plan, counts, assertionMasses: shares, ConfidenceSource.Sampled);
    }

    private static string NoTokensReason(ChatCompletion completion) =>
        completion.FinishReason == ChatFinishReason.Length
            ? "the token budget was consumed before the model produced an answer, which a reasoning model does that " +
              "spends its budget on hidden reasoning tokens. Use ConfidenceStrategy.Sampling, or a model that answers " +
              "with a plain first token"
            : "the response carried no log probabilities";

    private static string Text(ChatCompletion completion) =>
        completion.Content.Count == 0 ? string.Empty : completion.Content[0].Text ?? string.Empty;

    private static void Accumulate(
        QuestionPlan plan,
        Dictionary<string, double> weights,
        string token,
        float logProbability,
        bool onlyWhenMissing = false)
    {
        if (MatchToken(plan, token) is not { } label || (onlyWhenMissing && weights.ContainsKey(label)))
        {
            return;
        }

        weights[label] = weights.GetValueOrDefault(label) + Math.Exp(logProbability);
    }

    private static string? MatchToken(QuestionPlan plan, string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : Offered(plan, Leading(text));

    private static string? MatchSample(QuestionPlan plan, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        // A model that ignores the instruction still tends to put the label in a word of its own, so the
        // first word that is a label wins rather than only the very first word.
        foreach (var word in text.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Offered(plan, Leading(word)) is { } label)
            {
                return label;
            }
        }

        return null;
    }

    private static string? Offered(QuestionPlan plan, string candidate)
    {
        if (candidate.Length == 0)
        {
            return null;
        }

        foreach (var label in plan.Labels)
        {
            if (string.Equals(candidate, label.Label, StringComparison.OrdinalIgnoreCase))
            {
                return label.Label;
            }
        }

        return null;
    }

    // Models decorate labels: "**A**", "- A", "\"yes\"", "`A`", "(A)". Everything before the first
    // letter or digit is dropped, and the run of letters and digits that follows is the candidate.
    private static string Leading(string text)
    {
        var span = text.AsSpan().Trim();
        var start = 0;
        while (start < span.Length && !char.IsLetterOrDigit(span[start]))
        {
            start++;
        }

        var end = start;
        while (end < span.Length && char.IsLetterOrDigit(span[end]))
        {
            end++;
        }

        return span[start..end].ToString();
    }

    private static Dictionary<string, double> Normalise(Dictionary<string, double> weights)
    {
        var total = weights.Values.Sum();
        if (total <= 0)
        {
            return weights;
        }

        var normalised = new Dictionary<string, double>(weights.Count, StringComparer.Ordinal);
        foreach (var (label, weight) in weights)
        {
            normalised[label] = weight / total;
        }

        return normalised;
    }

    private static AnswerSpec ToAnswer(
        QuestionPlan plan,
        Dictionary<string, double> weights,
        Dictionary<string, double> assertionMasses,
        ConfidenceSource source)
    {
        if (plan.Question is AssertSpec)
        {
            return Assertion(plan, assertionMasses);
        }

        var byLabel = Normalise(weights);
        var probabilities = new Dictionary<string, double>(byLabel.Count, StringComparer.Ordinal);
        foreach (var label in plan.Labels)
        {
            if (byLabel.TryGetValue(label.Label, out var probability))
            {
                probabilities[label.Key] = probability;
            }
        }

        return plan.Question is RateSpec
            ? new RateAnswerSpec(plan.Question.Name, probabilities, Confidence: null, source)
            : new ClassifyAnswerSpec(plan.Question.Name, probabilities, Confidence: null, source);
    }

    private static AssertAnswerSpec Assertion(QuestionPlan plan, Dictionary<string, double> masses)
    {
        var hasYes = masses.TryGetValue(OpenAIPromptBuilder.TrueLabel, out var yes);
        var hasNo = masses.TryGetValue(OpenAIPromptBuilder.FalseLabel, out var no);

        // With only one of the two answers in hand, the mass that is missing sits somewhere in the rest
        // of the distribution, so the residual stands in for it rather than the answer saturating at 1.
        if (hasYes && !hasNo)
        {
            no = Math.Max(0, 1 - yes);
        }
        else if (hasNo && !hasYes)
        {
            yes = Math.Max(0, 1 - no);
        }

        var total = yes + no;
        var probability = total <= 0 ? 0.5 : yes / total;

        return new AssertAnswerSpec(plan.Question.Name, Math.Clamp(probability, MinimumAssertion, MaximumAssertion));
    }

    private static ProviderResponseException Invalid(QuestionPlan plan, string content, string reason) =>
        new(
            $"The OpenAI answer for question '{plan.Question.Name}' could not be mapped because {reason}. The model replied with '{content}'.",
            OpenAIException.ProviderName);
}

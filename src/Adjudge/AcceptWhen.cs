namespace Adjudge.Providers;

/// <summary>Ready-made acceptance rules for a <see cref="CascadeStage"/>.</summary>
public static class AcceptWhen
{
    /// <summary>Accepts every answer, which is what a stage does when it is given no rule of its own.</summary>
    public static AcceptAnswer Always { get; } = static (_, _) => true;

    /// <summary>
    /// Accepts an answer whose confidence, computed with the library's own formula over the answer's
    /// probabilities, is at least <paramref name="floor"/>. The option or level count comes from the
    /// question, so keys the answer left out count as zero mass. A proposition is read as a two-way
    /// choice whose top mass is the larger of the probability and its complement, so a probability of
    /// 0.9 or of 0.1 both give a confidence of 0.8.
    /// <para>
    /// A malformed answer is never accepted: an empty set of probabilities, a mass that is negative or
    /// not finite, or a total outside 1 plus or minus 0.02 all read as a confidence of 0, so the
    /// question falls through to the next stage rather than standing on nonsense.
    /// </para>
    /// </summary>
    /// <param name="floor">The lowest confidence that still stands, on the 0 to 1 scale.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="floor"/> is not finite, or lies outside 0 to 1.</exception>
    public static AcceptAnswer ConfidenceAtLeast(double floor)
    {
        if (!double.IsFinite(floor) || floor is < 0d or > 1d)
        {
            throw new ArgumentOutOfRangeException(nameof(floor), floor, "A confidence floor has to be a finite value from 0 to 1.");
        }

        return (question, answer) => ConfidenceOf(question, answer) >= floor;
    }

    private static double ConfidenceOf(QuestionSpec question, AnswerSpec answer)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(answer);

        switch (answer)
        {
            case AssertAnswerSpec assertion:
                var probability = assertion.Probability;
                return !double.IsFinite(probability) || probability is < 0d or > 1d
                    ? 0d
                    : ConfidenceFormula.From(2, Math.Max(probability, 1d - probability));
            case ClassifyAnswerSpec classification:
                return Confidence(question, classification.Probabilities);
            case RateAnswerSpec rating:
                return Confidence(question, rating.Probabilities);
            default:
                return 0d;
        }
    }

    private static double Confidence(QuestionSpec question, IReadOnlyDictionary<string, double> probabilities)
    {
        if (probabilities is null || probabilities.Count == 0)
        {
            return 0d;
        }

        var total = 0d;
        var top = 0d;
        foreach (var value in probabilities.Values)
        {
            if (!double.IsFinite(value) || value < 0d)
            {
                return 0d;
            }

            total += value;
            if (value > top)
            {
                top = value;
            }
        }

        return Math.Abs(total - 1d) > 0.02d ? 0d : ConfidenceFormula.From(Count(question, probabilities.Count), top);
    }

    private static int Count(QuestionSpec question, int fallback) => question switch
    {
        ClassifySpec classify => classify.Options.Count,
        RateSpec rate => rate.Levels.Count,
        _ => fallback,
    };
}

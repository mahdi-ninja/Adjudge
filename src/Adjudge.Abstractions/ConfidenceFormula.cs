namespace Adjudge;

internal static class ConfidenceFormula
{
    public static double From(int optionCount, double topProbability) =>
        optionCount <= 1
            ? 1d
            : Math.Clamp(((optionCount * topProbability) - 1d) / (optionCount - 1d), 0d, 1d);
}

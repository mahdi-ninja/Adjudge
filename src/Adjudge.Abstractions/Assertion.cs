namespace Adjudge;

/// <summary>The answer to a yes/no question, reported as a probability rather than a verdict.</summary>
/// <param name="Probability">How likely the proposition is, from 0 to 1.</param>
/// <exception cref="ArgumentOutOfRangeException">The value falls outside 0 to 1.</exception>
public sealed record Assertion(double Probability)
{
    private readonly double _probability = InRange(Probability, nameof(Probability));

    /// <summary>How likely the proposition is, from 0 to 1.</summary>
    public double Probability
    {
        get => _probability;
        init => _probability = InRange(value, nameof(Probability));
    }

    private static double InRange(double value, string name)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 0, name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 1, name);
        return value;
    }
}

namespace Adjudge;

/// <summary>The answer to an ordered question: a position on the level scale, not just the closest level.</summary>
/// <typeparam name="T">The enum whose members are the levels, in ascending order.</typeparam>
/// <param name="Value">The position on the scale, where 0 is the first level.</param>
/// <param name="Nearest">The level the position rounds to.</param>
/// <param name="Distribution">The probability mass over every level.</param>
/// <param name="Confidence">How sharp the distribution is, computed by the library.</param>
public sealed record Rating<T>(double Value, T Nearest, Distribution<T> Distribution, Confidence Confidence)
    where T : struct, Enum
{
    /// <summary>The position of the context on the level scale, where 0 is the first declared level.</summary>
    public double Value { get; init; } = Value;

    /// <summary>Builds a rating whose position is the probability-weighted mean of the level positions.</summary>
    /// <exception cref="ArgumentException">The distribution does not cover every level of <typeparamref name="T"/>.</exception>
    public static Rating<T> From(Distribution<T> distribution, Confidence confidence)
    {
        var levels = EnumMembers<T>.Values;
        if (distribution.Probabilities.Count != levels.Length || levels.Length == 0)
        {
            throw new ArgumentException($"The distribution does not cover the members of '{typeof(T).Name}'.", nameof(distribution));
        }

        var value = 0d;
        for (var index = 0; index < levels.Length; index++)
        {
            value += index * distribution.Probabilities[levels[index]];
        }

        var nearest = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        return new Rating<T>(value, levels[Math.Clamp(nearest, 0, levels.Length - 1)], distribution, confidence);
    }
}

namespace Adjudge.Testing;

/// <summary>Builds answers directly, for tests that assert over an answer without running a decision.</summary>
public static class Answers
{
    /// <summary>Builds a distribution whose top member is <paramref name="top"/> and whose derived confidence comes out at <paramref name="confidence"/>, with the rest of the mass spread evenly.</summary>
    /// <typeparam name="T">The enum the mass is spread over.</typeparam>
    /// <exception cref="ArgumentException">The enum has no members, or <paramref name="top"/> is not one of them.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="confidence"/> is outside 0 to 1, or not finite.</exception>
    public static Distribution<T> Distribution<T>(T top, double confidence)
        where T : struct, Enum
    {
        var members = EnumMembers<T>.Values;
        if (members.Length == 0)
        {
            throw new ArgumentException($"'{typeof(T).Name}' declares no members.", nameof(top));
        }

        if (!members.Contains(top))
        {
            throw new ArgumentException($"'{top}' is not a member of '{typeof(T).Name}'.", nameof(top));
        }

        var probabilities = new Dictionary<T, double>(members.Length);
        foreach (var (member, probability) in Spread(members, top, confidence))
        {
            probabilities[member] = probability;
        }

        return new Distribution<T>(probabilities);
    }

    /// <summary>Builds a classification with a plausible distribution behind it.</summary>
    /// <typeparam name="T">The enum whose members are the options.</typeparam>
    /// <param name="value">The option the mass sits on.</param>
    /// <param name="confidence">The confidence the distribution should report.</param>
    /// <exception cref="ArgumentException"><paramref name="value"/> is not a member of <typeparamref name="T"/>.</exception>
    public static Classification<T> Classification<T>(T value, double confidence = 0.9)
        where T : struct, Enum
    {
        var distribution = Distribution(value, confidence);
        return new Classification<T>(value, distribution, Derived(distribution.Confidence));
    }

    /// <summary>Builds a rating whose fractional value lands on <paramref name="nearest"/>.</summary>
    /// <typeparam name="T">The enum whose members are the levels.</typeparam>
    /// <param name="nearest">The level the mass sits on.</param>
    /// <param name="confidence">The confidence the distribution should report.</param>
    /// <exception cref="ArgumentException"><paramref name="nearest"/> is not a member of <typeparamref name="T"/>.</exception>
    public static Rating<T> Rating<T>(T nearest, double confidence = 0.9)
        where T : struct, Enum
    {
        var distribution = Distribution(nearest, confidence);
        return Adjudge.Rating<T>.From(distribution, Derived(distribution.Confidence));
    }

    /// <summary>Builds an assertion.</summary>
    /// <param name="probability">The mass on the proposition holding.</param>
    public static Assertion Assertion(double probability) => new(probability);

    /// <summary>Inverts the library confidence formula, so the distribution reports the requested confidence.</summary>
    internal static double TopMass(int count, double confidence)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        if (!double.IsFinite(confidence))
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be a finite value.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(confidence, 0, nameof(confidence));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(confidence, 1, nameof(confidence));

        return count == 1 ? 1d : ((confidence * (count - 1)) + 1d) / count;
    }

    private static IEnumerable<(T Member, double Probability)> Spread<T>(T[] members, T top, double confidence)
        where T : struct, Enum
    {
        var mass = TopMass(members.Length, confidence);
        var rest = members.Length == 1 ? 0d : (1d - mass) / (members.Length - 1);

        foreach (var member in members)
        {
            yield return (member, member.Equals(top) ? mass : rest);
        }
    }

    private static Confidence Derived(double value) => new(value, ConfidenceSource.Derived);
}

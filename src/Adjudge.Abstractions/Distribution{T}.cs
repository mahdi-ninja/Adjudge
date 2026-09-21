using System.Collections.ObjectModel;

namespace Adjudge;

/// <summary>
/// The probability mass an answer put over a closed set of enum members. Every member is present,
/// including those the provider left out, and the values are normalised to sum to 1.
/// </summary>
/// <typeparam name="T">The enum the answer is over.</typeparam>
public readonly record struct Distribution<T>
    where T : struct, Enum
{
    private readonly ReadOnlyDictionary<T, double>? _probabilities;

    /// <summary>Normalises the supplied weights, so they need not already sum to 1.</summary>
    /// <param name="probabilities">Weights for some or all members; anything omitted is taken as zero.</param>
    /// <exception cref="ArgumentException">The enum has no members, the map is empty, names a non-member, or holds a negative, non-finite or all-zero set of weights.</exception>
    public Distribution(IReadOnlyDictionary<T, double> probabilities)
    {
        ArgumentNullException.ThrowIfNull(probabilities);

        var members = EnumMembers<T>.Values;
        if (members.Length == 0)
        {
            throw new ArgumentException($"'{typeof(T).Name}' declares no members.", nameof(probabilities));
        }

        if (probabilities.Count == 0)
        {
            throw new ArgumentException("Distribution must contain at least one entry.", nameof(probabilities));
        }

        var normalised = new Dictionary<T, double>(members.Length);
        foreach (var member in members)
        {
            normalised[member] = 0d;
        }

        var total = 0d;
        foreach (var (key, value) in probabilities)
        {
            if (!normalised.ContainsKey(key))
            {
                throw new ArgumentException($"'{key}' is not a member of '{typeof(T).Name}'.", nameof(probabilities));
            }

            if (!double.IsFinite(value) || value < 0)
            {
                throw new ArgumentException($"Probability for '{key}' must be a finite value of zero or greater.", nameof(probabilities));
            }

            total += value;
        }

        if (!double.IsFinite(total))
        {
            throw new ArgumentException("Probabilities must sum to a finite value.", nameof(probabilities));
        }

        if (total <= 0)
        {
            throw new ArgumentException("Probabilities must not all be zero.", nameof(probabilities));
        }

        foreach (var member in members)
        {
            normalised[member] = probabilities.TryGetValue(member, out var value) ? value / total : 0d;
        }

        _probabilities = new ReadOnlyDictionary<T, double>(normalised);

        var top = members[0];
        var topValue = double.NegativeInfinity;
        var secondValue = double.NegativeInfinity;
        foreach (var member in members)
        {
            var value = normalised[member];
            if (value > topValue)
            {
                secondValue = topValue;
                topValue = value;
                top = member;
            }
            else if (value > secondValue)
            {
                secondValue = value;
            }
        }

        Top = top;
        Margin = members.Length == 1 ? topValue : topValue - secondValue;
        Confidence = members.Length == 1
            ? 1d
            : Math.Clamp(((members.Length * topValue) - 1d) / (members.Length - 1d), 0d, 1d);
    }

    /// <summary>The normalised mass per member, summing to 1 and covering every member of the enum.</summary>
    public IReadOnlyDictionary<T, double> Probabilities => _probabilities ?? ReadOnlyDictionary<T, double>.Empty;

    /// <summary>The member holding the most probability mass, tie-broken by the lowest underlying value.</summary>
    public T Top { get; }

    /// <summary>The gap between the top member and the runner-up, which is small when the answer was a close call.</summary>
    public double Margin { get; }

    /// <summary>How far the distribution is from uniform, on a scale where 0 is uniform and 1 puts all mass on one member.</summary>
    public double Confidence { get; }

    /// <summary>Compares two distributions member by member, so two separately normalised maps can be equal.</summary>
    public bool Equals(Distribution<T> other)
    {
        if (!Top.Equals(other.Top) ||
            !Margin.Equals(other.Margin) ||
            !Confidence.Equals(other.Confidence))
        {
            return false;
        }

        var mine = Probabilities;
        var theirs = other.Probabilities;
        if (mine.Count != theirs.Count)
        {
            return false;
        }

        foreach (var member in EnumMembers<T>.Values)
        {
            var hasMine = mine.TryGetValue(member, out var left);
            var hasTheirs = theirs.TryGetValue(member, out var right);
            if (hasMine != hasTheirs || !left.Equals(right))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Top);
        hash.Add(Margin);
        hash.Add(Confidence);

        var probabilities = Probabilities;
        foreach (var member in EnumMembers<T>.Values)
        {
            hash.Add(probabilities.TryGetValue(member, out var value) ? value : 0d);
        }

        return hash.ToHashCode();
    }
}

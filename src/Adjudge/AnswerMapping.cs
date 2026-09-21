using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Adjudge;

internal static class AnswerMapping
{
    private const double SumTolerance = 0.02;

    public static Distribution<T> ToDistribution<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T>(
        IReadOnlyDictionary<string, double> probabilities,
        string question,
        string provider)
        where T : struct, Enum
    {
        if (probabilities is null || probabilities.Count == 0)
        {
            throw new ProviderResponseException($"Question '{question}' came back with no probabilities.", provider);
        }

        var byName = EnumMetadata<T>.ByName;
        var mapped = new Dictionary<T, double>(probabilities.Count);
        var total = 0d;

        foreach (var (key, value) in probabilities)
        {
            if (!byName.TryGetValue(key, out var member))
            {
                throw new ProviderResponseException(
                    $"Question '{question}' came back with key '{key}', which is not a member of '{typeof(T).Name}'.",
                    provider);
            }

            if (double.IsNaN(value) || value < 0)
            {
                throw new ProviderResponseException($"Question '{question}' came back with a negative probability for '{key}'.", provider);
            }

            mapped[member] = value;
            total += value;
        }

        if (Math.Abs(total - 1d) > SumTolerance)
        {
            throw new ProviderResponseException(
                $"Probabilities for question '{question}' sum to {total.ToString("0.###", CultureInfo.InvariantCulture)}, which is not close enough to 1.",
                provider);
        }

        return new Distribution<T>(mapped);
    }

    public static Confidence Confidence(double derived, double? reported) =>
        reported is null
            ? new Confidence(derived, ConfidenceSource.Derived)
            : new Confidence(derived, ConfidenceSource.Native, reported);
}

namespace Adjudge.Testing.Tests;

public static class Probabilities
{
    public static Distribution<T> ToDistribution<T>(IReadOnlyDictionary<string, double> probabilities)
        where T : struct, Enum =>
        new(probabilities.ToDictionary(p => Enum.Parse<T>(p.Key), p => p.Value));
}

namespace Adjudge.Providers;

/// <summary>
/// Collects the predicates that decide a classification. Each call names one option and one test;
/// several tests may name the same option, and any of them holding marks that option matched.
/// </summary>
/// <typeparam name="TContext">The context the predicates read.</typeparam>
/// <typeparam name="T">The enum the question classifies over.</typeparam>
public sealed class RulesClassifyBuilder<TContext, T>
    where TContext : class
    where T : struct, Enum
{
    private readonly List<(string Key, Func<TContext, bool> Predicate)> _rules = [];

    internal RulesClassifyBuilder()
    {
    }

    internal IReadOnlyList<(string Key, Func<TContext, bool> Predicate)> Rules => _rules;

    /// <summary>Marks <paramref name="option"/> as matched whenever <paramref name="predicate"/> holds.</summary>
    /// <param name="option">The option the predicate votes for.</param>
    /// <param name="predicate">The test, run against the context on every decision.</param>
    /// <returns>The same builder, so tests can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="option"/> is not a declared member of <typeparamref name="T"/>.</exception>
    public RulesClassifyBuilder<TContext, T> When(T option, Func<TContext, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        var key = Enum.GetName(option);
        if (key is null || !RuleOptionKeys<T>.Keys.Contains(key))
        {
            throw new ArgumentException(
                $"'{option}' is not a declared member of '{typeof(T).Name}', so it has no key to answer with.",
                nameof(option));
        }

        _rules.Add((key, predicate));
        return this;
    }
}

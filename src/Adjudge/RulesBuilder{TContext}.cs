namespace Adjudge.Providers;

/// <summary>Collects the rules a <see cref="RulesProvider{TContext}"/> is built from. One question is registered once.</summary>
/// <typeparam name="TContext">The context type the rules read.</typeparam>
public sealed class RulesBuilder<TContext>
    where TContext : class
{
    private readonly Dictionary<string, RulesRule<TContext>> _rules = new(StringComparer.Ordinal);

    internal RulesBuilder()
    {
    }

    /// <summary>Registers the predicates that classify one question.</summary>
    /// <typeparam name="T">The enum the question classifies over, which must be the one the decision uses.</typeparam>
    /// <param name="question">The question name, in camelCase, as the definition produces it.</param>
    /// <param name="configure">Adds the per-option predicates to the builder.</param>
    /// <returns>The same builder, so registrations can be chained.</returns>
    /// <exception cref="ArgumentException"><paramref name="question"/> is null, empty, whitespace, or already registered, or a rule names an option that is not a member of <typeparamref name="T"/>.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is null.</exception>
    public RulesBuilder<TContext> Classify<T>(string question, Action<RulesClassifyBuilder<TContext, T>> configure)
        where T : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new RulesClassifyBuilder<TContext, T>();
        configure(builder);
        var rules = builder.Rules.ToArray();
        foreach (var rule in rules)
        {
            if (!RuleOptionKeys<T>.Keys.Contains(rule.Key))
            {
                throw new ArgumentException(
                    $"Rule for question '{question}' names option '{rule.Key}', which is not a member of " +
                    $"'{typeof(T).Name}'. Its members are: {RuleOptionKeys<T>.Rendered}.",
                    nameof(configure));
            }
        }

        return Register(question, (context, spec) =>
        {
            if (spec is not ClassifySpec classify)
            {
                return null;
            }

            RequireKeys<T>(classify);

            string? matched = null;
            foreach (var rule in rules)
            {
                if (!rule.Predicate(context) || string.Equals(rule.Key, matched, StringComparison.Ordinal))
                {
                    continue;
                }

                if (matched is not null)
                {
                    return null;
                }

                matched = rule.Key;
            }

            if (matched is null)
            {
                return null;
            }

            var probabilities = new Dictionary<string, double>(classify.Options.Count, StringComparer.Ordinal);
            foreach (var option in classify.Options)
            {
                probabilities[option.Key] = string.Equals(option.Key, matched, StringComparison.Ordinal) ? 1d : 0d;
            }

            return new ClassifyAnswerSpec(spec.Name, probabilities, Confidence: null, ConfidenceSource.Heuristic);
        });
    }

    /// <summary>Registers the predicates that settle one proposition. Both holding is a contradiction, so the question is declined.</summary>
    /// <param name="question">The question name, in camelCase, as the definition produces it.</param>
    /// <param name="whenTrue">Holds when the proposition is certainly true.</param>
    /// <param name="whenFalse">Holds when the proposition is certainly false. Without it, only the true case is ever answered.</param>
    /// <returns>The same builder, so registrations can be chained.</returns>
    /// <exception cref="ArgumentException"><paramref name="question"/> is null, empty, whitespace, or already registered.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="whenTrue"/> is null.</exception>
    public RulesBuilder<TContext> Assert(string question, Func<TContext, bool> whenTrue, Func<TContext, bool>? whenFalse = null)
    {
        ArgumentNullException.ThrowIfNull(whenTrue);

        return Register(question, (context, spec) =>
        {
            if (spec is not AssertSpec)
            {
                return null;
            }

            var holdsTrue = whenTrue(context);
            var holdsFalse = whenFalse is not null && whenFalse(context);
            if (holdsTrue == holdsFalse)
            {
                return null;
            }

            return new AssertAnswerSpec(spec.Name, holdsTrue ? 1d : 0d);
        });
    }

    internal IReadOnlyDictionary<string, RulesRule<TContext>> Rules => _rules;

    private static void RequireKeys<T>(ClassifySpec classify)
        where T : struct, Enum
    {
        var expected = RuleOptionKeys<T>.Keys;
        var matches = classify.Options.Count == expected.Count;
        if (matches)
        {
            foreach (var option in classify.Options)
            {
                if (!expected.Contains(option.Key))
                {
                    matches = false;
                    break;
                }
            }
        }

        if (!matches)
        {
            throw new DecisionDefinitionException(
                $"Rules for question '{classify.Name}' were written over '{typeof(T).Name}', whose options are: " +
                $"{RuleOptionKeys<T>.Rendered}. The decision offers: {string.Join(", ", classify.Options.Select(o => o.Key))}.");
        }
    }

    private RulesBuilder<TContext> Register(string question, RulesRule<TContext> rule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);

        if (!_rules.TryAdd(question, rule))
        {
            throw new ArgumentException($"Question '{question}' already has rules registered.", nameof(question));
        }

        return this;
    }
}

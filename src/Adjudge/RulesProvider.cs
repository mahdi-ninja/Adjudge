namespace Adjudge.Providers;

/// <summary>Builds a <see cref="RulesProvider{TContext}"/>, which is the only way one is made.</summary>
public static class RulesProvider
{
    /// <summary>Collects the rules and freezes them into a provider that never changes afterwards.</summary>
    /// <typeparam name="TContext">The context type the rules read, which every decision using this provider must supply.</typeparam>
    /// <param name="configure">Registers the rules on the builder.</param>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is null.</exception>
    /// <exception cref="ArgumentException">A question was registered twice, or a rule named an option its enum does not declare.</exception>
    public static RulesProvider<TContext> Create<TContext>(Action<RulesBuilder<TContext>> configure)
        where TContext : class
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new RulesBuilder<TContext>();
        configure(builder);

        return new RulesProvider<TContext>(builder.Rules);
    }
}

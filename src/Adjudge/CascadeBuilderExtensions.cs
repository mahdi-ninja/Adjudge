using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Adjudge.Providers;

/// <summary>Registers a <see cref="CascadeProvider"/> as the engine's provider.</summary>
public static class CascadeBuilderExtensions
{
    /// <summary>
    /// Registers a cascade of stages as the single <see cref="IDecisionProvider"/> the engine calls. It
    /// removes every <see cref="IDecisionProvider"/> registration made before it and adds the cascade as
    /// a singleton, so a provider package registered earlier serves the cascade rather than the engine.
    /// A later <c>TryAdd</c>-based registration, which is what <c>AddJev</c> and <c>AddOpenAI</c> use, is
    /// a no-op and leaves the cascade in place; a later plain <c>AddSingleton&lt;IDecisionProvider&gt;</c>
    /// would win, because the last plain registration of a service always does.
    /// </summary>
    /// <param name="builder">The Adjudge builder to register on.</param>
    /// <param name="configure">Adds the stages, cheapest first by convention.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="configure"/> is null.</exception>
    /// <exception cref="ArgumentException">No stage was added.</exception>
    public static IAdjudgeBuilder UseCascade(this IAdjudgeBuilder builder, Action<CascadeBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var cascade = new CascadeBuilder();
        configure(cascade);
        if (cascade.Count == 0)
        {
            throw new ArgumentException("A cascade needs at least one stage.", nameof(configure));
        }

        builder.Services.RemoveAll<IDecisionProvider>();
        builder.Services.AddSingleton<IDecisionProvider>(cascade.Build);
        return builder;
    }
}

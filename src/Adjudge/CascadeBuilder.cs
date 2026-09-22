using Microsoft.Extensions.DependencyInjection;

namespace Adjudge.Providers;

/// <summary>Collects the stages of a cascade, in the order they will be tried.</summary>
public sealed class CascadeBuilder
{
    private readonly List<Func<IServiceProvider, CascadeStage>> _stages = [];

    /// <summary>
    /// Adds a stage whose provider is resolved from the container, so it has to be registered as its own
    /// concrete type, and as a singleton: the cascade resolves it once, from the root provider, and holds
    /// on to it for the life of the application.
    /// </summary>
    /// <typeparam name="TProvider">The provider type to resolve.</typeparam>
    /// <param name="accept">Whether an answer from this stage is good enough to stand. Left null, every answer stands.</param>
    /// <param name="fallThroughOnError">Sets <see cref="CascadeStage.FallThroughOnError"/> on the stage this adds.</param>
    public CascadeBuilder Stage<TProvider>(
        AcceptAnswer? accept = null,
        bool fallThroughOnError = false)
        where TProvider : class, IDecisionProvider
    {
        _stages.Add(services => new CascadeStage(services.GetRequiredService<TProvider>(), accept)
        {
            FallThroughOnError = fallThroughOnError,
        });

        return this;
    }

    /// <summary>Adds a stage over a provider the caller already has in hand.</summary>
    /// <param name="instance">The provider this stage calls.</param>
    /// <param name="accept">Whether an answer from this stage is good enough to stand. Left null, every answer stands.</param>
    /// <param name="fallThroughOnError">Sets <see cref="CascadeStage.FallThroughOnError"/> on the stage this adds.</param>
    /// <exception cref="ArgumentNullException"><paramref name="instance"/> is null.</exception>
    public CascadeBuilder Stage(
        IDecisionProvider instance,
        AcceptAnswer? accept = null,
        bool fallThroughOnError = false)
    {
        ArgumentNullException.ThrowIfNull(instance);

        _stages.Add(_ => new CascadeStage(instance, accept) { FallThroughOnError = fallThroughOnError });
        return this;
    }

    /// <summary>Adds a stage whose provider is built from the container, for a provider that is not registered under a type of its own.</summary>
    /// <param name="factory">Builds the provider, called once when the cascade itself is built.</param>
    /// <param name="accept">Whether an answer from this stage is good enough to stand. Left null, every answer stands.</param>
    /// <param name="fallThroughOnError">Sets <see cref="CascadeStage.FallThroughOnError"/> on the stage this adds.</param>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is null.</exception>
    public CascadeBuilder Stage(
        Func<IServiceProvider, IDecisionProvider> factory,
        AcceptAnswer? accept = null,
        bool fallThroughOnError = false)
    {
        ArgumentNullException.ThrowIfNull(factory);

        _stages.Add(services => new CascadeStage(factory(services), accept) { FallThroughOnError = fallThroughOnError });
        return this;
    }

    internal int Count => _stages.Count;

    internal CascadeProvider Build(IServiceProvider services) =>
        new(_stages.Select(stage => stage(services)));
}

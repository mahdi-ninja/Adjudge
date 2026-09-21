using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Adjudge;

/// <summary>Registers the engine into a container.</summary>
public static class ServiceCollectionExtensions
{
    private const string ReflectionMessage =
        "Serialising the context uses reflection. Use the overload that takes a SerialiseContext, built with ContextSerializer.From.";

    /// <summary>Registers the engine, serialising contexts by reflection. Call it once per collection; a provider still has to be registered.</summary>
    /// <param name="services">The collection the engine is registered into.</param>
    /// <param name="configure">Tunes the engine options before they are bound.</param>
    /// <returns>A builder for chaining provider and decision registrations.</returns>
    /// <exception cref="InvalidOperationException">The engine is already registered in this collection.</exception>
    [RequiresUnreferencedCode(ReflectionMessage)]
    [RequiresDynamicCode(ReflectionMessage)]
    public static IAdjudgeBuilder AddAdjudge(this IServiceCollection services, Action<DecisionEngineOptions>? configure = null)
    {
        Register(services, configure);
        services.TryAddSingleton(sp => new DecisionEngine(
            sp.GetRequiredService<Providers.IDecisionProvider>(),
            sp.GetRequiredService<IOptions<DecisionEngineOptions>>().Value));

        return new AdjudgeBuilder(services);
    }

    /// <summary>Registers the engine with an explicit context serialiser, which keeps the whole path trimming and ahead-of-time safe.</summary>
    /// <param name="services">The collection the engine is registered into.</param>
    /// <param name="contextSerializer">Turns a context into JSON, typically through a source-generated serialiser context.</param>
    /// <param name="configure">Tunes the engine options before they are bound.</param>
    /// <returns>A builder for chaining provider and decision registrations.</returns>
    /// <exception cref="InvalidOperationException">The engine is already registered in this collection.</exception>
    public static IAdjudgeBuilder AddAdjudge(
        this IServiceCollection services,
        SerialiseContext contextSerializer,
        Action<DecisionEngineOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(contextSerializer);

        Register(services, configure);
        services.TryAddSingleton(sp => new DecisionEngine(
            sp.GetRequiredService<Providers.IDecisionProvider>(),
            sp.GetRequiredService<IOptions<DecisionEngineOptions>>().Value,
            contextSerializer));

        return new AdjudgeBuilder(services);
    }

    private static void Register(IServiceCollection services, Action<DecisionEngineOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (services.Any(descriptor => descriptor.ServiceType == typeof(DecisionEngine)))
        {
            throw new InvalidOperationException("AddAdjudge has already been called on this service collection.");
        }

        services.AddOptions();
        if (configure is not null)
        {
            services.Configure(configure);
        }
    }
}

using System.Diagnostics.CodeAnalysis;
using Adjudge.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Adjudge;

/// <summary>Registration helpers hung off the builder that <see cref="ServiceCollectionExtensions.AddAdjudge(Microsoft.Extensions.DependencyInjection.IServiceCollection, Action{DecisionEngineOptions})"/> returns.</summary>
public static class AdjudgeBuilderExtensions
{
    /// <summary>Registers the decision as a singleton <see cref="IDecision{TContext, TResult}"/>. The definition is validated when the engine first creates it, not here.</summary>
    /// <typeparam name="TDecision">The decision to register. It needs a parameterless constructor.</typeparam>
    /// <typeparam name="TContext">The facts the decision takes.</typeparam>
    /// <typeparam name="TResult">The typed answers the decision produces.</typeparam>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    public static IAdjudgeBuilder AddDecision<TDecision, TContext, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TResult>(
        this IAdjudgeBuilder builder)
        where TDecision : Decision<TContext, TResult>, new()
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddSingleton(sp => sp.GetRequiredService<DecisionEngine>().Create<TDecision, TContext, TResult>());
        return builder;
    }

    /// <summary>Registers a provider as a singleton, unless one is registered already. Exactly one provider is supported.</summary>
    /// <typeparam name="TProvider">The provider to resolve from the container.</typeparam>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is null.</exception>
    public static IAdjudgeBuilder UseProvider<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>(
        this IAdjudgeBuilder builder)
        where TProvider : class, IDecisionProvider
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddSingleton<IDecisionProvider, TProvider>();
        return builder;
    }
}

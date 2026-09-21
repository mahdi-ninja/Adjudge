using System.Diagnostics.CodeAnalysis;

namespace Adjudge;

internal sealed class EngineDecision<TContext, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TResult>(
    DecisionEngine engine,
    DecisionDefinition<TContext, TResult> definition)
    : IDecision<TContext, TResult>
{
    public Task<DecisionResult<TResult>> DecideAsync(TContext context, CancellationToken ct = default) =>
        engine.EvaluateAsync(definition, context, ct);
}

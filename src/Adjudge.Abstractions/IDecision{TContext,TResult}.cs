namespace Adjudge;

/// <summary>One decision, ready to evaluate. Inject this rather than the engine.</summary>
/// <typeparam name="TContext">The facts the decision is made from.</typeparam>
/// <typeparam name="TResult">The type holding the answers.</typeparam>
public interface IDecision<in TContext, TResult>
{
    /// <summary>Sends the context to the provider and maps the answers back into <typeparamref name="TResult"/>.</summary>
    /// <exception cref="ProviderException">The provider failed.</exception>
    /// <exception cref="ProviderResponseException">The provider answered, but the answer could not be mapped.</exception>
    Task<DecisionResult<TResult>> DecideAsync(TContext context, CancellationToken ct = default);
}

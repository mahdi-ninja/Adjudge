namespace Adjudge.Providers;

/// <summary>One rung of a <see cref="CascadeProvider"/>: a provider plus the bar its answers have to clear before the cascade stops asking.</summary>
public sealed class CascadeStage
{
    /// <summary>Creates a stage over a provider.</summary>
    /// <param name="provider">The provider this rung calls.</param>
    /// <param name="accept">Whether an answer is good enough to stand. Left null, every answer stands.</param>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is null.</exception>
    public CascadeStage(IDecisionProvider provider, AcceptAnswer? accept = null)
    {
        ArgumentNullException.ThrowIfNull(provider);

        Provider = provider;
        Accept = accept ?? AcceptWhen.Always;
    }

    /// <summary>The provider this rung calls.</summary>
    public IDecisionProvider Provider { get; }

    /// <summary>The bar an answer has to clear. The last stage is authoritative, so its answers stand whatever this says.</summary>
    public AcceptAnswer Accept { get; }

    /// <summary>
    /// Whether a failure from this stage is swallowed and the questions carried to the next one. Off by
    /// default, so failures surface. It covers a transient <see cref="ProviderException"/> and a
    /// <see cref="ProviderResponseException"/> only; everything else propagates, cancellation and
    /// <see cref="DecisionDefinitionException"/> included. It is ignored on the last stage, which has
    /// nowhere to fall through to.
    /// <para>
    /// The <c>fallThroughOnError</c> parameter on the cascade builder's <c>Stage</c> methods sets this
    /// property, for callers who do not construct the stage themselves.
    /// </para>
    /// </summary>
    public bool FallThroughOnError { get; init; }
}

namespace Adjudge;

/// <summary>One evaluation of a decision: the typed answers, plus what produced them.</summary>
/// <typeparam name="TResult">The type holding the answers.</typeparam>
/// <param name="Id">Unique to this evaluation, so it can be correlated with logs.</param>
/// <param name="DefinitionId">The identifier the decision is reported under.</param>
/// <param name="Provider">The provider that answered.</param>
/// <param name="Model">The model the provider reported, when it named one.</param>
/// <param name="Value">The typed answers.</param>
/// <param name="Usage">Tokens the provider reported, when it reported any.</param>
/// <param name="Timestamp">When the engine completed the evaluation.</param>
public sealed record DecisionResult<TResult>(
    Guid Id,
    string DefinitionId,
    string Provider,
    string? Model,
    TResult Value,
    Usage? Usage,
    DateTimeOffset Timestamp);

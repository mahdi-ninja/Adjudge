namespace Adjudge.Providers;

/// <summary>Everything a provider needs for one evaluation.</summary>
/// <param name="Context">The caller's facts, which the provider serialises itself.</param>
/// <param name="Questions">The questions to answer, in the order the definition declared them.</param>
/// <param name="DefinitionId">The identifier of the decision, useful for provider-side logging.</param>
public sealed record ProviderRequest(
    DecisionContext Context,
    IReadOnlyList<QuestionSpec> Questions,
    string DefinitionId);

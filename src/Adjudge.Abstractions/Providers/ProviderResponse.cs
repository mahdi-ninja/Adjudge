namespace Adjudge.Providers;

/// <summary>Everything a provider hands back from one evaluation.</summary>
/// <param name="Answers">One answer per question, keyed by question name. A missing question is an error the engine raises.</param>
/// <param name="Model">The model version the provider actually used, when it reports one.</param>
/// <param name="Usage">What the evaluation cost, when the provider reports it.</param>
/// <param name="Metadata">Anything else worth carrying, such as a request identifier. The engine does not interpret it.</param>
public sealed record ProviderResponse(
    IReadOnlyDictionary<string, AnswerSpec> Answers,
    string? Model = null,
    Usage? Usage = null,
    IReadOnlyDictionary<string, string>? Metadata = null);

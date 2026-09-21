namespace Adjudge;

/// <summary>What the provider reported spending on one evaluation. Either figure may be absent.</summary>
/// <param name="InputTokens">Tokens the provider counted against the request.</param>
/// <param name="OutputTokens">Tokens the provider counted against the answer.</param>
public sealed record Usage(long? InputTokens, long? OutputTokens);

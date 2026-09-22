namespace Adjudge.OpenAI;

/// <summary>How the provider turns one chat call, or several, into a distribution over the labels it offered.</summary>
public enum ConfidenceStrategy
{
    /// <summary>One call at temperature zero, reading the mass straight off the first answer token's top log probabilities.</summary>
    LogProbabilities,

    /// <summary>Several calls at <see cref="OpenAIOptions.SamplingTemperature"/>, counting how often each label came back.</summary>
    Sampling,
}

namespace Adjudge;

/// <summary>The answer to a closed-set question: one option, with the full distribution behind it.</summary>
/// <typeparam name="T">The enum whose members are the options.</typeparam>
/// <param name="Value">The chosen option, which is always the distribution's top member.</param>
/// <param name="Distribution">The probability mass over every option.</param>
/// <param name="Confidence">How sharp the distribution is, computed by the library.</param>
public sealed record Classification<T>(T Value, Distribution<T> Distribution, Confidence Confidence)
    where T : struct, Enum;

namespace Adjudge.Testing;

/// <summary>What <see cref="FakeDecisionProvider"/> does with a question that has no script.</summary>
public enum UnscriptedBehaviour
{
    /// <summary>Spread the mass evenly over the keys, so the answer is valid but carries no confidence.</summary>
    Uniform,

    /// <summary>Fail the call, so a question nobody scripted cannot pass unnoticed.</summary>
    Throw,
}

namespace Adjudge.Tests.Abstractions;

public sealed class AssertionTests
{
    [Fact]
    public void Constructor_WhenProbabilityIsInRange_KeepsIt()
    {
        new Assertion(0.7).Probability.ShouldBe(0.7);
    }

    [Fact]
    public void Constructor_WhenProbabilityIsAboveOne_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new Assertion(1.1));
    }

    [Fact]
    public void Constructor_WhenProbabilityIsNegative_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new Assertion(-0.1));
    }

    [Fact]
    public void With_WhenProbabilityIsOutOfRange_Throws()
    {
        var assertion = new Assertion(0.5);

        Should.Throw<ArgumentOutOfRangeException>(() => assertion with { Probability = 1.5 });
    }
}

namespace Adjudge.Testing.Tests;

public sealed class AnswersTests
{
    [Theory]
    [InlineData(0.9)]
    [InlineData(0.42)]
    [InlineData(1d)]
    public void Distribution_ForTwoMembers_DerivesRequestedConfidence(double confidence)
    {
        var distribution = Answers.Distribution(Pair.B, confidence);

        distribution.Confidence.ShouldBe(confidence, 1e-9);
        distribution.Top.ShouldBe(Pair.B);
    }

    [Fact]
    public void Distribution_ForThreeMembers_DerivesRequestedConfidence()
    {
        var distribution = Answers.Distribution(Trio.C, 0.73);

        distribution.Confidence.ShouldBe(0.73, 1e-9);
        distribution.Probabilities.Values.Sum().ShouldBe(1d, 1e-9);
    }

    [Fact]
    public void Distribution_ForFiveMembers_SpreadsRemainderUniformly()
    {
        var distribution = Answers.Distribution(Five.A, 0.5);

        distribution.Confidence.ShouldBe(0.5, 1e-9);
        distribution.Probabilities
            .Where(p => p.Key != Five.A)
            .Select(p => p.Value)
            .Distinct()
            .Count()
            .ShouldBe(1);
    }

    [Fact]
    public void Distribution_ForSingleMember_PutsAllTheMassOnIt()
    {
        var distribution = Answers.Distribution(Solo.Only, 0.3);

        distribution.Probabilities[Solo.Only].ShouldBe(1d, 1e-9);
        distribution.Confidence.ShouldBe(1d);
    }

    [Fact]
    public void Distribution_WithConfidenceAboveOne_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Answers.Distribution(Trio.A, 1.5));

    [Fact]
    public void Classification_WithConfidence_IsDerivedAndMatches()
    {
        var classification = Answers.Classification(Trio.B, 0.8);

        classification.Value.ShouldBe(Trio.B);
        classification.Confidence.Value.ShouldBe(0.8, 1e-9);
        classification.Confidence.Source.ShouldBe(ConfidenceSource.Derived);
        classification.Confidence.ProviderReported.ShouldBeNull();
    }

    [Fact]
    public void Rating_WithConfidence_IsDerivedAndNearestIsTheRequestedLevel()
    {
        var rating = Answers.Rating(Urgency.High, 0.9);

        rating.Nearest.ShouldBe(Urgency.High);
        rating.Confidence.Value.ShouldBe(0.9, 1e-9);
        rating.Confidence.Source.ShouldBe(ConfidenceSource.Derived);
    }

    [Fact]
    public void Assertion_WithProbability_CarriesIt()
    {
        Answers.Assertion(0.72).Probability.ShouldBe(0.72);
    }
}

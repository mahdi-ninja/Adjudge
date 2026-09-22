using Adjudge.Providers;
using static Adjudge.Tests.Core.CascadeFixtures;

namespace Adjudge.Tests.Core;

[Collection(CascadeTestGroup.Name)]
public sealed class CascadeAcceptTests
{
    private const double Epsilon = 1e-6;

    [Fact]
    public void Always_AcceptsAnUninformativeAnswer() =>
        AcceptWhen.Always(AbusiveQuestion, new AssertAnswerSpec("abusive", 0.5)).ShouldBeTrue();

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    public void ConfidenceAtLeast_FloorOutsideZeroToOne_Throws(double floor) =>
        Should.Throw<ArgumentOutOfRangeException>(() => AcceptWhen.ConfidenceAtLeast(floor));

    [Theory]
    [InlineData(0.8, 0.15, 0.05, 0.7)]
    [InlineData(0.34, 0.33, 0.33, 0.01)]
    [InlineData(0.75, 0.2, 0.05, 0.625)]
    public void ConfidenceAtLeast_Classification_UsesTheOptionCountFromTheQuestion(
        double billing,
        double tracking,
        double returns,
        double confidence)
    {
        var answer = new ClassifyAnswerSpec(
            "intent",
            new Dictionary<string, double> { ["Billing"] = billing, ["Tracking"] = tracking, ["Returns"] = returns });

        Straddles(IntentQuestion, answer, confidence).ShouldBe((true, false));
    }

    [Theory]
    [InlineData(0.6, 0.3, 0.1, 0.4)]
    [InlineData(0.9, 0.05, 0.05, 0.85)]
    public void ConfidenceAtLeast_Rating_UsesTheLevelCountFromTheQuestion(
        double high,
        double medium,
        double low,
        double confidence)
    {
        var answer = new RateAnswerSpec(
            "urgency",
            new Dictionary<string, double> { ["High"] = high, ["Medium"] = medium, ["Low"] = low });

        Straddles(UrgencyQuestion, answer, confidence).ShouldBe((true, false));
    }

    [Theory]
    [InlineData(0.9, 0.8)]
    [InlineData(0.6, 0.2)]
    [InlineData(0.1, 0.8)]
    [InlineData(0.5, 0)]
    public void ConfidenceAtLeast_Proposition_ReadsItAsTwoOptions(double probability, double confidence) =>
        Straddles(AbusiveQuestion, new AssertAnswerSpec("abusive", probability), confidence).ShouldBe((true, false));

    [Fact]
    public void ConfidenceAtLeast_PropositionOutsideZeroToOne_IsNotAccepted() =>
        AcceptWhen.ConfidenceAtLeast(0.01)(AbusiveQuestion, new AssertAnswerSpec("abusive", 1.5)).ShouldBeFalse();

    [Fact]
    public void ConfidenceAtLeast_AllTheMassOnOneOption_ClearsTheHighestFloor()
    {
        var answer = new ClassifyAnswerSpec(
            "intent",
            new Dictionary<string, double> { ["Billing"] = 1, ["Tracking"] = 0, ["Returns"] = 0 });

        AcceptWhen.ConfidenceAtLeast(1)(IntentQuestion, answer).ShouldBeTrue();
    }

    [Fact]
    public void ConfidenceAtLeast_ClassificationMissingKeys_CountsThemAsZero()
    {
        var answer = new ClassifyAnswerSpec("intent", new Dictionary<string, double> { ["Billing"] = 1 });

        AcceptWhen.ConfidenceAtLeast(1)(IntentQuestion, answer).ShouldBeTrue();
    }

    [Fact]
    public void ConfidenceAtLeast_ClassificationOverTwoOfThreeOptions_StillDividesByThree()
    {
        var answer = new ClassifyAnswerSpec("intent", new Dictionary<string, double> { ["Billing"] = 0.8, ["Tracking"] = 0.2 });

        Straddles(IntentQuestion, answer, 0.7).ShouldBe((true, false));
    }

    [Fact]
    public void ConfidenceAtLeast_SingleOptionQuestion_IsAlwaysCertain()
    {
        var question = new ClassifySpec("choice", "Pick one", [new OptionSpec("Only", "The only one")]);
        var answer = new ClassifyAnswerSpec("choice", new Dictionary<string, double> { ["Only"] = 1 });

        AcceptWhen.ConfidenceAtLeast(1)(question, answer).ShouldBeTrue();
    }

    [Fact]
    public void ConfidenceAtLeast_AnswerWithNoMass_IsNotAccepted()
    {
        var answer = new ClassifyAnswerSpec("intent", new Dictionary<string, double> { ["Billing"] = 0 });

        AcceptWhen.ConfidenceAtLeast(0.01)(IntentQuestion, answer).ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(Malformed))]
    public void ConfidenceAtLeast_MalformedDistribution_IsNeverAccepted(Dictionary<string, double> probabilities) =>
        AcceptWhen.ConfidenceAtLeast(0.01)(IntentQuestion, new ClassifyAnswerSpec("intent", probabilities)).ShouldBeFalse();

    public static TheoryData<Dictionary<string, double>> Malformed() =>
    [
        new Dictionary<string, double>(),
        new Dictionary<string, double> { ["Billing"] = double.NaN, ["Tracking"] = 1 },
        new Dictionary<string, double> { ["Billing"] = double.PositiveInfinity },
        new Dictionary<string, double> { ["Billing"] = 1.2, ["Tracking"] = -0.2 },
        new Dictionary<string, double> { ["Billing"] = 8, ["Tracking"] = 1.5, ["Returns"] = 0.5 },
        new Dictionary<string, double> { ["Billing"] = 0.5, ["Tracking"] = 0.4 },
    ];

    private static (bool AtTheFloor, bool Above) Straddles(QuestionSpec question, AnswerSpec answer, double confidence) =>
        (AcceptWhen.ConfidenceAtLeast(Math.Max(0d, confidence - Epsilon))(question, answer),
            AcceptWhen.ConfidenceAtLeast(confidence + Epsilon)(question, answer));
}

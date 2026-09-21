namespace Adjudge.Tests.Core;

public sealed class DecisionValidationTests
{
    [Fact]
    public void Build_WithoutTheDecisionAttribute_NamesTheDecisionAfterItsType()
    {
        var definition = DecisionDefinition<Ticket, IntentOnly>.Build(new UnnamedDecision());

        definition.Id.ShouldBe("UnnamedDecision");
    }

    [Fact]
    public void Build_MemberBoundTwice_Throws()
    {
        var build = () => DecisionDefinition<Ticket, IntentOnly>.Build(new DuplicateMemberDecision());

        build.ShouldThrow<DecisionDefinitionException>().Message.ShouldContain("bound more than once");
    }

    [Fact]
    public void Build_ConstructorParameterWithoutQuestion_Throws()
    {
        var build = () => DecisionDefinition<Ticket, Unanswered>.Build(new UnansweredParameterDecision());

        build.ShouldThrow<DecisionDefinitionException>().Message.ShouldContain("has no question");
    }

    [Fact]
    public void Build_MemberMatchingNoConstructorParameter_Throws()
    {
        var build = () => DecisionDefinition<Ticket, Unbound>.Build(new UnboundMemberDecision());

        build.ShouldThrow<DecisionDefinitionException>().Message.ShouldContain("matches no constructor parameter");
    }

    [Fact]
    public void Build_RateWithFewerThanTwoLevels_Throws()
    {
        var build = () => DecisionDefinition<Ticket, SoloRating>.Build(new TooFewLevelsDecision());

        build.ShouldThrow<DecisionDefinitionException>().Message.ShouldContain("between 2 and 10");
    }

    [Fact]
    public void Build_RateWithMoreThanTenLevels_Throws()
    {
        var build = () => DecisionDefinition<Ticket, ElevenRating>.Build(new TooManyLevelsDecision());

        build.ShouldThrow<DecisionDefinitionException>().Message.ShouldContain("between 2 and 10");
    }

    [Fact]
    public void Build_ClassifyWithNoOptions_Throws()
    {
        var build = () => DecisionDefinition<Ticket, BlankClassification>.Build(new NoOptionsDecision());

        build.ShouldThrow<DecisionDefinitionException>().Message.ShouldContain("no members");
    }

    [Fact]
    public void Build_ClassifyWithMoreThanTwoHundredAndFiftyFiveOptions_Throws()
    {
        var build = () => DecisionDefinition<Ticket, ManyClassification>.Build(new TooManyOptionsDecision());

        build.ShouldThrow<DecisionDefinitionException>().Message.ShouldContain("at most 255");
    }
}

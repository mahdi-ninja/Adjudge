using Adjudge.Providers;

namespace Adjudge.Tests.Core;

public sealed class DecisionBuilderTests
{
    [Fact]
    public void Build_BoundMembers_NamesQuestionsInCamelCase()
    {
        var definition = DecisionDefinition<Ticket, Triage>.Build(new TriageDecision());

        definition.Questions.Select(q => q.Name).ShouldBe(["intent", "urgency", "abusive"]);
    }

    [Fact]
    public void Build_DecisionAttribute_CarriesTheDeclaredId()
    {
        var definition = DecisionDefinition<Ticket, Triage>.Build(new TriageDecision());

        definition.Id.ShouldBe("support.ticket-triage");
    }

    [Fact]
    public void Build_ClassifyOptions_AreOrderedByAscendingUnderlyingValue()
    {
        var definition = DecisionDefinition<Ticket, PriorityOnly>.Build(new PriorityOnlyDecision());

        var options = definition.Questions.OfType<ClassifySpec>().Single().Options;

        options.Select(o => o.Key).ShouldBe(["High", "Low"]);
    }

    [Fact]
    public void Build_RateLevels_AreOrderedByAscendingUnderlyingValue()
    {
        var definition = DecisionDefinition<Ticket, PriorityRating>.Build(new PriorityRatingDecision());

        var levels = definition.Questions.OfType<RateSpec>().Single().Levels;

        levels.Select(l => l.Key).ShouldBe(["High", "Low"]);
    }

    [Fact]
    public void Build_OptionWithDescriptionOnly_UsesThePlainString()
    {
        var definition = DecisionDefinition<Ticket, IntentOnly>.Build(new IntentOnlyDecision());

        var options = definition.Questions.OfType<ClassifySpec>().Single().Options;

        options.Single(o => o.Key == "Tracking").Rubric.ShouldBe("Where an existing order is");
    }

    [Fact]
    public void Build_OptionWithNotForAndExamples_UsesStructuredRubric()
    {
        var definition = DecisionDefinition<Ticket, IntentOnly>.Build(new IntentOnlyDecision());

        var options = definition.Questions.OfType<ClassifySpec>().Single().Options;

        var rubric = options.Single(o => o.Key == "Billing").Rubric.ShouldBeOfType<OptionRubric>();

        (rubric.Description, rubric.NotFor).ShouldBe(("Charges, invoices, refunds", "Order tracking"));
    }

    [Fact]
    public void Build_OptionWithExamples_CarriesThemOnTheRubric()
    {
        var definition = DecisionDefinition<Ticket, IntentOnly>.Build(new IntentOnlyDecision());

        var options = definition.Questions.OfType<ClassifySpec>().Single().Options;
        var rubric = options.Single(o => o.Key == "Billing").Rubric.ShouldBeOfType<OptionRubric>();

        rubric.Examples.ShouldBe(["I was charged twice"]);
    }

    [Fact]
    public void Build_MemberWithoutAttribute_UsesTheMemberNameAsRubric()
    {
        var definition = DecisionDefinition<Ticket, IntentOnly>.Build(new IntentOnlyDecision());

        var options = definition.Questions.OfType<ClassifySpec>().Single().Options;

        options.Single(o => o.Key == "Returns").Rubric.ShouldBe("Returns");
    }

    [Fact]
    public void Build_LevelWithoutAttribute_UsesTheMemberNameAsRubric()
    {
        var definition = DecisionDefinition<Ticket, UrgencyOnly>.Build(new UrgencyOnlyDecision());

        var levels = definition.Questions.OfType<RateSpec>().Single().Levels;

        levels.Single(l => l.Key == "Medium").Rubric.ShouldBe("Medium");
    }

    [Fact]
    public void Build_LevelWithAttribute_UsesTheDescription()
    {
        var definition = DecisionDefinition<Ticket, UrgencyOnly>.Build(new UrgencyOnlyDecision());

        var levels = definition.Questions.OfType<RateSpec>().Single().Levels;

        levels.Single(l => l.Key == "High").Rubric.ShouldBe("Needs attention now");
    }

    [Fact]
    public void Build_AssertQuestion_CarriesTrueAndFalseMeanings()
    {
        var definition = DecisionDefinition<Ticket, Triage>.Build(new TriageDecision());

        var assert = definition.Questions.OfType<AssertSpec>().Single();

        (assert.TrueMeans, assert.FalseMeans).ShouldBe(("Insults or threats", "Frustrated but civil"));
    }

    [Fact]
    public void Build_QuestionKinds_AggregateTheRequiredCapabilities()
    {
        var definition = DecisionDefinition<Ticket, Triage>.Build(new TriageDecision());

        definition.RequiredCapabilities
            .ShouldBe(DecisionCapabilities.Classify | DecisionCapabilities.Rate | DecisionCapabilities.Assert);
    }
}

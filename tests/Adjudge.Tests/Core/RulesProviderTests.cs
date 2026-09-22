using Adjudge.Providers;
using static Adjudge.Tests.Core.CascadeFixtures;

namespace Adjudge.Tests.Core;

public sealed class RulesProviderTests
{
    private static readonly Ticket Context = new("I was charged twice");

    [Fact]
    public async Task DecideAsync_ClassifyWithOneMatchingRule_PutsAllTheMassOnThatOption()
    {
        var provider = RulesProvider.Create<Ticket>(rules => rules
            .Classify<Intent>("intent", b => b.When(Intent.Billing, t => t.Message.Contains("charged"))));

        var response = await DecideAsync(provider, IntentQuestion);

        var answer = (ClassifyAnswerSpec)response.Answers["intent"];
        answer.Probabilities.ShouldBe(new Dictionary<string, double>
        {
            ["Billing"] = 1,
            ["Tracking"] = 0,
            ["Returns"] = 0,
        });
    }

    [Fact]
    public async Task DecideAsync_ClassifyWithOneMatchingRule_ReportsNoConfidenceAndAHeuristicSource()
    {
        var provider = RulesProvider.Create<Ticket>(rules => rules
            .Classify<Intent>("intent", b => b.When(Intent.Billing, t => t.Message.Contains("charged"))));

        var response = await DecideAsync(provider, IntentQuestion);

        var answer = (ClassifyAnswerSpec)response.Answers["intent"];
        (answer.Confidence, answer.Source).ShouldBe((null, ConfidenceSource.Heuristic));
    }

    [Fact]
    public async Task DecideAsync_ClassifyWithSeveralRulesForOneOption_TreatsThemAsOneMatch()
    {
        var provider = RulesProvider.Create<Ticket>(rules => rules.Classify<Intent>("intent", b => b
            .When(Intent.Billing, t => t.Message.Contains("charged"))
            .When(Intent.Billing, t => t.Message.Contains("twice"))));

        var response = await DecideAsync(provider, IntentQuestion);

        ((ClassifyAnswerSpec)response.Answers["intent"]).Probabilities["Billing"].ShouldBe(1);
    }

    [Fact]
    public async Task DecideAsync_ClassifyWithNoMatchingRule_DeclinesTheQuestion()
    {
        var provider = RulesProvider.Create<Ticket>(rules => rules
            .Classify<Intent>("intent", b => b.When(Intent.Tracking, t => t.Message.Contains("where"))));

        var response = await DecideAsync(provider, IntentQuestion);

        response.Answers.ShouldNotContainKey("intent");
    }

    [Fact]
    public async Task DecideAsync_ClassifyWithTwoOptionsMatching_DeclinesTheQuestion()
    {
        var provider = RulesProvider.Create<Ticket>(rules => rules.Classify<Intent>("intent", b => b
            .When(Intent.Billing, _ => true)
            .When(Intent.Returns, _ => true)));

        var response = await DecideAsync(provider, IntentQuestion);

        response.Answers.ShouldNotContainKey("intent");
    }

    [Fact]
    public async Task DecideAsync_AssertWhoseTrueRuleHolds_AnswersOne()
    {
        var provider = RulesProvider.Create<Ticket>(rules => rules.Assert("abusive", t => t.Message.Contains("charged")));

        var response = await DecideAsync(provider, AbusiveQuestion);

        ((AssertAnswerSpec)response.Answers["abusive"]).Probability.ShouldBe(1);
    }

    [Fact]
    public async Task DecideAsync_AssertWhoseFalseRuleHolds_AnswersZero()
    {
        var provider = RulesProvider.Create<Ticket>(rules => rules
            .Assert("abusive", _ => false, t => t.Message.Contains("charged")));

        var response = await DecideAsync(provider, AbusiveQuestion);

        ((AssertAnswerSpec)response.Answers["abusive"]).Probability.ShouldBe(0);
    }

    [Fact]
    public async Task DecideAsync_AssertWithBothRulesHolding_DeclinesTheQuestion()
    {
        var provider = RulesProvider.Create<Ticket>(rules => rules.Assert("abusive", _ => true, _ => true));

        var response = await DecideAsync(provider, AbusiveQuestion);

        response.Answers.ShouldNotContainKey("abusive");
    }

    [Fact]
    public async Task DecideAsync_AssertWithNeitherRuleHolding_DeclinesTheQuestion()
    {
        var provider = RulesProvider.Create<Ticket>(rules => rules.Assert("abusive", _ => false));

        var response = await DecideAsync(provider, AbusiveQuestion);

        response.Answers.ShouldNotContainKey("abusive");
    }

    [Fact]
    public async Task DecideAsync_QuestionWithNoRules_DeclinesIt()
    {
        var response = await DecideAsync(RulesProvider.Create<Ticket>(_ => { }), IntentQuestion);

        response.Answers.ShouldBeEmpty();
    }

    [Fact]
    public async Task DecideAsync_RateQuestion_DeclinesItWhateverIsRegisteredUnderTheName()
    {
        var provider = RulesProvider.Create<Ticket>(rules => rules
            .Classify<Urgency>("urgency", b => b.When(Urgency.High, _ => true)));

        var response = await DecideAsync(provider, UrgencyQuestion);

        response.Answers.ShouldBeEmpty();
    }

    [Fact]
    public async Task DecideAsync_AnsweredQuestions_AreCountedInMetadata()
    {
        var provider = RulesProvider.Create<Ticket>(rules => rules
            .Classify<Intent>("intent", b => b.When(Intent.Billing, _ => true))
            .Assert("abusive", _ => false));

        var response = await DecideAsync(provider, IntentQuestion, AbusiveQuestion);

        response.Metadata.ShouldNotBeNull()["rules.matched"].ShouldBe("1");
    }

    [Fact]
    public async Task DecideAsync_BehindACascade_ReportsTheRuleCountUnderTheStagePrefix()
    {
        var rules = RulesProvider.Create<Ticket>(r => r.Classify<Intent>("intent", b => b.When(Intent.Billing, _ => true)));
        var cascade = new CascadeProvider(
            new CascadeStage(rules),
            new CascadeStage(new Testing.FakeDecisionProvider()));

        var response = await cascade.DecideAsync(Request(IntentQuestion), TestContext.Current.CancellationToken);

        response.Metadata!["stage.0.rules.matched"].ShouldBe("1");
    }

    [Fact]
    public async Task DecideAsync_ContextOfAnotherType_ThrowsDecisionDefinitionException()
    {
        var provider = RulesProvider.Create<Ticket>(rules => rules
            .Classify<Intent>("intent", b => b.When(Intent.Billing, _ => true)));
        var request = new ProviderRequest(
            DecisionContext.FromJson("not a ticket", default),
            [IntentQuestion],
            "test.rules-triage");

        var decide = async () => await provider.DecideAsync(request, TestContext.Current.CancellationToken);

        (await decide.ShouldThrowAsync<DecisionDefinitionException>()).Message.ShouldContain("Ticket");
    }

    [Fact]
    public async Task DecideAsync_OptionsThatAreNotTheEnumTheRulesWereWrittenOver_ThrowsDecisionDefinitionException()
    {
        var provider = RulesProvider.Create<Ticket>(rules => rules
            .Classify<Priority>("intent", b => b.When(Priority.High, _ => true)));

        var decide = async () => await DecideAsync(provider, IntentQuestion);

        (await decide.ShouldThrowAsync<DecisionDefinitionException>()).Message.ShouldContain("High");
    }

    [Fact]
    public void Create_QuestionRegisteredTwice_ThrowsArgumentException()
    {
        var create = () => RulesProvider.Create<Ticket>(rules => rules
            .Classify<Intent>("intent", b => b.When(Intent.Billing, _ => true))
            .Classify<Intent>("intent", b => b.When(Intent.Returns, _ => true)));

        create.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Create_QuestionAlreadyRegisteredAsClassify_ThrowsArgumentException()
    {
        var create = () => RulesProvider.Create<Ticket>(rules => rules
            .Classify<Intent>("intent", b => b.When(Intent.Billing, _ => true))
            .Assert("intent", _ => true));

        create.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Create_RuleNamingAnOptionThatIsNotAnEnumMember_ThrowsArgumentException()
    {
        var create = () => RulesProvider.Create<Ticket>(rules => rules
            .Classify<Intent>("intent", b => b.When((Intent)42, _ => true)));

        create.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void RulesProvider_OnceCreated_OffersNoRegistrationApi() =>
        typeof(RulesProvider<Ticket>)
            .GetMethods()
            .Select(m => m.Name)
            .ShouldNotContain(name => name == "Classify" || name == "Assert");

    [Fact]
    public async Task DecideAsync_PredicateThatThrows_LetsTheExceptionThrough()
    {
        var provider = RulesProvider.Create<Ticket>(rules => rules
            .Classify<Intent>("intent", b => b.When(Intent.Billing, _ => throw new InvalidTimeZoneException("rule bug"))));

        var decide = async () => await DecideAsync(provider, IntentQuestion);

        (await decide.ShouldThrowAsync<InvalidTimeZoneException>()).Message.ShouldBe("rule bug");
    }

    [Fact]
    public async Task DecideAsync_ThroughTheEngineWithEveryQuestionCovered_ReturnsTypedAnswers()
    {
        var provider = RulesProvider.Create<Ticket>(rules => rules
            .Classify<Intent>("intent", b => b.When(Intent.Billing, t => t.Message.Contains("charged")))
            .Assert("abusive", _ => false, _ => true));
        var decision = new DecisionEngine(provider).Create<RuledTriageDecision, Ticket, RuledTriage>();

        var result = await decision.DecideAsync(Context, TestContext.Current.CancellationToken);

        (result.Value.Intent.Value, result.Value.Abusive.Probability).ShouldBe((Intent.Billing, 0));
    }

    [Fact]
    public async Task DecideAsync_ThroughTheEngine_ReportsTheConfidenceSourceAsHeuristic()
    {
        var provider = RulesProvider.Create<Ticket>(rules => rules
            .Classify<Intent>("intent", b => b.When(Intent.Billing, t => t.Message.Contains("charged")))
            .Assert("abusive", _ => false, _ => true));
        var decision = new DecisionEngine(provider).Create<RuledTriageDecision, Ticket, RuledTriage>();

        var result = await decision.DecideAsync(Context, TestContext.Current.CancellationToken);

        result.Value.Intent.Confidence.Source.ShouldBe(ConfidenceSource.Heuristic);
    }

    [Fact]
    public async Task DecideAsync_ThroughTheEngineWithARulesOnlyProviderDeclining_ThrowsProviderResponseException()
    {
        var provider = RulesProvider.Create<Ticket>(rules => rules
            .Classify<Intent>("intent", b => b.When(Intent.Billing, t => t.Message.Contains("charged"))));
        var decision = new DecisionEngine(provider).Create<RuledTriageDecision, Ticket, RuledTriage>();

        var decide = async () => await decision.DecideAsync(Context, TestContext.Current.CancellationToken);

        await decide.ShouldThrowAsync<ProviderResponseException>();
    }

    private static Task<ProviderResponse> DecideAsync(RulesProvider<Ticket> provider, params QuestionSpec[] questions) =>
        provider.DecideAsync(
            new ProviderRequest(DecisionContext.FromJson(Context, default), questions, "test.rules-triage"),
            TestContext.Current.CancellationToken);
}

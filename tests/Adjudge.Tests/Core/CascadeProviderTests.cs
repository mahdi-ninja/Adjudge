using Adjudge.Providers;
using Adjudge.Testing;
using static Adjudge.Tests.Core.CascadeFixtures;

namespace Adjudge.Tests.Core;

[Collection(CascadeTestGroup.Name)]
public sealed class CascadeProviderTests
{
    [Fact]
    public void Constructor_WithNoStages_Throws() =>
        Should.Throw<ArgumentException>(() => new CascadeProvider());

    [Fact]
    public void Name_IsCascade() =>
        Single().Name.ShouldBe("cascade");

    [Fact]
    public void Capabilities_AreTheKindsOfTheLastStage()
    {
        var cascade = new CascadeProvider(
            new CascadeStage(new StubProvider { Capabilities = DecisionCapabilities.Classify | DecisionCapabilities.Batch }),
            new CascadeStage(new StubProvider { Capabilities = DecisionCapabilities.Classify | DecisionCapabilities.Assert }));

        cascade.Capabilities.ShouldBe(DecisionCapabilities.Classify | DecisionCapabilities.Assert);
    }

    [Fact]
    public void Capabilities_NativeConfidenceAndBatch_NeedEveryStage()
    {
        var everything = DecisionCapabilities.Classify | DecisionCapabilities.NativeConfidence | DecisionCapabilities.Batch;
        var cascade = new CascadeProvider(
            new CascadeStage(new StubProvider { Capabilities = everything }),
            new CascadeStage(new StubProvider { Capabilities = DecisionCapabilities.Classify | DecisionCapabilities.NativeConfidence }));

        cascade.Capabilities.ShouldBe(DecisionCapabilities.Classify | DecisionCapabilities.NativeConfidence);
    }

    [Fact]
    public void Constructor_WhenTheLastStageIsNarrowerThanAnEarlierOne_Throws()
    {
        var wide = new CascadeStage(new StubProvider { Name = "wide", Capabilities = DecisionCapabilities.Classify | DecisionCapabilities.Rate });
        var narrow = new CascadeStage(new StubProvider { Name = "narrow", Capabilities = DecisionCapabilities.Classify });

        Should.Throw<ArgumentException>(() => new CascadeProvider(wide, narrow)).Message.ShouldContain("Rate");
    }

    [Fact]
    public async Task DecideAsync_WhenTheFirstStageDeclines_TakesTheAnswerFromTheSecond()
    {
        var first = new FakeDecisionProvider().Classify("intent", "Billing").Declines("urgency");
        var second = new FakeDecisionProvider().Rate("urgency", "High");

        var response = await DecideAsync(Cascade(first, second), IntentQuestion, UrgencyQuestion);

        response.Answers.Keys.OrderBy(k => k, StringComparer.Ordinal).ShouldBe(["intent", "urgency"]);
    }

    [Fact]
    public async Task DecideAsync_WhenTheFirstStageAnswers_KeepsItsAnswer()
    {
        var first = new FakeDecisionProvider().Classify("intent", "Billing");
        var second = new FakeDecisionProvider().Classify("intent", "Returns");

        var response = await DecideAsync(Cascade(first, second), IntentQuestion);

        ((ClassifyAnswerSpec)response.Answers["intent"]).Probabilities["Billing"].ShouldBeGreaterThan(0.5);
    }

    [Fact]
    public async Task DecideAsync_WhenTheFirstStageAnswersEverything_NeverCallsTheSecond()
    {
        var second = new FakeDecisionProvider();

        await DecideAsync(Cascade(new FakeDecisionProvider().Classify("intent", "Billing"), second), IntentQuestion);

        second.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task DecideAsync_WhenAnAnswerFailsAcceptance_FallsThroughToTheNextStage()
    {
        var first = new FakeDecisionProvider().Classify("intent", "Billing", confidence: 0.1);
        var second = new FakeDecisionProvider().Classify("intent", "Returns", confidence: 0.95);
        var cascade = new CascadeProvider(
            new CascadeStage(first, AcceptWhen.ConfidenceAtLeast(0.5)),
            new CascadeStage(second));

        var response = await DecideAsync(cascade, IntentQuestion);

        ((ClassifyAnswerSpec)response.Answers["intent"]).Probabilities["Returns"].ShouldBeGreaterThan(0.5);
    }

    [Fact]
    public async Task DecideAsync_WhenAnAnswerFailsAcceptance_ReportsTheAnsweringStage()
    {
        var cascade = new CascadeProvider(
            new CascadeStage(new StubProvider { Name = "cheap", Response = Classified(0.34, 0.33, 0.33) }, AcceptWhen.ConfidenceAtLeast(0.5)),
            new CascadeStage(new StubProvider { Name = "strong", Response = Classified(0.9, 0.05, 0.05) }));

        var response = await DecideAsync(cascade, IntentQuestion);

        response.Metadata!["question.intent.provider"].ShouldBe("strong");
    }

    [Fact]
    public async Task DecideAsync_WhenTheLastStageAnswersBelowTheBar_AcceptsItAnyway()
    {
        var cascade = new CascadeProvider(
            new CascadeStage(new StubProvider { Name = "cheap", Response = Classified(0.34, 0.33, 0.33) }, AcceptWhen.ConfidenceAtLeast(0.5)),
            new CascadeStage(new StubProvider { Name = "strong", Response = Classified(0.34, 0.33, 0.33) }, AcceptWhen.ConfidenceAtLeast(0.5)));

        var response = await DecideAsync(cascade, IntentQuestion);

        response.Metadata!["question.intent.provider"].ShouldBe("strong");
    }

    [Fact]
    public async Task DecideAsync_WhenTheLastStageOmitsAQuestion_ThrowsNamingIt()
    {
        var cascade = Cascade(
            new FakeDecisionProvider().Declines("urgency"),
            new FakeDecisionProvider().Declines("urgency"));

        var exception = await Should.ThrowAsync<ProviderResponseException>(
            () => DecideAsync(cascade, UrgencyQuestion));

        exception.Message.ShouldContain("urgency");
    }

    [Fact]
    public async Task DecideAsync_WhenTheLastStageOmitsAQuestion_AttributesTheFailureToTheCascade()
    {
        var cascade = Cascade(new FakeDecisionProvider().Declines("urgency"));

        var exception = await Should.ThrowAsync<ProviderResponseException>(
            () => DecideAsync(cascade, UrgencyQuestion));

        exception.Provider.ShouldBe("cascade");
    }

    [Fact]
    public async Task DecideAsync_StageWithoutTheQuestionKind_IsNotAskedIt()
    {
        var narrow = new StubProvider { Capabilities = DecisionCapabilities.Classify, Response = Classified(0.9, 0.05, 0.05) };
        var cascade = Cascade(narrow, new FakeDecisionProvider().Assert("abusive", 0.8));

        await DecideAsync(cascade, IntentQuestion, AbusiveQuestion);

        narrow.Requests.Single().Questions.Select(q => q.Name).ShouldBe(["intent"]);
    }

    [Fact]
    public async Task DecideAsync_StageSupportingNoneOfTheOpenQuestions_IsNotCalled()
    {
        var narrow = new StubProvider { Capabilities = DecisionCapabilities.Classify };
        var cascade = Cascade(narrow, new FakeDecisionProvider().Assert("abusive", 0.8));

        await DecideAsync(cascade, AbusiveQuestion);

        narrow.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task DecideAsync_LaterStage_IsAskedOnlyTheOpenQuestions()
    {
        var first = new FakeDecisionProvider().Classify("intent", "Billing").Declines("urgency");
        var second = new FakeDecisionProvider().Rate("urgency", "High");

        await DecideAsync(Cascade(first, second), IntentQuestion, UrgencyQuestion);

        second.Requests.Single().Questions.Select(q => q.Name).ShouldBe(["urgency"]);
    }

    [Fact]
    public async Task DecideAsync_AnswerForAQuestionThatWasNotAsked_IsIgnored()
    {
        var chatty = new StubProvider
        {
            Capabilities = DecisionCapabilities.Classify,
            Response = new ProviderResponse(new Dictionary<string, AnswerSpec>
            {
                ["intent"] = Intent(0.9, 0.05, 0.05),
                ["urgency"] = new RateAnswerSpec("urgency", new Dictionary<string, double> { ["High"] = 1 }),
            }),
        };

        var response = await DecideAsync(Cascade(chatty), IntentQuestion);

        response.Answers.Keys.ShouldBe(["intent"]);
    }

    [Fact]
    public async Task DecideAsync_TransientFailureWithFallThrough_CarriesOnToTheNextStage()
    {
        var cascade = new CascadeProvider(
            new CascadeStage(Failing(transient: true)) { FallThroughOnError = true },
            new CascadeStage(new FakeDecisionProvider().Classify("intent", "Billing")));

        var response = await DecideAsync(cascade, IntentQuestion);

        response.Answers.ShouldContainKey("intent");
    }

    [Fact]
    public async Task DecideAsync_TransientFailureWithFallThrough_RecordsTheFailureInMetadata()
    {
        var cascade = new CascadeProvider(
            new CascadeStage(Failing(transient: true)) { FallThroughOnError = true },
            new CascadeStage(new FakeDecisionProvider().Classify("intent", "Billing")));

        var response = await DecideAsync(cascade, IntentQuestion);

        response.Metadata!["stage.0.error"].ShouldBe("upstream is busy");
    }

    [Fact]
    public async Task DecideAsync_TransientFailureWithoutFallThrough_Propagates()
    {
        var cascade = Cascade(Failing(transient: true), new FakeDecisionProvider().Classify("intent", "Billing"));

        await Should.ThrowAsync<ProviderException>(() => DecideAsync(cascade, IntentQuestion));
    }

    [Fact]
    public async Task DecideAsync_NonTransientFailureWithFallThrough_Propagates()
    {
        var cascade = new CascadeProvider(
            new CascadeStage(Failing(transient: false)) { FallThroughOnError = true },
            new CascadeStage(new FakeDecisionProvider().Classify("intent", "Billing")));

        await Should.ThrowAsync<ProviderException>(() => DecideAsync(cascade, IntentQuestion));
    }

    [Fact]
    public async Task DecideAsync_CancellationWithFallThrough_Propagates()
    {
        var cascade = new CascadeProvider(
            new CascadeStage(new FakeDecisionProvider().AlwaysThrows(new OperationCanceledException())) { FallThroughOnError = true },
            new CascadeStage(new FakeDecisionProvider().Classify("intent", "Billing")));

        await Should.ThrowAsync<OperationCanceledException>(() => DecideAsync(cascade, IntentQuestion));
    }

    [Fact]
    public async Task DecideAsync_CancelledToken_ThrowsBeforeAnyStageIsCalled()
    {
        var stage = new FakeDecisionProvider().Classify("intent", "Billing");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => Cascade(stage).DecideAsync(Request(IntentQuestion), cancellation.Token));
        stage.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task DecideAsync_StagesReportingUsage_SumsIt()
    {
        var first = new FakeDecisionProvider().Classify("intent", "Billing").Declines("urgency").WithUsage(10, 2);
        var second = new FakeDecisionProvider().Rate("urgency", "High").WithUsage(5, 3);

        var response = await DecideAsync(Cascade(first, second), IntentQuestion, UrgencyQuestion);

        response.Usage.ShouldBe(new Usage(15, 5));
    }

    [Fact]
    public async Task DecideAsync_NoStageReportingUsage_LeavesUsageNull()
    {
        var response = await DecideAsync(Cascade(new FakeDecisionProvider().Classify("intent", "Billing")), IntentQuestion);

        response.Usage.ShouldBeNull();
    }

    [Fact]
    public async Task DecideAsync_OneAnsweringStageReportingAModel_ReportsThatModel()
    {
        var first = new FakeDecisionProvider().Classify("intent", "Billing").WithModel("cheap-1");

        var response = await DecideAsync(Cascade(first, new FakeDecisionProvider()), IntentQuestion);

        response.Model.ShouldBe("cheap-1");
    }

    [Fact]
    public async Task DecideAsync_AnsweringStagesReportingDifferentModels_LeavesTheModelNull()
    {
        var first = new FakeDecisionProvider().Classify("intent", "Billing").Declines("urgency").WithModel("cheap-1");
        var second = new FakeDecisionProvider().Rate("urgency", "High").WithModel("strong-1");

        var response = await DecideAsync(Cascade(first, second), IntentQuestion, UrgencyQuestion);

        response.Model.ShouldBeNull();
    }

    [Fact]
    public async Task DecideAsync_Metadata_NamesTheStageThatAnsweredEachQuestion()
    {
        var first = new StubProvider { Name = "cheap", Response = Classified(0.9, 0.05, 0.05) };
        var second = new StubProvider
        {
            Name = "strong",
            Response = new ProviderResponse(new Dictionary<string, AnswerSpec> { ["abusive"] = new AssertAnswerSpec("abusive", 0.8) }),
        };

        var response = await DecideAsync(Cascade(first, second), IntentQuestion, AbusiveQuestion);

        (response.Metadata!["question.intent.provider"], response.Metadata["question.abusive.provider"]).ShouldBe(("cheap", "strong"));
    }

    [Fact]
    public async Task DecideAsync_Metadata_CountsTheStagesCalled()
    {
        var first = new FakeDecisionProvider().Classify("intent", "Billing").Declines("urgency");
        var second = new FakeDecisionProvider().Rate("urgency", "High");

        var response = await DecideAsync(Cascade(first, second), IntentQuestion, UrgencyQuestion);

        response.Metadata!["stages.called"].ShouldBe("2");
    }

    [Fact]
    public async Task DecideAsync_Metadata_DoesNotCountAStageThatWasSkipped()
    {
        var first = new FakeDecisionProvider().Classify("intent", "Billing");
        var second = new FakeDecisionProvider();

        var response = await DecideAsync(Cascade(first, second), IntentQuestion);

        response.Metadata!["stages.called"].ShouldBe("1");
    }


    [Fact]
    public async Task DecideAsync_Metadata_RecordsTheStageIndexPerQuestion()
    {
        var first = new FakeDecisionProvider().Classify("intent", "Billing").Declines("urgency");
        var second = new FakeDecisionProvider().Rate("urgency", "High");

        var response = await DecideAsync(Cascade(first, second), IntentQuestion, UrgencyQuestion);

        (response.Metadata!["question.intent.stage"], response.Metadata["question.urgency.stage"]).ShouldBe(("0", "1"));
    }

    [Fact]
    public async Task DecideAsync_Metadata_NamesTheProviderOfEveryStageCalled()
    {
        var first = new StubProvider { Name = "cheap", Response = Classified(0.34, 0.33, 0.33) };
        var second = new StubProvider { Name = "strong", Response = Classified(0.9, 0.05, 0.05) };
        var cascade = new CascadeProvider(
            new CascadeStage(first, AcceptWhen.ConfidenceAtLeast(0.5)),
            new CascadeStage(second));

        var response = await DecideAsync(cascade, IntentQuestion);

        (response.Metadata!["stage.0.provider"], response.Metadata["stage.1.provider"]).ShouldBe(("cheap", "strong"));
    }

    [Fact]
    public async Task DecideAsync_StagesSharingAProviderName_AreToldApartByIndex()
    {
        var first = new StubProvider { Name = "twin", Response = Classified(0.34, 0.33, 0.33) };
        var second = new StubProvider { Name = "twin", Response = Classified(0.9, 0.05, 0.05) };
        var cascade = new CascadeProvider(
            new CascadeStage(first, AcceptWhen.ConfidenceAtLeast(0.5)),
            new CascadeStage(second));

        var response = await DecideAsync(cascade, IntentQuestion);

        (response.Metadata!["stage.0.provider"], response.Metadata["stage.1.provider"], response.Metadata["question.intent.stage"])
            .ShouldBe(("twin", "twin", "1"));
    }

    [Fact]
    public async Task DecideAsync_StageReportingMetadata_CarriesItUnderTheStagePrefix()
    {
        var reporting = new StubProvider
        {
            Name = "cheap",
            Response = new ProviderResponse(
                new Dictionary<string, AnswerSpec> { ["intent"] = Intent(0.9, 0.05, 0.05) },
                Metadata: new Dictionary<string, string> { ["request_id"] = "abc-123" }),
        };

        var response = await DecideAsync(Cascade(reporting), IntentQuestion);

        response.Metadata!["stage.0.request_id"].ShouldBe("abc-123");
    }

    [Fact]
    public async Task DecideAsync_StagesReportingHalfTheirUsage_SumsWhatWasReported()
    {
        var first = new StubProvider
        {
            Name = "cheap",
            Response = new ProviderResponse(
                new Dictionary<string, AnswerSpec> { ["intent"] = Intent(0.9, 0.05, 0.05) },
                Usage: new Usage(10, null)),
        };
        var second = new StubProvider
        {
            Name = "strong",
            Response = new ProviderResponse(
                new Dictionary<string, AnswerSpec> { ["abusive"] = new AssertAnswerSpec("abusive", 0.8) },
                Usage: new Usage(null, 3)),
        };

        var response = await DecideAsync(Cascade(first, second), IntentQuestion, AbusiveQuestion);

        response.Usage.ShouldBe(new Usage(10, 3));
    }

    [Fact]
    public async Task DecideAsync_AnsweringStageWithoutAModel_LeavesTheModelNull()
    {
        var first = new FakeDecisionProvider().Classify("intent", "Billing").Declines("urgency");
        var second = new FakeDecisionProvider().Rate("urgency", "High").WithModel("strong-1");

        var response = await DecideAsync(Cascade(first, second), IntentQuestion, UrgencyQuestion);

        response.Model.ShouldBeNull();
    }

    [Fact]
    public async Task DecideAsync_StageReturningNoResponse_ThrowsNamingTheProvider()
    {
        var silent = new StubProvider { Name = "silent", Response = null! };

        var exception = await Should.ThrowAsync<ProviderResponseException>(() => DecideAsync(Cascade(silent), IntentQuestion));

        exception.Provider.ShouldBe("silent");
    }

    [Fact]
    public async Task DecideAsync_ResponseExceptionOnANonLastStageWithFallThrough_CarriesOnToTheNextStage()
    {
        var cascade = new CascadeProvider(
            new CascadeStage(new StubProvider { Name = "nonsense", Throws = new ProviderResponseException("nonsense", "nonsense") })
            {
                FallThroughOnError = true,
            },
            new CascadeStage(new FakeDecisionProvider().Classify("intent", "Billing")));

        var response = await DecideAsync(cascade, IntentQuestion);

        (response.Answers.ContainsKey("intent"), response.Metadata!["stage.0.error"]).ShouldBe((true, "nonsense"));
    }

    [Fact]
    public async Task DecideAsync_LastStageWithFallThrough_StillPropagatesItsFailure()
    {
        var cascade = new CascadeProvider(
            new CascadeStage(new FakeDecisionProvider().Classify("intent", "Billing").Declines("urgency")),
            new CascadeStage(Failing(transient: true)) { FallThroughOnError = true });

        await Should.ThrowAsync<ProviderException>(() => DecideAsync(cascade, IntentQuestion, UrgencyQuestion));
    }

    [Fact]
    public async Task DecideAsync_NonProviderFailureWithFallThrough_Propagates()
    {
        var cascade = new CascadeProvider(
            new CascadeStage(new StubProvider { Name = "buggy", Throws = new InvalidTimeZoneException("rule bug") })
            {
                FallThroughOnError = true,
            },
            new CascadeStage(new FakeDecisionProvider().Classify("intent", "Billing")));

        await Should.ThrowAsync<InvalidTimeZoneException>(() => DecideAsync(cascade, IntentQuestion));
    }

    [Fact]
    public async Task DecideAsync_DefinitionFailureWithFallThrough_Propagates()
    {
        var cascade = new CascadeProvider(
            new CascadeStage(new StubProvider { Name = "strict", Throws = new DecisionDefinitionException("bad definition") })
            {
                FallThroughOnError = true,
            },
            new CascadeStage(new FakeDecisionProvider().Classify("intent", "Billing")));

        await Should.ThrowAsync<DecisionDefinitionException>(() => DecideAsync(cascade, IntentQuestion));
    }

    [Fact]
    public void DecideAsync_CalledConcurrentlyOnOneCascade_AnswersEveryCall()
    {
        var cascade = Cascade(
            new FakeDecisionProvider().Classify("intent", "Billing").Declines("urgency"),
            new FakeDecisionProvider().Rate("urgency", "High"));
        var answered = 0;

        Parallel.For(0, 16, _ =>
        {
            var response = DecideAsync(cascade, IntentQuestion, UrgencyQuestion).GetAwaiter().GetResult();
            if (response.Answers.Count == 2)
            {
                Interlocked.Increment(ref answered);
            }
        });

        answered.ShouldBe(16);
    }

    private static CascadeProvider Cascade(params IDecisionProvider[] providers) =>
        new(providers.Select(p => new CascadeStage(p)));

    private static CascadeProvider Single() =>
        Cascade(new FakeDecisionProvider());

    private static StubProvider Failing(bool transient) =>
        new StubProvider { Name = "flaky", Throws = new ProviderException("upstream is busy", "flaky", transient) };

    private static ClassifyAnswerSpec Intent(double billing, double tracking, double returns) =>
        new("intent", new Dictionary<string, double> { ["Billing"] = billing, ["Tracking"] = tracking, ["Returns"] = returns });

    private static ProviderResponse Classified(double billing, double tracking, double returns) =>
        new(new Dictionary<string, AnswerSpec> { ["intent"] = Intent(billing, tracking, returns) });

    private static Task<ProviderResponse> DecideAsync(CascadeProvider cascade, params QuestionSpec[] questions) =>
        cascade.DecideAsync(Request(questions), TestContext.Current.CancellationToken);
}

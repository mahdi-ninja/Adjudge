using Adjudge.Providers;

namespace Adjudge.Testing.Tests;

public sealed class FakeDecisionProviderTests
{
    [Fact]
    public async Task DecideAsync_WithScriptedClassify_ProducesRequestedTopAndConfidence()
    {
        var provider = new FakeDecisionProvider().Classify("intent", nameof(Intent.Tracking), 0.8);

        var response = await provider.DecideAsync(
            Requests.For(Requests.Classify("intent", nameof(Intent.Billing), nameof(Intent.Tracking), nameof(Intent.Returns))),
            TestContext.Current.CancellationToken);

        var answer = response.Answers["intent"].ShouldBeOfType<ClassifyAnswerSpec>();
        answer.Probabilities.Values.Sum().ShouldBe(1d, 1e-9);
        answer.Confidence.ShouldBeNull();

        var distribution = Probabilities.ToDistribution<Intent>(answer.Probabilities);
        distribution.Top.ShouldBe(Intent.Tracking);
        distribution.Confidence.ShouldBe(0.8, 1e-9);
    }

    [Fact]
    public async Task DecideAsync_WithScriptedProbabilities_PassesThemThrough()
    {
        var scripted = new Dictionary<string, double>
        {
            [nameof(Intent.Billing)] = 0.7,
            [nameof(Intent.Tracking)] = 0.3,
        };
        var provider = new FakeDecisionProvider().Classify("intent", scripted, nativeConfidence: 0.55);

        var response = await provider.DecideAsync(
            Requests.For(Requests.Classify("intent", nameof(Intent.Billing), nameof(Intent.Tracking), nameof(Intent.Returns))),
            TestContext.Current.CancellationToken);

        var answer = response.Answers["intent"].ShouldBeOfType<ClassifyAnswerSpec>();
        answer.Probabilities.ShouldBe(scripted);
        answer.Confidence.ShouldBe(0.55);
    }

    [Fact]
    public async Task DecideAsync_WithScriptedRate_ProducesRequestedTopAndConfidence()
    {
        var provider = new FakeDecisionProvider().Rate("urgency", nameof(Urgency.High), 0.6);

        var response = await provider.DecideAsync(
            Requests.For(Requests.Rate("urgency", nameof(Urgency.Low), nameof(Urgency.Medium), nameof(Urgency.High))),
            TestContext.Current.CancellationToken);

        var answer = response.Answers["urgency"].ShouldBeOfType<RateAnswerSpec>();
        Probabilities.ToDistribution<Urgency>(answer.Probabilities).Confidence.ShouldBe(0.6, 1e-9);
    }

    [Fact]
    public async Task DecideAsync_WithScriptedRateProbabilities_PassesThemThrough()
    {
        var scripted = new Dictionary<string, double>
        {
            [nameof(Urgency.Low)] = 0.1,
            [nameof(Urgency.Medium)] = 0.2,
            [nameof(Urgency.High)] = 0.7,
        };
        var provider = new FakeDecisionProvider().Rate("urgency", scripted, nativeConfidence: 0.42);

        var response = await provider.DecideAsync(
            Requests.For(Requests.Rate("urgency", nameof(Urgency.Low), nameof(Urgency.Medium), nameof(Urgency.High))),
            TestContext.Current.CancellationToken);

        var answer = response.Answers["urgency"].ShouldBeOfType<RateAnswerSpec>();
        answer.Confidence.ShouldBe(0.42);
        answer.Probabilities.ShouldBe(scripted);
    }

    [Fact]
    public async Task DecideAsync_WithScriptedAssert_ReturnsThatProbability()
    {
        var provider = new FakeDecisionProvider().Assert("abusive", 0.83);

        var response = await provider.DecideAsync(Requests.For(Requests.Assert("abusive")), TestContext.Current.CancellationToken);

        response.Answers["abusive"].ShouldBeOfType<AssertAnswerSpec>().Probability.ShouldBe(0.83);
    }

    [Fact]
    public async Task DecideAsync_WithUnscriptedQuestionAndUniformBehaviour_SpreadsMassEvenly()
    {
        var provider = new FakeDecisionProvider();

        var response = await provider.DecideAsync(
            Requests.For(
                Requests.Classify("intent", nameof(Intent.Billing), nameof(Intent.Tracking), nameof(Intent.Returns)),
                Requests.Assert("abusive")),
            TestContext.Current.CancellationToken);

        var classify = response.Answers["intent"].ShouldBeOfType<ClassifyAnswerSpec>();
        classify.Probabilities.Values.ShouldAllBe(v => Math.Abs(v - (1d / 3)) < 1e-9);
        classify.Confidence.ShouldBeNull();
        response.Answers["abusive"].ShouldBeOfType<AssertAnswerSpec>().Probability.ShouldBe(0.5);
    }

    [Fact]
    public async Task DecideAsync_WithUnscriptedQuestionAndThrowBehaviour_Throws()
    {
        var provider = new FakeDecisionProvider(UnscriptedBehaviour.Throw);

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => provider.DecideAsync(Requests.For(Requests.Assert("abusive")), TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("abusive");
    }

    [Fact]
    public async Task DecideAsync_AfterThrows_ThrowsOnceThenAnswers()
    {
        var provider = new FakeDecisionProvider().Assert("abusive", 0.2).Throws(new TimeoutException("boom"));
        var request = Requests.For(Requests.Assert("abusive"));

        await Should.ThrowAsync<TimeoutException>(() => provider.DecideAsync(request, TestContext.Current.CancellationToken));
        var response = await provider.DecideAsync(request, TestContext.Current.CancellationToken);

        response.Answers["abusive"].ShouldBeOfType<AssertAnswerSpec>().Probability.ShouldBe(0.2);
    }

    [Fact]
    public async Task DecideAsync_AfterAlwaysThrows_ThrowsEveryTime()
    {
        var provider = new FakeDecisionProvider().AlwaysThrows(new TimeoutException("boom"));
        var request = Requests.For(Requests.Assert("abusive"));

        await Should.ThrowAsync<TimeoutException>(() => provider.DecideAsync(request, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<TimeoutException>(() => provider.DecideAsync(request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DecideAsync_WithModelUsageAndMetadata_ReportsThem()
    {
        var provider = new FakeDecisionProvider()
            .WithModel("fake-1")
            .WithUsage(120, 34)
            .WithMetadata("requestId", "abc");

        var response = await provider.DecideAsync(Requests.For(Requests.Assert("abusive")), TestContext.Current.CancellationToken);

        response.Model.ShouldBe("fake-1");
        response.Usage.ShouldBe(new Usage(120, 34));
        response.Metadata.ShouldNotBeNull()["requestId"].ShouldBe("abc");
    }

    [Fact]
    public async Task DecideAsync_WithScriptedKeyTheQuestionDoesNotDeclare_Throws()
    {
        var provider = new FakeDecisionProvider().Classify("intent", "Nonsense");

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => provider.DecideAsync(
                Requests.For(Requests.Classify("intent", nameof(Intent.Billing), nameof(Intent.Tracking))),
                TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("Nonsense");
    }

    [Fact]
    public async Task DecideAsync_WithScriptedClassifySource_CarriesTheHint()
    {
        var provider = new FakeDecisionProvider().Classify("intent", nameof(Intent.Tracking), source: ConfidenceSource.Sampled);

        var response = await provider.DecideAsync(
            Requests.For(Requests.Classify("intent", nameof(Intent.Billing), nameof(Intent.Tracking), nameof(Intent.Returns))),
            TestContext.Current.CancellationToken);

        response.Answers["intent"].ShouldBeOfType<ClassifyAnswerSpec>().Source.ShouldBe(ConfidenceSource.Sampled);
    }

    [Fact]
    public async Task DecideAsync_WithScriptedClassifyProbabilitiesAndSource_CarriesTheHint()
    {
        var scripted = new Dictionary<string, double> { [nameof(Intent.Billing)] = 1d };
        var provider = new FakeDecisionProvider().Classify("intent", scripted, nativeConfidence: 0.55, source: ConfidenceSource.Heuristic);

        var response = await provider.DecideAsync(
            Requests.For(Requests.Classify("intent", nameof(Intent.Billing), nameof(Intent.Tracking), nameof(Intent.Returns))),
            TestContext.Current.CancellationToken);

        response.Answers["intent"].ShouldBeOfType<ClassifyAnswerSpec>().Source.ShouldBe(ConfidenceSource.Heuristic);
    }

    [Fact]
    public async Task DecideAsync_WithScriptedRateSource_CarriesTheHint()
    {
        var provider = new FakeDecisionProvider().Rate("urgency", nameof(Urgency.High), source: ConfidenceSource.Sampled);

        var response = await provider.DecideAsync(
            Requests.For(Requests.Rate("urgency", nameof(Urgency.Low), nameof(Urgency.Medium), nameof(Urgency.High))),
            TestContext.Current.CancellationToken);

        response.Answers["urgency"].ShouldBeOfType<RateAnswerSpec>().Source.ShouldBe(ConfidenceSource.Sampled);
    }

    [Fact]
    public async Task DecideAsync_WithScriptedRateProbabilitiesAndSource_CarriesTheHint()
    {
        var scripted = new Dictionary<string, double> { [nameof(Urgency.High)] = 1d };
        var provider = new FakeDecisionProvider().Rate("urgency", scripted, nativeConfidence: 0.6, source: ConfidenceSource.Heuristic);

        var response = await provider.DecideAsync(
            Requests.For(Requests.Rate("urgency", nameof(Urgency.Low), nameof(Urgency.Medium), nameof(Urgency.High))),
            TestContext.Current.CancellationToken);

        response.Answers["urgency"].ShouldBeOfType<RateAnswerSpec>().Source.ShouldBe(ConfidenceSource.Heuristic);
    }

    [Fact]
    public async Task DecideAsync_WithNoSourceScripted_LeavesTheHintNull()
    {
        var provider = new FakeDecisionProvider().Classify("intent", nameof(Intent.Tracking));

        var response = await provider.DecideAsync(
            Requests.For(Requests.Classify("intent", nameof(Intent.Billing), nameof(Intent.Tracking), nameof(Intent.Returns))),
            TestContext.Current.CancellationToken);

        response.Answers["intent"].ShouldBeOfType<ClassifyAnswerSpec>().Source.ShouldBeNull();
    }

    [Fact]
    public async Task DecideAsync_WithADeclinedQuestion_OmitsItFromTheResponse()
    {
        var provider = new FakeDecisionProvider().Declines("intent");

        var response = await provider.DecideAsync(
            Requests.For(Requests.Classify("intent", nameof(Intent.Billing), nameof(Intent.Tracking))),
            TestContext.Current.CancellationToken);

        response.Answers.ShouldNotContainKey("intent");
    }

    [Fact]
    public async Task DecideAsync_WithADeclinedQuestion_StillAnswersTheRest()
    {
        var provider = new FakeDecisionProvider().Declines("intent").Assert("abusive", 0.3);

        var response = await provider.DecideAsync(
            Requests.For(Requests.Classify("intent", nameof(Intent.Billing)), Requests.Assert("abusive")),
            TestContext.Current.CancellationToken);

        response.Answers.Keys.ShouldBe(["abusive"]);
    }

    [Fact]
    public async Task DecideAsync_WhenAScriptReplacesADecline_AnswersTheQuestionAgain()
    {
        var provider = new FakeDecisionProvider().Declines("intent").Classify("intent", nameof(Intent.Billing));

        var response = await provider.DecideAsync(
            Requests.For(Requests.Classify("intent", nameof(Intent.Billing), nameof(Intent.Tracking))),
            TestContext.Current.CancellationToken);

        response.Answers.ShouldContainKey("intent");
    }

    [Fact]
    public async Task DecideAsync_AfterResetClearsADecline_AnswersTheQuestionAgain()
    {
        var provider = new FakeDecisionProvider().Declines("intent");
        provider.Reset();

        var response = await provider.DecideAsync(
            Requests.For(Requests.Classify("intent", nameof(Intent.Billing), nameof(Intent.Tracking))),
            TestContext.Current.CancellationToken);

        response.Answers.ShouldContainKey("intent");
    }

    [Fact]
    public async Task DecideAsync_WithADeclinedQuestionRunThroughTheEngine_Throws()
    {
        var provider = new FakeDecisionProvider().Declines("intent");
        var engine = new DecisionEngine(provider, new DecisionEngineOptions { EnableTelemetry = false });
        var decision = engine.Create<TriageDecision, Ticket, Triage>();

        var exception = await Should.ThrowAsync<ProviderResponseException>(
            () => decision.DecideAsync(new Ticket("hello"), TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("intent");
    }

    [Fact]
    public async Task DecideAsync_WhenCalled_RecordsTheRequest()
    {
        var provider = new FakeDecisionProvider();
        var request = Requests.For(Requests.Assert("abusive"));

        await provider.DecideAsync(request, TestContext.Current.CancellationToken);

        provider.Requests.Count.ShouldBe(1);
        provider.LastRequest.ShouldBeSameAs(request);
    }

    [Fact]
    public async Task Reset_AfterScriptingAndCalling_ClearsScriptsAndRequests()
    {
        var provider = new FakeDecisionProvider().Assert("abusive", 0.9);
        var request = Requests.For(Requests.Assert("abusive"));
        await provider.DecideAsync(request, TestContext.Current.CancellationToken);

        provider.Reset();
        var response = await provider.DecideAsync(request, TestContext.Current.CancellationToken);

        provider.Requests.Count.ShouldBe(1);
        response.Answers["abusive"].ShouldBeOfType<AssertAnswerSpec>().Probability.ShouldBe(0.5);
    }

    [Fact]
    public void DecideAsync_CalledInParallel_RecordsEveryRequest()
    {
        var provider = new FakeDecisionProvider().Assert("abusive", 0.4);

        Parallel.For(0, 32, _ => provider.DecideAsync(Requests.For(Requests.Assert("abusive")), CancellationToken.None).GetAwaiter().GetResult());

        provider.Requests.Count.ShouldBe(32);
    }
}

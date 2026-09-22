using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Adjudge.Providers;
using Adjudge.Testing;
using static Adjudge.Tests.Core.CascadeFixtures;

namespace Adjudge.Tests.Core;

[Collection(CascadeTestGroup.Name)]
public sealed class CascadeTelemetryTests
{
    private const string DefinitionId = "test.cascade";

    [Fact]
    public async Task DecideAsync_EachStageCall_StartsOneActivity()
    {
        var activities = new ConcurrentBag<Activity>();
        using var listener = Listen(activities);

        await DecideAsync();

        activities.Count.ShouldBe(2);
    }

    [Fact]
    public async Task DecideAsync_StageActivity_CarriesTheStageIndexProviderAndDefinition()
    {
        var activities = new ConcurrentBag<Activity>();
        using var listener = Listen(activities);

        await DecideAsync();

        var second = activities.Single(a => (int)a.GetTagItem("stage.index")! == 1);
        (second.GetTagItem("provider"), second.GetTagItem("questions"), second.GetTagItem("definition.id"))
            .ShouldBe(("fake", 1, DefinitionId));
    }

    [Fact]
    public async Task DecideAsync_StageThatFails_SetsAnErrorStatusOnItsActivity()
    {
        var activities = new ConcurrentBag<Activity>();
        using var listener = Listen(activities);
        var cascade = new CascadeProvider(
            new CascadeStage(new StubProvider { Name = "flaky", Throws = new ProviderException("upstream is busy", "flaky", isTransient: true) }));

        await Should.ThrowAsync<ProviderException>(
            () => cascade.DecideAsync(Request(IntentQuestion), TestContext.Current.CancellationToken));

        var failed = activities.Single();
        (failed.Status, failed.StatusDescription).ShouldBe((ActivityStatusCode.Error, "upstream is busy"));
    }

    [Fact]
    public async Task DecideAsync_StageThatFallsThrough_SetsAnErrorStatusOnItsActivity()
    {
        var activities = new ConcurrentBag<Activity>();
        using var listener = Listen(activities);
        var cascade = new CascadeProvider(
            new CascadeStage(new StubProvider { Name = "flaky", Throws = new ProviderException("upstream is busy", "flaky", isTransient: true) })
            {
                FallThroughOnError = true,
            },
            new CascadeStage(new FakeDecisionProvider().Classify("intent", "Billing")));

        await cascade.DecideAsync(Request(IntentQuestion), TestContext.Current.CancellationToken);

        activities.Single(a => (int)a.GetTagItem("stage.index")! == 0).Status.ShouldBe(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task DecideAsync_AcceptedAnswers_AreCountedAgainstTheStageThatServedThem()
    {
        var counted = new List<(string Provider, long Value)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == "Adjudge" && instrument.Name == "adjudge.cascade.answers")
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            var read = tags.ToArray();
            if (read.Single(t => t.Key == "definition.id").Value!.ToString() != DefinitionId)
            {
                return;
            }

            var provider = read.Single(t => t.Key == "provider").Value!.ToString()!;
            lock (counted)
            {
                counted.Add((provider, value));
            }
        });
        listener.Start();

        await DecideAsync();

        counted.Sum(c => c.Value).ShouldBe(2);
    }

    private static ActivityListener Listen(ConcurrentBag<Activity> activities)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Adjudge",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stopped =>
            {
                if (stopped.OperationName == "adjudge.cascade.stage" &&
                    stopped.GetTagItem("definition.id") as string == DefinitionId)
                {
                    activities.Add(stopped);
                }
            },
        };

        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static Task<ProviderResponse> DecideAsync()
    {
        var first = new FakeDecisionProvider().Classify("intent", "Billing").Declines("urgency");
        var second = new FakeDecisionProvider().Rate("urgency", "High");
        var cascade = new CascadeProvider(new CascadeStage(first), new CascadeStage(second));

        return cascade.DecideAsync(Request(IntentQuestion, UrgencyQuestion), TestContext.Current.CancellationToken);
    }
}

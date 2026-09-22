using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Adjudge.Tests.Core;

[Collection(CascadeTestGroup.Name)]
public sealed class TelemetryTests
{
    private const string DefinitionId = "test.telemetry";

    private static readonly Ticket Context = new("I was charged twice");

    [Fact]
    public async Task DecideAsync_TelemetryEnabled_CountsTheDecision()
    {
        var counts = new List<long>();
        using var listener = Listen(
            longMeasurement: (instrument, value, _) =>
            {
                if (instrument.Name == "adjudge.decisions")
                {
                    counts.Add(value);
                }
            });

        await DecideAsync();

        counts.Sum().ShouldBe(1);
    }

    [Fact]
    public async Task DecideAsync_TelemetryEnabled_RecordsConfidencePerQuestion()
    {
        var questions = new List<string>();
        using var listener = Listen(
            doubleMeasurement: (instrument, tags) =>
            {
                if (instrument.Name == "adjudge.confidence")
                {
                    questions.Add(Tag(tags, "question")!);
                }
            });

        await DecideAsync();

        questions.ShouldBe(["intent", "urgency"]);
    }

    [Fact]
    public async Task DecideAsync_ProviderReportingUsage_CountsInputTokens()
    {
        var tokens = new List<long>();
        using var listener = Listen(
            longMeasurement: (instrument, value, _) =>
            {
                if (instrument.Name == "adjudge.tokens.input")
                {
                    tokens.Add(value);
                }
            });

        await DecideAsync(new Usage(31, 7));

        tokens.Sum().ShouldBe(31);
    }

    [Fact]
    public async Task DecideAsync_DecisionSucceeded_TagsTheCountWithAnOkOutcome()
    {
        var outcomes = new List<string>();
        using var listener = Listen(
            longMeasurement: (instrument, _, tags) =>
            {
                if (instrument.Name == "adjudge.decisions")
                {
                    outcomes.Add(Tag(tags, "outcome")!);
                }
            });

        await DecideAsync();

        outcomes.ShouldBe(["ok"]);
    }

    [Fact]
    public async Task DecideAsync_TelemetryDisabled_RecordsNothing()
    {
        var measurements = 0;
        using var listener = Listen(
            longMeasurement: (_, _, _) => measurements++,
            doubleMeasurement: (_, _) => measurements++);

        await DecideAsync(telemetry: false);

        measurements.ShouldBe(0);
    }

    [Fact]
    public async Task DecideAsync_DecisionFailed_TagsTheCountWithAnErrorOutcome()
    {
        var outcomes = new List<string>();
        using var listener = Listen(
            longMeasurement: (instrument, _, tags) =>
            {
                if (instrument.Name == "adjudge.decisions")
                {
                    outcomes.Add(Tag(tags, "outcome")!);
                }
            });

        await FailAsync();

        outcomes.ShouldBe(["error"]);
    }

    [Fact]
    public async Task DecideAsync_DecisionFailed_StopsTheActivityWithAnErrorStatus()
    {
        // The listener is process-global, so classes running in parallel stop "Adjudge" activities on their own
        // threads. Filter to this definition inside the callback, and collect into a thread-safe queue.
        var activities = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Adjudge",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stopped =>
            {
                if (stopped.GetTagItem("definition.id") as string == DefinitionId)
                {
                    activities.Enqueue(stopped);
                }
            },
        };
        ActivitySource.AddActivityListener(listener);

        await FailAsync();

        var activity = activities.ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Error);
        activity.IsStopped.ShouldBeTrue();
    }

    private static async Task FailAsync()
    {
        var provider = new StubProvider { Throws = new TimeoutException("boom") };
        var decision = new DecisionEngine(provider).Create<TelemetryDecision, Ticket, Triage>();

        var decide = async () => await decision.DecideAsync(Context, TestContext.Current.CancellationToken);

        await decide.ShouldThrowAsync<TimeoutException>();
    }

    private static string? Tag(ReadOnlySpan<KeyValuePair<string, object?>> tags, string key)
    {
        foreach (var tag in tags)
        {
            if (tag.Key == key)
            {
                return tag.Value?.ToString();
            }
        }

        return null;
    }

    private static bool Mine(ReadOnlySpan<KeyValuePair<string, object?>> tags) => Tag(tags, "definition.id") == DefinitionId;

    private static async Task DecideAsync(Usage? usage = null, bool telemetry = true)
    {
        var provider = new StubProvider { Response = Answers.Triage(usage: usage) };
        var options = new DecisionEngineOptions { EnableTelemetry = telemetry };
        var decision = new DecisionEngine(provider, options).Create<TelemetryDecision, Ticket, Triage>();

        await decision.DecideAsync(Context, TestContext.Current.CancellationToken);
    }

    private static MeterListener Listen(
        LongMeasurement? longMeasurement = null,
        DoubleMeasurement? doubleMeasurement = null)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == "Adjudge")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };

        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (Mine(tags))
            {
                longMeasurement?.Invoke(instrument, value, tags);
            }
        });

        listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) =>
        {
            if (Mine(tags))
            {
                doubleMeasurement?.Invoke(instrument, tags);
            }
        });

        listener.Start();
        return listener;
    }

    private delegate void LongMeasurement(Instrument instrument, long value, ReadOnlySpan<KeyValuePair<string, object?>> tags);

    private delegate void DoubleMeasurement(Instrument instrument, ReadOnlySpan<KeyValuePair<string, object?>> tags);
}

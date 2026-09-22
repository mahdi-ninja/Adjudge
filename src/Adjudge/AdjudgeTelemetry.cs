using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Adjudge;

internal static class AdjudgeTelemetry
{
    public const string SourceName = "Adjudge";

    public static readonly ActivitySource Activities = new(SourceName);

    private static readonly Meter Meter = new(SourceName);

    public static readonly Counter<long> Decisions = Meter.CreateCounter<long>("adjudge.decisions", "{decision}");

    public static readonly Counter<long> InputTokens = Meter.CreateCounter<long>("adjudge.tokens.input", "{token}");

    public static readonly Counter<long> OutputTokens = Meter.CreateCounter<long>("adjudge.tokens.output", "{token}");

    public static readonly Counter<long> CascadeAnswers = Meter.CreateCounter<long>("adjudge.cascade.answers", "{answer}");

    public static readonly Histogram<double> Confidence = Meter.CreateHistogram<double>("adjudge.confidence", "1");
}

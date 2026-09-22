using System.Diagnostics;
using System.Globalization;

namespace Adjudge.Providers;

/// <summary>
/// A provider that puts each question to a sequence of stages, keeping the first answer that clears the
/// stage's bar and carrying the rest on to the next stage. The last stage is authoritative, so anything
/// it answers stands, and anything still open after it is an error.
/// <para>
/// The response metadata carries <c>stage.{index}.provider</c> for every stage that was called,
/// <c>stage.{index}.error</c> for a stage that failed and fell through, <c>question.{name}.stage</c> and
/// <c>question.{name}.provider</c> for every question that was answered, <c>stages.called</c>, and each
/// stage's own response metadata under a <c>stage.{index}.</c> prefix, so <c>request_id</c> from the
/// second stage reads as <c>stage.1.request_id</c>.
/// </para>
/// </summary>
public sealed class CascadeProvider : IDecisionProvider
{
    private const DecisionCapabilities Kinds =
        DecisionCapabilities.Classify | DecisionCapabilities.Rate | DecisionCapabilities.Assert;

    private readonly CascadeStage[] _stages;

    /// <summary>Creates a cascade over the stages, in the order they should be tried.</summary>
    /// <param name="stages">At least one stage, cheapest first by convention.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stages"/> is null, or holds a null stage.</exception>
    /// <exception cref="ArgumentException">No stage was supplied, or the last stage cannot serve every kind an earlier stage can.</exception>
    public CascadeProvider(params CascadeStage[] stages)
        : this((IEnumerable<CascadeStage>)(stages ?? throw new ArgumentNullException(nameof(stages))))
    {
    }

    /// <summary>Creates a cascade over the stages, in the order they should be tried.</summary>
    /// <param name="stages">At least one stage, cheapest first by convention.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stages"/> is null, or holds a null stage.</exception>
    /// <exception cref="ArgumentException">No stage was supplied, or the last stage cannot serve every kind an earlier stage can.</exception>
    public CascadeProvider(IEnumerable<CascadeStage> stages)
    {
        ArgumentNullException.ThrowIfNull(stages);

        _stages = [.. stages];
        if (_stages.Length == 0)
        {
            throw new ArgumentException("A cascade needs at least one stage.", nameof(stages));
        }

        foreach (var stage in _stages)
        {
            ArgumentNullException.ThrowIfNull(stage, nameof(stages));
        }

        var last = _stages[^1].Provider.Capabilities;
        var shared = last;
        foreach (var stage in _stages)
        {
            var missing = (stage.Provider.Capabilities & Kinds) & ~last;
            if (missing != DecisionCapabilities.None)
            {
                throw new ArgumentException(
                    $"The last stage '{_stages[^1].Provider.Name}' cannot answer {missing}, which stage " +
                    $"'{stage.Provider.Name}' can, so a question it declined would have nowhere to go.",
                    nameof(stages));
            }

            shared &= stage.Provider.Capabilities;
        }

        Capabilities = (last & Kinds) |
            (shared & (DecisionCapabilities.NativeConfidence | DecisionCapabilities.Batch));
    }

    /// <summary>Always <c>cascade</c>. The stage that actually answered each question is reported in the response metadata.</summary>
    public string Name => "cascade";

    /// <summary>The kinds the last stage can answer, since it has to be able to take on anything an earlier stage declined, plus native confidence and batching only when every stage offers them.</summary>
    public DecisionCapabilities Capabilities { get; }

    /// <summary>Runs the stages in order until every question is answered, merging what each stage contributed.</summary>
    /// <param name="request">The questions and the context, passed on to each stage with only the still-open questions.</param>
    /// <param name="ct">Checked before the first stage and passed to every stage.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ProviderResponseException">A question was still unanswered after the last stage, or a stage returned no response at all.</exception>
    /// <exception cref="ProviderException">A stage failed, and was not set to fall through, was the last stage, or the failure was not transient.</exception>
    public async Task<ProviderResponse> DecideAsync(ProviderRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        var open = new List<QuestionSpec>(request.Questions);
        var settled = new HashSet<string>(StringComparer.Ordinal);
        var answers = new Dictionary<string, AnswerSpec>(open.Count, StringComparer.Ordinal);
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        var models = new HashSet<string>(StringComparer.Ordinal);
        var modelless = false;
        var called = 0;
        long? inputTokens = null;
        long? outputTokens = null;
        var anyUsage = false;

        for (var index = 0; index < _stages.Length && open.Count > 0; index++)
        {
            var stage = _stages[index];
            var asked = Eligible(open, stage.Provider.Capabilities);
            if (asked.Count == 0)
            {
                continue;
            }

            var last = index == _stages.Length - 1;
            var name = stage.Provider.Name;

            using var activity = AdjudgeTelemetry.Activities.StartActivity("adjudge.cascade.stage", ActivityKind.Internal);
            activity?.SetTag("definition.id", request.DefinitionId);
            activity?.SetTag("stage.index", index);
            activity?.SetTag("provider", name);
            activity?.SetTag("questions", asked.Count);

            ProviderResponse response;
            try
            {
                var returned = await stage.Provider
                    .DecideAsync(new ProviderRequest(request.Context, asked, request.DefinitionId), ct)
                    .ConfigureAwait(false);

                response = returned?.Answers is null
                    ? throw new ProviderResponseException($"Stage {index} provider '{name}' returned no answers.", name)
                    : returned;
            }
            catch (Exception exception)
            {
                activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
                if (!FallsThrough(exception, stage, last))
                {
                    throw;
                }

                called++;
                metadata[$"stage.{index}.provider"] = name;
                metadata[$"stage.{index}.error"] = exception.Message;
                continue;
            }

            called++;
            metadata[$"stage.{index}.provider"] = name;
            Accumulate(response.Usage, ref inputTokens, ref outputTokens, ref anyUsage);

            if (response.Metadata is { Count: > 0 } reported)
            {
                foreach (var (key, value) in reported)
                {
                    metadata[$"stage.{index}.{key}"] = value;
                }
            }

            var accepted = 0;
            foreach (var question in asked)
            {
                if (settled.Contains(question.Name) ||
                    !response.Answers.TryGetValue(question.Name, out var answer) ||
                    answer is null)
                {
                    continue;
                }

                if (!last && !stage.Accept(question, answer))
                {
                    continue;
                }

                answers[question.Name] = answer;
                settled.Add(question.Name);
                metadata[$"question.{question.Name}.stage"] = index.ToString(CultureInfo.InvariantCulture);
                metadata[$"question.{question.Name}.provider"] = name;
                accepted++;
            }

            if (accepted == 0)
            {
                continue;
            }

            if (response.Model is { Length: > 0 } model)
            {
                models.Add(model);
            }
            else
            {
                modelless = true;
            }

            AdjudgeTelemetry.CascadeAnswers.Add(
                accepted,
                new TagList { { "provider", name }, { "stage.index", index }, { "definition.id", request.DefinitionId } });

            var remaining = new List<QuestionSpec>(open.Count - accepted);
            foreach (var question in open)
            {
                if (!settled.Contains(question.Name))
                {
                    remaining.Add(question);
                }
            }

            open = remaining;
        }

        if (open.Count > 0)
        {
            throw new ProviderResponseException(
                $"No stage answered: {string.Join(", ", open.Select(q => q.Name))}.",
                Name);
        }

        metadata["stages.called"] = called.ToString(CultureInfo.InvariantCulture);

        return new ProviderResponse(
            answers,
            !modelless && models.Count == 1 ? models.First() : null,
            anyUsage ? new Usage(inputTokens, outputTokens) : null,
            metadata);
    }

    private static bool FallsThrough(Exception exception, CascadeStage stage, bool last) =>
        stage.FallThroughOnError &&
        !last &&
        exception switch
        {
            ProviderResponseException => true,
            ProviderException provider => provider.IsTransient,
            _ => false,
        };

    private static List<QuestionSpec> Eligible(List<QuestionSpec> open, DecisionCapabilities capabilities)
    {
        var eligible = new List<QuestionSpec>(open.Count);
        foreach (var question in open)
        {
            var required = question switch
            {
                ClassifySpec => DecisionCapabilities.Classify,
                RateSpec => DecisionCapabilities.Rate,
                AssertSpec => DecisionCapabilities.Assert,
                _ => DecisionCapabilities.None,
            };

            if ((required & ~capabilities) == DecisionCapabilities.None)
            {
                eligible.Add(question);
            }
        }

        return eligible;
    }

    private static void Accumulate(Usage? usage, ref long? input, ref long? output, ref bool any)
    {
        if (usage is null)
        {
            return;
        }

        any = true;
        if (usage.InputTokens is { } reportedInput)
        {
            input = (input ?? 0) + reportedInput;
        }

        if (usage.OutputTokens is { } reportedOutput)
        {
            output = (output ?? 0) + reportedOutput;
        }
    }
}

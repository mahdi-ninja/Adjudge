using Adjudge.Providers;

namespace Adjudge.Testing;

/// <summary>
/// An <see cref="IDecisionProvider"/> whose answers are scripted per question name. Scripts hold the
/// intent; the distribution is built when the request arrives, against the keys the question declares.
/// </summary>
public sealed class FakeDecisionProvider : IDecisionProvider
{
    private readonly object _sync = new();
    private readonly Dictionary<string, Func<QuestionSpec, AnswerSpec>> _scripts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _metadata = new(StringComparer.Ordinal);
    private readonly Queue<Exception> _pendingThrows = new();
    private readonly List<ProviderRequest> _requests = [];
    private Exception? _alwaysThrows;
    private string? _model;
    private Usage? _usage;

    /// <summary>Creates a provider with nothing scripted yet.</summary>
    /// <param name="unscripted">What to do with a question no script covers. Defaults to a uniform answer.</param>
    public FakeDecisionProvider(UnscriptedBehaviour unscripted = UnscriptedBehaviour.Uniform) =>
        Unscripted = unscripted;

    /// <summary>Always <c>fake</c>, which is what results and telemetry report.</summary>
    public string Name => "fake";

    /// <summary>Everything, so no definition is rejected on capability grounds.</summary>
    public DecisionCapabilities Capabilities =>
        DecisionCapabilities.Classify |
        DecisionCapabilities.Rate |
        DecisionCapabilities.Assert |
        DecisionCapabilities.NativeConfidence |
        DecisionCapabilities.Batch;

    /// <summary>What to do with a question no script covers. Can be changed between calls.</summary>
    public UnscriptedBehaviour Unscripted { get; set; }

    /// <summary>Every request seen so far, in call order. Each read returns a snapshot, so it is safe to enumerate while calls are in flight.</summary>
    public IReadOnlyList<ProviderRequest> Requests
    {
        get
        {
            lock (_sync)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>The most recent request, or null before the first call.</summary>
    public ProviderRequest? LastRequest
    {
        get
        {
            lock (_sync)
            {
                return _requests.Count == 0 ? null : _requests[^1];
            }
        }
    }

    /// <summary>Scripts a classification that favours one option, with the remaining mass spread evenly.</summary>
    /// <param name="question">The question name, in camelCase, as the definition produces it.</param>
    /// <param name="top">An option key, checked against the question when the request arrives.</param>
    /// <param name="confidence">The confidence the resulting distribution should report.</param>
    /// <exception cref="ArgumentException"><paramref name="top"/> is null, empty or whitespace.</exception>
    public FakeDecisionProvider Classify(string question, string top, double confidence = 0.9)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(top);

        return Script(question, spec =>
        {
            var keys = Keys(spec, question);
            RequireKey(keys, top, question);
            return new ClassifyAnswerSpec(question, Spread(keys, top, confidence));
        });
    }

    /// <summary>Scripts a classification with exact probabilities, for tests over margin or normalisation.</summary>
    /// <param name="question">The question name, in camelCase, as the definition produces it.</param>
    /// <param name="probabilities">Mass per key, checked against the question when the request arrives.</param>
    /// <param name="nativeConfidence">A provider-reported confidence, which turns the answer's source native.</param>
    /// <exception cref="ArgumentException">No probability was supplied.</exception>
    public FakeDecisionProvider Classify(string question, IReadOnlyDictionary<string, double> probabilities, double? nativeConfidence = null)
    {
        var scripted = Copy(probabilities);

        return Script(question, spec =>
        {
            RequireKeys(Keys(spec, question), scripted.Keys, question);
            return new ClassifyAnswerSpec(question, scripted, nativeConfidence);
        });
    }

    /// <summary>Scripts a rating that favours one level, with the remaining mass spread evenly.</summary>
    /// <param name="question">The question name, in camelCase, as the definition produces it.</param>
    /// <param name="level">A level key, checked against the question when the request arrives.</param>
    /// <param name="confidence">The confidence the resulting distribution should report.</param>
    /// <exception cref="ArgumentException"><paramref name="level"/> is null, empty or whitespace.</exception>
    public FakeDecisionProvider Rate(string question, string level, double confidence = 0.9)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(level);

        return Script(question, spec =>
        {
            var keys = Keys(spec, question);
            RequireKey(keys, level, question);
            return new RateAnswerSpec(question, Spread(keys, level, confidence));
        });
    }

    /// <summary>Scripts a rating with exact probabilities, which is how a fractional value between levels is arranged.</summary>
    /// <param name="question">The question name, in camelCase, as the definition produces it.</param>
    /// <param name="probabilities">Mass per key, checked against the question when the request arrives.</param>
    /// <param name="nativeConfidence">A provider-reported confidence, which turns the answer's source native.</param>
    /// <exception cref="ArgumentException">No probability was supplied.</exception>
    public FakeDecisionProvider Rate(
        string question,
        IReadOnlyDictionary<string, double> probabilities,
        double? nativeConfidence = null)
    {
        var scripted = Copy(probabilities);

        return Script(question, spec =>
        {
            RequireKeys(Keys(spec, question), scripted.Keys, question);
            return new RateAnswerSpec(question, scripted, nativeConfidence);
        });
    }

    /// <summary>Scripts the probability of a proposition.</summary>
    /// <param name="question">The question name, in camelCase, as the definition produces it.</param>
    /// <param name="probability">The mass on the proposition holding.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="probability"/> is outside 0 to 1.</exception>
    public FakeDecisionProvider Assert(string question, double probability)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(probability, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(probability, 1);

        return Script(question, _ => new AssertAnswerSpec(question, probability));
    }

    /// <summary>Queues an exception for the next call, so retry and fallback paths can be exercised in order.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is null.</exception>
    public FakeDecisionProvider Throws(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        lock (_sync)
        {
            _pendingThrows.Enqueue(exception);
        }

        return this;
    }

    /// <summary>Makes every call fail, taking precedence over anything queued by <see cref="Throws"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is null.</exception>
    public FakeDecisionProvider AlwaysThrows(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        lock (_sync)
        {
            _alwaysThrows = exception;
        }

        return this;
    }

    /// <summary>Sets the model reported on every response.</summary>
    /// <exception cref="ArgumentException"><paramref name="model"/> is null, empty or whitespace.</exception>
    public FakeDecisionProvider WithModel(string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        lock (_sync)
        {
            _model = model;
        }

        return this;
    }

    /// <summary>Sets the token usage reported on every response, so telemetry assertions have something to read.</summary>
    public FakeDecisionProvider WithUsage(long input, long output)
    {
        lock (_sync)
        {
            _usage = new Usage(input, output);
        }

        return this;
    }

    /// <summary>Adds one metadata entry to every response, replacing any entry under the same key.</summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty or whitespace.</exception>
    public FakeDecisionProvider WithMetadata(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        lock (_sync)
        {
            _metadata[key] = value;
        }

        return this;
    }

    /// <summary>Clears the scripts, the recorded requests and everything else, so one instance can be shared across tests.</summary>
    public void Reset()
    {
        lock (_sync)
        {
            _scripts.Clear();
            _metadata.Clear();
            _pendingThrows.Clear();
            _requests.Clear();
            _alwaysThrows = null;
            _model = null;
            _usage = null;
        }
    }

    /// <summary>Records the request and answers each question from its script, or according to <see cref="Unscripted"/>.</summary>
    /// <exception cref="InvalidOperationException">A script names a key the question does not declare, or a question is unscripted and <see cref="Unscripted"/> is <see cref="UnscriptedBehaviour.Throw"/>.</exception>
    public Task<ProviderResponse> DecideAsync(ProviderRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        Func<QuestionSpec, AnswerSpec>?[] scripts;
        string? model;
        Usage? usage;
        IReadOnlyDictionary<string, string>? metadata;

        lock (_sync)
        {
            _requests.Add(request);

            if (_alwaysThrows is { } always)
            {
                return Task.FromException<ProviderResponse>(always);
            }

            if (_pendingThrows.Count > 0)
            {
                return Task.FromException<ProviderResponse>(_pendingThrows.Dequeue());
            }

            scripts = new Func<QuestionSpec, AnswerSpec>?[request.Questions.Count];
            for (var index = 0; index < request.Questions.Count; index++)
            {
                scripts[index] = _scripts.GetValueOrDefault(request.Questions[index].Name);
            }

            model = _model;
            usage = _usage;
            metadata = _metadata.Count == 0 ? null : new Dictionary<string, string>(_metadata, StringComparer.Ordinal);
        }

        var answers = new Dictionary<string, AnswerSpec>(request.Questions.Count, StringComparer.Ordinal);
        for (var index = 0; index < request.Questions.Count; index++)
        {
            var question = request.Questions[index];
            answers[question.Name] = scripts[index] is { } script ? script(question) : Unanswered(question);
        }

        return Task.FromResult(new ProviderResponse(answers, model, usage, metadata));
    }

    private static IReadOnlyList<string> Keys(QuestionSpec question, string name) => question switch
    {
        ClassifySpec classify => [.. classify.Options.Select(o => o.Key)],
        RateSpec rate => [.. rate.Levels.Select(l => l.Key)],
        _ => throw new InvalidOperationException(
            $"Question '{name}' is a '{question.GetType().Name}', which carries no option or level keys."),
    };

    private static void RequireKey(IReadOnlyList<string> keys, string key, string question)
    {
        if (!keys.Contains(key, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Question '{question}' has no key '{key}'. Its keys are: {string.Join(", ", keys)}.");
        }
    }

    private static void RequireKeys(IReadOnlyList<string> keys, IEnumerable<string> scripted, string question)
    {
        foreach (var key in scripted)
        {
            RequireKey(keys, key, question);
        }
    }

    private static Dictionary<string, double> Spread(IReadOnlyList<string> keys, string top, double confidence)
    {
        var mass = Answers.TopMass(keys.Count, confidence);
        var rest = keys.Count == 1 ? 0d : (1d - mass) / (keys.Count - 1);

        var probabilities = new Dictionary<string, double>(keys.Count, StringComparer.Ordinal);
        foreach (var key in keys)
        {
            probabilities[key] = string.Equals(key, top, StringComparison.Ordinal) ? mass : rest;
        }

        return probabilities;
    }

    private static Dictionary<string, double> Copy(IReadOnlyDictionary<string, double> probabilities)
    {
        ArgumentNullException.ThrowIfNull(probabilities);

        if (probabilities.Count == 0)
        {
            throw new ArgumentException("A scripted answer must declare at least one probability.", nameof(probabilities));
        }

        return new Dictionary<string, double>(probabilities, StringComparer.Ordinal);
    }

    private static Dictionary<string, double> Uniform(IReadOnlyList<string> keys)
    {
        var share = 1d / keys.Count;
        var probabilities = new Dictionary<string, double>(keys.Count, StringComparer.Ordinal);
        foreach (var key in keys)
        {
            probabilities[key] = share;
        }

        return probabilities;
    }

    private AnswerSpec Unanswered(QuestionSpec question)
    {
        if (Unscripted == UnscriptedBehaviour.Throw)
        {
            throw new InvalidOperationException($"Question '{question.Name}' has no scripted answer.");
        }

        return question switch
        {
            ClassifySpec classify => new ClassifyAnswerSpec(question.Name, Uniform(Keys(classify, question.Name))),
            RateSpec rate => new RateAnswerSpec(question.Name, Uniform(Keys(rate, question.Name))),
            AssertSpec => new AssertAnswerSpec(question.Name, 0.5),
            _ => throw new InvalidOperationException($"Question '{question.Name}' is of unsupported kind '{question.GetType().Name}'."),
        };
    }

    private FakeDecisionProvider Script(string question, Func<QuestionSpec, AnswerSpec> answer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);

        lock (_sync)
        {
            _scripts[question] = answer;
        }

        return this;
    }
}

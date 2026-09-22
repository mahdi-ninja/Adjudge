namespace Adjudge.Testing;

/// <summary>An <see cref="IDecision{TContext, TResult}"/> that returns a scripted result and records its calls.</summary>
/// <typeparam name="TContext">The facts the decision takes.</typeparam>
/// <typeparam name="TResult">The typed answers the decision produces.</typeparam>
public sealed class FakeDecision<TContext, TResult> : IDecision<TContext, TResult>
{
    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private readonly object _gate = new();
    private readonly List<TContext> _contexts = [];
    private readonly Dictionary<string, string> _metadata = new(StringComparer.Ordinal);
    private Func<TContext, DecisionResult<TResult>>? _result;
    private Exception? _exception;
    private int _calls;

    /// <summary>How many times the decision has been called, counted even when the call throws.</summary>
    public int Calls => Volatile.Read(ref _calls);

    /// <summary>The contexts seen so far, in call order. Each read returns a snapshot, so it is safe to enumerate while calls are in flight.</summary>
    public IReadOnlyList<TContext> Contexts
    {
        get
        {
            lock (_gate)
            {
                return [.. _contexts];
            }
        }
    }

    /// <summary>Scripts one result for every call, wrapped in a plausible envelope. Clears any scripted exception.</summary>
    public FakeDecision<TContext, TResult> Returns(TResult value) => Returns(_ => value);

    /// <summary>Scripts a result computed from the context, for tests that vary the answer by input. Clears any scripted exception.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public FakeDecision<TContext, TResult> Returns(Func<TContext, TResult> value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Script(context => Wrap(value(context)));
    }

    /// <summary>Scripts the whole envelope, for tests that assert over the identifier, usage or timestamp. Clears any scripted exception.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
    public FakeDecision<TContext, TResult> Returns(DecisionResult<TResult> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return Script(_ => result);
    }

    /// <summary>Adds one metadata entry to every wrapped result, replacing any entry under the same key. A result scripted whole carries its own metadata instead.</summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is null, empty or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public FakeDecision<TContext, TResult> WithMetadata(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        lock (_gate)
        {
            _metadata[key] = value;
        }

        return this;
    }

    /// <summary>Makes every call fail with this exception, until a <c>Returns</c> replaces it.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is null.</exception>
    public FakeDecision<TContext, TResult> Throws(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        lock (_gate)
        {
            _exception = exception;
        }

        return this;
    }

    /// <summary>Records the context and returns whatever was scripted.</summary>
    /// <exception cref="InvalidOperationException">Nothing was scripted.</exception>
    public Task<DecisionResult<TResult>> DecideAsync(TContext context, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        Func<TContext, DecisionResult<TResult>>? result;
        Exception? exception;

        lock (_gate)
        {
            _contexts.Add(context);
            result = _result;
            exception = _exception;
        }

        Interlocked.Increment(ref _calls);

        if (exception is not null)
        {
            return Task.FromException<DecisionResult<TResult>>(exception);
        }

        if (result is null)
        {
            throw new InvalidOperationException($"No result was scripted for this '{typeof(TResult).Name}' decision.");
        }

        return Task.FromResult(result(context));
    }

    private DecisionResult<TResult> Wrap(TResult value)
    {
        IReadOnlyDictionary<string, string> metadata;
        lock (_gate)
        {
            metadata = _metadata.Count == 0 ? EmptyMetadata : new Dictionary<string, string>(_metadata, StringComparer.Ordinal);
        }

        return new DecisionResult<TResult>(Guid.NewGuid(), "fake", "fake", null, value, null, DateTimeOffset.UtcNow, metadata);
    }

    private FakeDecision<TContext, TResult> Script(Func<TContext, DecisionResult<TResult>> result)
    {
        lock (_gate)
        {
            _result = result;
            _exception = null;
        }

        return this;
    }
}

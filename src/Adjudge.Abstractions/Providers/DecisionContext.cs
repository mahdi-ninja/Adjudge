using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Adjudge.Providers;

/// <summary>The caller's facts on their way to a provider, with the JSON form produced once, lazily, and cached.</summary>
public sealed class DecisionContext
{
    private readonly Lazy<JsonElement> _json;

    /// <summary>Wraps a context whose JSON form is produced on first use by reflection.</summary>
    /// <param name="value">The object the provider will see, serialised as given.</param>
    /// <param name="options">The settings the JSON is produced with. Defaults to the serialiser's own.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    [RequiresUnreferencedCode("Serialising the context uses reflection; supply a pre-built JsonElement when trimming.")]
    [RequiresDynamicCode("Serialising the context uses reflection; supply a pre-built JsonElement when trimming.")]
    public DecisionContext(object value, JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(value);

        Value = value;
        _json = new Lazy<JsonElement>(
            () => JsonSerializer.SerializeToElement(value, value.GetType(), options),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private DecisionContext(object value, JsonElement json)
    {
        Value = value;
        _json = new Lazy<JsonElement>(json);
    }

    /// <summary>The caller's own object, untouched, for providers that would rather read it directly than read the JSON.</summary>
    public object Value { get; }

    /// <summary>Builds a context over JSON the caller has already produced, so nothing is serialised by reflection and the type stays trim-safe.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static DecisionContext FromJson(object value, JsonElement json)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new DecisionContext(value, json);
    }

    /// <summary>Returns the JSON form, serialising on the first call. Safe to call from several threads.</summary>
    public JsonElement AsJson() => _json.Value;
}

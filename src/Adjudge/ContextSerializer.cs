using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Adjudge;

/// <summary>Builds context serialisers over source-generated metadata, which is what keeps the engine trimming and ahead-of-time safe.</summary>
public static class ContextSerializer
{
    /// <summary>Serialises each context through the type metadata the supplied context carries, so several context types can share one serialiser.</summary>
    /// <param name="context">A source-generated serialiser context that registers every type passed to a decision.</param>
    /// <returns>A serialiser that resolves metadata per context type on each call.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public static SerialiseContext From(JsonSerializerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return value =>
        {
            var typeInfo = context.GetTypeInfo(value.GetType())
                ?? throw new InvalidOperationException(
                    $"'{context.GetType()}' has no metadata for context type '{value.GetType()}'. Add [JsonSerializable(typeof({value.GetType().Name}))] to it.");

            return JsonSerializer.SerializeToElement(value, typeInfo);
        };
    }

    /// <summary>Serialises every context through one known type's metadata, for the common case of a decision with a single context type.</summary>
    /// <typeparam name="T">The context type the engine will be handed.</typeparam>
    /// <param name="typeInfo">The source-generated metadata for <typeparamref name="T"/>.</param>
    /// <returns>A serialiser that accepts contexts of <typeparamref name="T"/> alone.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="typeInfo"/> is null.</exception>
    public static SerialiseContext From<T>(JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);

        return value => value is T typed
            ? JsonSerializer.SerializeToElement(typed, typeInfo)
            : throw new InvalidOperationException(
                $"This serialiser only handles '{typeof(T)}', but the context was '{value.GetType()}'.");
    }
}

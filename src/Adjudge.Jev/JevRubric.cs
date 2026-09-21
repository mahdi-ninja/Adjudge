using System.Text.Json.Nodes;

namespace Adjudge.Jev;

internal static class JevRubric
{
    public static JsonNode? ToNode(object value, string path)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value switch
        {
            string text => JsonValue.Create(text),
            OptionRubric rubric => Option(rubric),
            _ => throw new DecisionDefinitionException(
                $"Cannot serialise '{path}' of type '{value.GetType()}'. Supply a string or an {nameof(OptionRubric)}."),
        };
    }

    private static JsonObject Option(OptionRubric rubric)
    {
        var node = new JsonObject { ["description"] = rubric.Description };

        if (rubric.NotFor is { } notFor)
        {
            node["not_for"] = notFor;
        }

        if (rubric.Examples is { Count: > 0 } examples)
        {
            var values = new JsonNode?[examples.Count];
            for (var index = 0; index < examples.Count; index++)
            {
                values[index] = JsonValue.Create(examples[index]);
            }

            node["examples"] = new JsonArray(values);
        }

        return node;
    }
}

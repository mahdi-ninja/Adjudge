using System.Text.Json.Nodes;

namespace Adjudge.Jev;

internal sealed class JevQuestionPayload
{
    public string Type { get; set; } = string.Empty;

    public JsonNode? Instructions { get; set; }

    public JsonNode? Criteria { get; set; }
}

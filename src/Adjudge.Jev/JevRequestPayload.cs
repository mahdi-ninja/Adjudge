using System.Text.Json;

namespace Adjudge.Jev;

internal sealed class JevRequestPayload
{
    public JsonElement State { get; set; }

    public string Model { get; set; } = string.Empty;

    public Dictionary<string, JevQuestionPayload> Questions { get; set; } = [];
}

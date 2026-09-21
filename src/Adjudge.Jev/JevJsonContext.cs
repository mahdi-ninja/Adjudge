using System.Text.Json;
using System.Text.Json.Serialization;

namespace Adjudge.Jev;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(JevRequestPayload))]
[JsonSerializable(typeof(JevResponsePayload))]
[JsonSerializable(typeof(JsonElement))]
internal sealed partial class JevJsonContext : JsonSerializerContext;

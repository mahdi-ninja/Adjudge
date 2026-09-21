using System.Text.Json.Serialization;

namespace Adjudge.Tests.Core;

public sealed record Unregistered(string Note);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Ticket))]
public sealed partial class ProbeJsonContext : JsonSerializerContext;

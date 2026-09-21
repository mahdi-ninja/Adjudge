using System.Text.Json;

namespace Adjudge;

/// <summary>Everything the engine can be tuned with. The defaults are what most callers want.</summary>
public sealed class DecisionEngineOptions
{
    /// <summary>The settings the context is serialised with. Defaults to the web defaults, so camelCase property names.</summary>
    public JsonSerializerOptions JsonSerializerOptions { get; set; } = new(JsonSerializerDefaults.Web);

    /// <summary>Whether the engine emits its activity and metrics. On by default; it costs nothing when nobody is listening.</summary>
    public bool EnableTelemetry { get; set; } = true;
}

namespace Adjudge;

/// <summary>
/// Names a decision. It is optional: without it the decision is reported under its own simple type
/// name.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class DecisionAttribute : Attribute
{
    /// <summary>Names the decision, for telemetry and for the request sent to the provider.</summary>
    /// <param name="id">A stable identifier, conventionally dotted, such as <c>support.ticket-triage</c>.</param>
    /// <exception cref="ArgumentException"><paramref name="id"/> is null, empty or whitespace.</exception>
    public DecisionAttribute(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        Id = id;
    }

    /// <summary>The stable identifier the decision is reported under.</summary>
    public string Id { get; }
}

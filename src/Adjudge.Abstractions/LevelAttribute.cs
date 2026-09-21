namespace Adjudge;

/// <summary>Describes one level of an ordered enum used with <c>Rate</c>. Undecorated members fall back to their name.</summary>
[AttributeUsage(AttributeTargets.Field, Inherited = false)]
public sealed class LevelAttribute : Attribute
{
    /// <summary>Describes what the context has to look like to sit at this level.</summary>
    /// <exception cref="ArgumentException"><paramref name="description"/> is null, empty or whitespace.</exception>
    public LevelAttribute(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Description = description;
    }

    /// <summary>What the context has to look like to sit at this level.</summary>
    public string Description { get; }
}

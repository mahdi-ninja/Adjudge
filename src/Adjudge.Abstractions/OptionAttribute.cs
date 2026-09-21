namespace Adjudge;

/// <summary>Describes one option of an enum used with <c>Classify</c>. Undecorated members fall back to their name.</summary>
[AttributeUsage(AttributeTargets.Field, Inherited = false)]
public sealed class OptionAttribute : Attribute
{
    private readonly string[] _examples = [];

    /// <summary>Describes what the option covers.</summary>
    /// <exception cref="ArgumentException"><paramref name="description"/> is null, empty or whitespace.</exception>
    public OptionAttribute(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        Description = description;
    }

    /// <summary>What the option covers.</summary>
    public string Description { get; }

    /// <summary>What this option is commonly mistaken for, which sharpens the boundary against its neighbours.</summary>
    public string? NotFor { get; set; }

    /// <summary>Short illustrations that clearly belong to this option. Copied in and out, so the array cannot be mutated through it.</summary>
    public string[] Examples
    {
        get => [.. _examples];
        init => _examples = value is null ? [] : [.. value];
    }

    /// <summary>The examples without the defensive copy that <see cref="Examples"/> makes on every read.</summary>
    public IReadOnlyList<string> ExampleValues => _examples;
}

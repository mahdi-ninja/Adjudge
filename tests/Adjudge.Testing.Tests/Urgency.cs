namespace Adjudge.Testing.Tests;

public enum Urgency
{
    [Level("Can wait days")]
    Low,

    Medium,

    [Level("Needs attention now")]
    High,
}

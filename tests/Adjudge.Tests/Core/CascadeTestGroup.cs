namespace Adjudge.Tests.Core;

/// <summary>
/// Activity and Meter listeners are process-global, so the classes that install one run alone: a test
/// class running beside them would otherwise have its own cascade spans and measurements counted.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CascadeTestGroup
{
    public const string Name = "cascade";
}

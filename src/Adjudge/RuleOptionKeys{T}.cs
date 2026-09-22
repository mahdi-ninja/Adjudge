using System.Collections.Frozen;

namespace Adjudge.Providers;

internal static class RuleOptionKeys<T>
    where T : struct, Enum
{
    public static readonly FrozenSet<string> Keys =
        EnumMembers<T>.Values.Select(value => Enum.GetName(value)!).ToFrozenSet(StringComparer.Ordinal);

    public static readonly string Rendered = string.Join(", ", Keys);
}

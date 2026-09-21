namespace Adjudge;

internal sealed record EnumMemberDescriptor<T>(T Value, string Name, object Rubric)
    where T : struct, Enum;

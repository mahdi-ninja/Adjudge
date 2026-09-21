using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Adjudge;

internal static class EnumMetadata<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T>
    where T : struct, Enum
{
    private static readonly EnumMemberDescriptor<T>[] OptionMembers = Build(option: true);
    private static readonly EnumMemberDescriptor<T>[] LevelMembers = Build(option: false);

    public static IReadOnlyList<EnumMemberDescriptor<T>> Options => OptionMembers;

    public static IReadOnlyList<EnumMemberDescriptor<T>> Levels => LevelMembers;

    public static int Count => OptionMembers.Length;

    public static IReadOnlyDictionary<string, T> ByName { get; } =
        OptionMembers.ToDictionary(m => m.Name, m => m.Value, StringComparer.Ordinal);

    private static EnumMemberDescriptor<T>[] Build(bool option)
    {
        var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static);
        var values = EnumMembers<T>.Values;
        var members = new EnumMemberDescriptor<T>[values.Length];

        for (var index = 0; index < values.Length; index++)
        {
            var name = Enum.GetName(values[index])!;
            var field = Array.Find(fields, f => string.Equals(f.Name, name, StringComparison.Ordinal));
            members[index] = new EnumMemberDescriptor<T>(values[index], name, Rubric(field, name, option));
        }

        return members;
    }

    private static object Rubric(FieldInfo? field, string name, bool option)
    {
        if (field is null)
        {
            return name;
        }

        if (!option)
        {
            return field.GetCustomAttribute<LevelAttribute>()?.Description ?? (object)name;
        }

        var attribute = field.GetCustomAttribute<OptionAttribute>();
        if (attribute is null)
        {
            return name;
        }

        if (attribute.NotFor is null && attribute.ExampleValues.Count == 0)
        {
            return attribute.Description;
        }

        return new OptionRubric(attribute.Description, attribute.NotFor, attribute.ExampleValues);
    }
}

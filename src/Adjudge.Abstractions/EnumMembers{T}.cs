using System.Globalization;

namespace Adjudge;

internal static class EnumMembers<T>
    where T : struct, Enum
{
    public static readonly T[] Values;

    static EnumMembers()
    {
        var values = Enum.GetValues<T>().Distinct().ToArray();

        if (Enum.GetUnderlyingType(typeof(T)) == typeof(ulong))
        {
            var keys = new ulong[values.Length];
            for (var index = 0; index < values.Length; index++)
            {
                keys[index] = Convert.ToUInt64(values[index], CultureInfo.InvariantCulture);
            }

            Array.Sort(keys, values);
        }
        else
        {
            var keys = new long[values.Length];
            for (var index = 0; index < values.Length; index++)
            {
                keys[index] = Convert.ToInt64(values[index], CultureInfo.InvariantCulture);
            }

            Array.Sort(keys, values);
        }

        Values = values;
    }
}

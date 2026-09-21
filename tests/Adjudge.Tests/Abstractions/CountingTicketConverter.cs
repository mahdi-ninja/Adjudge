using System.Text.Json;
using System.Text.Json.Serialization;

namespace Adjudge.Tests.Abstractions;

internal sealed class CountingTicketConverter : JsonConverter<Ticket>
{
    private int _writes;

    public int Writes => Volatile.Read(ref _writes);

    public override Ticket Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException();

    public override void Write(Utf8JsonWriter writer, Ticket value, JsonSerializerOptions options)
    {
        Interlocked.Increment(ref _writes);

        writer.WriteStartObject();
        writer.WriteString("subject", value.Subject);
        writer.WriteEndObject();
    }
}

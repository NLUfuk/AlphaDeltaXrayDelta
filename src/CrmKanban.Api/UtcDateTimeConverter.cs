using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrmKanban.Api;

// DB datetime2 columns materialize as DateTimeKind.Unspecified, so the default serializer omits the
// 'Z' and browsers then read a UTC instant as local time. Every timestamp here is UTC (clock.UtcNow),
// so stamp a 'Z' on write in one place — the client can localize (Europe/Istanbul) reliably.
public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
        => DateTime.SpecifyKind(reader.GetDateTime(), DateTimeKind.Utc);

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions o)
        => writer.WriteStringValue(DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
}

public sealed class NullableUtcDateTimeConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
        => reader.TokenType == JsonTokenType.Null ? null : DateTime.SpecifyKind(reader.GetDateTime(), DateTimeKind.Utc);

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions o)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
    }
}

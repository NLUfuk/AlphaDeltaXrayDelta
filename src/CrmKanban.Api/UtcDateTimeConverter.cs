using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrmKanban.Api;

// DB datetime2 columns materialize as DateTimeKind.Unspecified, so the default serializer omits the
// 'Z' and browsers then read a UTC instant as local time. Every timestamp here is UTC (clock.UtcNow),
// so stamp a 'Z' on write in one place — the client can localize (Europe/Istanbul) reliably.
// The format is written with InvariantCulture: the host's culture is not ours to choose (shared hosting),
// and a non-Gregorian or non-ASCII-digit culture would otherwise emit a timestamp no client can parse.
public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    internal const string Format = "yyyy-MM-ddTHH:mm:ss.fffZ";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => DateTime.SpecifyKind(reader.GetDateTime(), DateTimeKind.Utc);

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString(Format, CultureInfo.InvariantCulture));
}

public sealed class NullableUtcDateTimeConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.Null ? null : DateTime.SpecifyKind(reader.GetDateTime(), DateTimeKind.Utc);

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else writer.WriteStringValue(
            DateTime.SpecifyKind(value.Value, DateTimeKind.Utc).ToString(UtcDateTimeConverter.Format, CultureInfo.InvariantCulture));
    }
}

using System.Text.Json;

namespace AvroCodeGen.Runtime;

/// <summary>Avro JSON encoding writer. Call <see cref="Flush"/> when finished.</summary>
public sealed class AvroJsonWriter(Stream output, bool indented = false) : IAvroWriter
{
    private readonly Utf8JsonWriter _json = new(output, new JsonWriterOptions { Indented = indented });
    private readonly Stack<bool> _unionWrapped = new();

    public void Flush() => _json.Flush();

    public void WriteNull() => _json.WriteNullValue();
    public void WriteBoolean(bool value) => _json.WriteBooleanValue(value);
    public void WriteInt(int value) => _json.WriteNumberValue(value);
    public void WriteLong(long value) => _json.WriteNumberValue(value);
    public void WriteFloat(float value) => _json.WriteNumberValue(value);
    public void WriteDouble(double value) => _json.WriteNumberValue(value);
    public void WriteBytes(ReadOnlySpan<byte> value) => _json.WriteStringValue(JsonBytes.ToString(value));
    public void WriteString(string value) => _json.WriteStringValue(value);
    public void WriteFixed(ReadOnlySpan<byte> value) => WriteBytes(value);
    public void WriteEnum(int index, string[] symbols) => _json.WriteStringValue(symbols[index]);

    public void WriteRecordStart() => _json.WriteStartObject();
    public void WriteField(string name) => _json.WritePropertyName(name);
    public void WriteRecordEnd() => _json.WriteEndObject();

    public void WriteArrayStart(int count) => _json.WriteStartArray();
    public void WriteArrayEnd() => _json.WriteEndArray();

    public void WriteMapStart(int count) => _json.WriteStartObject();
    public void WriteMapKey(string key) => _json.WritePropertyName(key);
    public void WriteMapEnd() => _json.WriteEndObject();

    public void WriteUnion(int index, string branchName)
    {
        bool wrap = branchName != "null";
        _unionWrapped.Push(wrap);
        if (wrap)
        {
            _json.WriteStartObject();
            _json.WritePropertyName(branchName);
        }
    }

    public void WriteUnionEnd()
    {
        if (_unionWrapped.Pop()) _json.WriteEndObject();
    }
}

internal static class JsonBytes
{
    // Avro JSON maps each byte to a code point 0-255 (ISO-8859-1).
    public static string ToString(ReadOnlySpan<byte> bytes) => string.Create(bytes.Length, bytes.ToArray(), (span, b) =>
    {
        for (int i = 0; i < b.Length; i++) span[i] = (char)b[i];
    });

    public static byte[] FromString(string s)
    {
        var result = new byte[s.Length];
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] > 255) throw new AvroException("Invalid character in Avro JSON bytes value.");
            result[i] = (byte)s[i];
        }
        return result;
    }
}

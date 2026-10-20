namespace AvroCodeGen.Runtime;

/// <summary>Serializes and deserializes generated Avro record types.</summary>
public static class AvroSerializer
{
    public static byte[] Serialize<T>(T value, AvroFormat format = AvroFormat.Binary) where T : IAvroRecord<T>
    {
        using var ms = new MemoryStream();
        Serialize(value, ms, format);
        return ms.ToArray();
    }

    public static void Serialize<T>(T value, Stream output, AvroFormat format = AvroFormat.Binary) where T : IAvroRecord<T>
    {
        ArgumentNullException.ThrowIfNull(value);
        switch (format)
        {
            case AvroFormat.Binary:
                value.Write(new AvroBinaryWriter(output));
                break;
            case AvroFormat.Json:
                var json = new AvroJsonWriter(output);
                value.Write(json);
                json.Flush();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format));
        }
    }

    public static string SerializeToJson<T>(T value) where T : IAvroRecord<T> =>
        System.Text.Encoding.UTF8.GetString(Serialize(value, AvroFormat.Json));

    public static T Deserialize<T>(ReadOnlyMemory<byte> data, AvroFormat format = AvroFormat.Binary) where T : IAvroRecord<T>
    {
        try
        {
            IAvroReader reader = format switch
            {
                AvroFormat.Binary => new AvroBinaryReader(data),
                AvroFormat.Json => new AvroJsonReader(data),
                _ => throw new ArgumentOutOfRangeException(nameof(format))
            };
            return T.Read(reader);
        }
        catch (Exception ex) when (ex is not AvroException and not ArgumentException)
        {
            throw new AvroException($"Failed to deserialize {typeof(T).Name}: {ex.Message}", ex);
        }
    }

    public static T Deserialize<T>(Stream input, AvroFormat format = AvroFormat.Binary) where T : IAvroRecord<T>
    {
        using var ms = new MemoryStream();
        input.CopyTo(ms);
        return Deserialize<T>(ms.ToArray(), format);
    }

    public static T DeserializeFromJson<T>(string json) where T : IAvroRecord<T> =>
        Deserialize<T>(System.Text.Encoding.UTF8.GetBytes(json), AvroFormat.Json);
}

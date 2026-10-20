using System.Text.Json;

namespace AvroCodeGen.Runtime;

/// <summary>Avro JSON encoding reader. Record fields may appear in any order.</summary>
public sealed class AvroJsonReader : IAvroReader
{
    private sealed class ArrayFrame(JsonElement.ArrayEnumerator e) { public JsonElement.ArrayEnumerator E = e; }
    private sealed class MapFrame(JsonElement.ObjectEnumerator e) { public JsonElement.ObjectEnumerator E = e; public string Key = ""; }

    private readonly JsonDocument _doc;
    private readonly Stack<object> _frames = new();
    private JsonElement _pending;
    private readonly Stack<JsonElement> _records = new();

    public AvroJsonReader(ReadOnlyMemory<byte> json)
    {
        _doc = JsonDocument.Parse(json);
        _pending = _doc.RootElement;
    }

    private JsonElement Take(JsonValueKind kind)
    {
        if (_pending.ValueKind != kind) throw new AvroException($"Expected JSON {kind} but found {_pending.ValueKind}.");
        return _pending;
    }

    public void ReadNull() => Take(JsonValueKind.Null);

    public bool ReadBoolean() => _pending.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => throw new AvroException($"Expected JSON boolean but found {_pending.ValueKind}.")
    };

    public int ReadInt() => Take(JsonValueKind.Number).TryGetInt32(out var v) ? v : throw new AvroException("Value is not a valid int.");
    public long ReadLong() => Take(JsonValueKind.Number).TryGetInt64(out var v) ? v : throw new AvroException("Value is not a valid long.");
    public float ReadFloat() => Take(JsonValueKind.Number).GetSingle();
    public double ReadDouble() => Take(JsonValueKind.Number).GetDouble();
    public string ReadString() => Take(JsonValueKind.String).GetString()!;
    public byte[] ReadBytes() => JsonBytes.FromString(ReadString());

    public byte[] ReadFixed(int size)
    {
        var bytes = ReadBytes();
        if (bytes.Length != size) throw new AvroException($"Expected fixed of size {size} but found {bytes.Length}.");
        return bytes;
    }

    public int ReadEnum(string[] symbols)
    {
        var s = ReadString();
        int i = Array.IndexOf(symbols, s);
        return i >= 0 ? i : throw new AvroException($"Unknown enum symbol '{s}'.");
    }

    public void ReadRecordStart()
    {
        _records.Push(Take(JsonValueKind.Object));
    }

    public void ReadField(string name)
    {
        if (!_records.Peek().TryGetProperty(name, out _pending))
            throw new AvroException($"Missing field '{name}'.");
    }

    public void ReadRecordEnd() => _records.Pop();

    public void ReadArrayStart() => _frames.Push(new ArrayFrame(Take(JsonValueKind.Array).EnumerateArray()));

    public bool NextArrayItem()
    {
        var f = (ArrayFrame)_frames.Peek();
        if (!f.E.MoveNext()) return false;
        _pending = f.E.Current;
        return true;
    }

    public void ReadArrayEnd() => _frames.Pop();

    public void ReadMapStart() => _frames.Push(new MapFrame(Take(JsonValueKind.Object).EnumerateObject()));

    public bool NextMapEntry()
    {
        var f = (MapFrame)_frames.Peek();
        if (!f.E.MoveNext()) return false;
        f.Key = f.E.Current.Name;
        _pending = f.E.Current.Value;
        return true;
    }

    public string ReadMapKey() => ((MapFrame)_frames.Peek()).Key;
    public void ReadMapEnd() => _frames.Pop();

    public int ReadUnionIndex(string[] branchNames)
    {
        if (_pending.ValueKind == JsonValueKind.Null)
        {
            int nullIndex = Array.IndexOf(branchNames, "null");
            return nullIndex >= 0 ? nullIndex : throw new AvroException("Union has no null branch.");
        }

        var obj = Take(JsonValueKind.Object);
        using var e = obj.EnumerateObject();
        if (!e.MoveNext()) throw new AvroException("Empty union object.");
        var prop = e.Current;
        int index = Array.IndexOf(branchNames, prop.Name);
        if (index < 0) throw new AvroException($"Unknown union branch '{prop.Name}'.");
        _pending = prop.Value;
        return index;
    }

    public void ReadUnionEnd() { }
}

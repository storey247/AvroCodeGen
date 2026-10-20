using System.Buffers.Binary;
using System.Text;

namespace AvroCodeGen.Runtime;

/// <summary>Avro binary encoding reader.</summary>
public sealed class AvroBinaryReader(ReadOnlyMemory<byte> data) : IAvroReader
{
    private int _pos;
    private readonly Stack<long> _remaining = new();

    public void ReadNull() { }
    public bool ReadBoolean() => ReadByte() != 0;
    public int ReadInt() => checked((int)ReadVarLong());
    public long ReadLong() => ReadVarLong();
    public float ReadFloat() => BinaryPrimitives.ReadSingleLittleEndian(Take(4));
    public double ReadDouble() => BinaryPrimitives.ReadDoubleLittleEndian(Take(8));
    public byte[] ReadBytes() => Take(ReadLength()).ToArray();
    public string ReadString() => Encoding.UTF8.GetString(Take(ReadLength()));
    public byte[] ReadFixed(int size) => Take(size).ToArray();

    public int ReadEnum(string[] symbols)
    {
        int index = ReadInt();
        if ((uint)index >= (uint)symbols.Length) throw new AvroException($"Enum index {index} out of range.");
        return index;
    }

    public void ReadRecordStart() { }
    public void ReadField(string name) { }
    public void ReadRecordEnd() { }

    public void ReadArrayStart() => _remaining.Push(0);
    public bool NextArrayItem() => NextBlockItem();
    public void ReadArrayEnd() => _remaining.Pop();

    public void ReadMapStart() => _remaining.Push(0);
    public bool NextMapEntry() => NextBlockItem();
    public string ReadMapKey() => ReadString();
    public void ReadMapEnd() => _remaining.Pop();

    public int ReadUnionIndex(string[] branchNames)
    {
        int index = ReadInt();
        if ((uint)index >= (uint)branchNames.Length) throw new AvroException($"Union index {index} out of range.");
        return index;
    }

    public void ReadUnionEnd() { }

    private bool NextBlockItem()
    {
        long remaining = _remaining.Pop();
        if (remaining == 0)
        {
            remaining = ReadVarLong();
            if (remaining < 0)
            {
                remaining = -remaining;
                ReadVarLong(); // block byte size, unused
            }
            if (remaining == 0)
            {
                _remaining.Push(0);
                return false;
            }
        }
        _remaining.Push(remaining - 1);
        return true;
    }

    private int ReadLength()
    {
        long len = ReadVarLong();
        if (len < 0 || len > int.MaxValue) throw new AvroException($"Invalid length {len}.");
        return (int)len;
    }

    private byte ReadByte()
    {
        if (_pos >= data.Length) throw new AvroException("Unexpected end of data.");
        return data.Span[_pos++];
    }

    private ReadOnlySpan<byte> Take(int count)
    {
        if (count > data.Length - _pos) throw new AvroException("Unexpected end of data.");
        var slice = data.Span.Slice(_pos, count);
        _pos += count;
        return slice;
    }

    private long ReadVarLong()
    {
        ulong value = 0;
        int shift = 0;
        while (true)
        {
            byte b = ReadByte();
            value |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
            if (shift > 63) throw new AvroException("Varint too long.");
        }
        return (long)(value >> 1) ^ -(long)(value & 1);
    }
}

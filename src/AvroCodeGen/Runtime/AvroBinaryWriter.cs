using System.Buffers.Binary;
using System.Text;

namespace AvroCodeGen.Runtime;

/// <summary>Avro binary encoding writer.</summary>
public sealed class AvroBinaryWriter(Stream output) : IAvroWriter
{
    public void WriteNull() { }
    public void WriteBoolean(bool value) => output.WriteByte(value ? (byte)1 : (byte)0);
    public void WriteInt(int value) => WriteVarLong(value);
    public void WriteLong(long value) => WriteVarLong(value);

    public void WriteFloat(float value)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(buf, value);
        output.Write(buf);
    }

    public void WriteDouble(double value)
    {
        Span<byte> buf = stackalloc byte[8];
        BinaryPrimitives.WriteDoubleLittleEndian(buf, value);
        output.Write(buf);
    }

    public void WriteBytes(ReadOnlySpan<byte> value)
    {
        WriteVarLong(value.Length);
        output.Write(value);
    }

    public void WriteString(string value) => WriteBytes(Encoding.UTF8.GetBytes(value));
    public void WriteFixed(ReadOnlySpan<byte> value) => output.Write(value);
    public void WriteEnum(int index, string[] symbols) => WriteVarLong(index);

    public void WriteRecordStart() { }
    public void WriteField(string name) { }
    public void WriteRecordEnd() { }

    public void WriteArrayStart(int count) { if (count > 0) WriteVarLong(count); }
    public void WriteArrayEnd() => WriteVarLong(0);

    public void WriteMapStart(int count) { if (count > 0) WriteVarLong(count); }
    public void WriteMapKey(string key) => WriteString(key);
    public void WriteMapEnd() => WriteVarLong(0);

    public void WriteUnion(int index, string branchName) => WriteVarLong(index);
    public void WriteUnionEnd() { }

    private void WriteVarLong(long value)
    {
        ulong n = (ulong)((value << 1) ^ (value >> 63));
        while (n > 0x7F)
        {
            output.WriteByte((byte)(n | 0x80));
            n >>= 7;
        }
        output.WriteByte((byte)n);
    }
}

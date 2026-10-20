namespace AvroCodeGen.Runtime;

/// <summary>Encoding-agnostic writer used by generated code.</summary>
public interface IAvroWriter
{
    void WriteNull();
    void WriteBoolean(bool value);
    void WriteInt(int value);
    void WriteLong(long value);
    void WriteFloat(float value);
    void WriteDouble(double value);
    void WriteBytes(ReadOnlySpan<byte> value);
    void WriteString(string value);
    void WriteFixed(ReadOnlySpan<byte> value);
    void WriteEnum(int index, string[] symbols);

    void WriteRecordStart();
    void WriteField(string name);
    void WriteRecordEnd();

    void WriteArrayStart(int count);
    void WriteArrayEnd();

    void WriteMapStart(int count);
    void WriteMapKey(string key);
    void WriteMapEnd();

    void WriteUnion(int index, string branchName);
    void WriteUnionEnd();
}

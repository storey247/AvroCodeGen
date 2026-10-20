namespace AvroCodeGen.Runtime;

/// <summary>Encoding-agnostic reader used by generated code.</summary>
public interface IAvroReader
{
    void ReadNull();
    bool ReadBoolean();
    int ReadInt();
    long ReadLong();
    float ReadFloat();
    double ReadDouble();
    byte[] ReadBytes();
    string ReadString();
    byte[] ReadFixed(int size);
    int ReadEnum(string[] symbols);

    void ReadRecordStart();
    void ReadField(string name);
    void ReadRecordEnd();

    void ReadArrayStart();
    bool NextArrayItem();
    void ReadArrayEnd();

    void ReadMapStart();
    bool NextMapEntry();
    string ReadMapKey();
    void ReadMapEnd();

    int ReadUnionIndex(string[] branchNames);
    void ReadUnionEnd();
}

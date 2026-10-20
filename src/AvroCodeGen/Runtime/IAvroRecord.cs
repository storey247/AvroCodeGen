namespace AvroCodeGen.Runtime;

/// <summary>Implemented by generated record types.</summary>
public interface IAvroRecord<TSelf> where TSelf : IAvroRecord<TSelf>
{
    static abstract string SchemaJson { get; }
    static abstract TSelf Read(IAvroReader reader);
    void Write(IAvroWriter writer);
}

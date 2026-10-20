namespace AvroCodeGen.Generation;

public sealed class CodeGenOptions
{
    /// <summary>Namespace used for all types when set (unless overridden by <see cref="NamespaceMap"/>).</summary>
    public string? Namespace { get; set; }

    /// <summary>Maps Avro namespaces to C# namespaces.</summary>
    public Dictionary<string, string> NamespaceMap { get; } = new();
}

public sealed record GeneratedFile(string FileName, string Content);

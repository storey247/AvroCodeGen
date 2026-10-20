using System.Text;
using Avro;
using AvroException = AvroCodeGen.Runtime.AvroException;

namespace AvroCodeGen.Generation;

/// <summary>Generates C# source for the named types (records, enums, fixed) in a set of Avro schemas.</summary>
public static class CodeGenerator
{
    public static IReadOnlyList<GeneratedFile> Generate(IEnumerable<NamedSchema> schemas, CodeGenOptions? options = null)
    {
        options ??= new CodeGenOptions();
        var naming = new Naming(options);
        var files = new List<GeneratedFile>();
        foreach (var schema in schemas)
        {
            var emitter = new TypeEmitter(naming, new TypeMapper(naming));
            var source = schema switch
            {
                RecordSchema r => emitter.EmitRecord(r),
                EnumSchema e => emitter.EmitEnum(e),
                FixedSchema f => emitter.EmitFixed(f),
                _ => throw new AvroException($"Unsupported named schema '{schema.Fullname}'.")
            };
            files.Add(new GeneratedFile($"{naming.Namespace(schema.Namespace)}.{naming.TypeName(schema)}.cs", source));
        }

        var dup = files.GroupBy(f => f.FileName).FirstOrDefault(g => g.Count() > 1);
        if (dup is not null) throw new AvroException($"Multiple schemas map to the same C# type '{dup.Key}'.");
        return files;
    }

    /// <summary>Loads schemas from <paramref name="schemaPath"/> and writes generated files to <paramref name="outputDir"/>.</summary>
    public static IReadOnlyList<string> GenerateToDirectory(string schemaPath, string outputDir, CodeGenOptions? options = null)
    {
        var files = Generate(SchemaLoader.Load(schemaPath), options);
        Directory.CreateDirectory(outputDir);
        var paths = new List<string>();
        foreach (var f in files)
        {
            var path = Path.Combine(outputDir, f.FileName);
            File.WriteAllText(path, f.Content, new UTF8Encoding(false));
            paths.Add(path);
        }
        return paths;
    }
}

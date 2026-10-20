using System.Reflection;
using AvroCodeGen.Generation;
using AvroCodeGen.Runtime;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AvroCodeGen.Tests;

public class GeneratorTests
{
    private static Assembly CompileGenerated(IEnumerable<(string Name, string Json)> schemas, CodeGenOptions? options = null)
    {
        var files = CodeGenerator.Generate(SchemaLoader.LoadJson(schemas), options);
        var trees = files.Select(f => CSharpSyntaxTree.ParseText(f.Content, path: f.FileName));
        var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(IAvroRecord<>).Assembly.Location));
        var compilation = CSharpCompilation.Create("Gen" + Guid.NewGuid().ToString("N"), trees, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);
        result.Success.Should().BeTrue("generated code should compile without errors: {0}",
            string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        return Assembly.Load(ms.ToArray());
    }

    [Fact]
    public void Edge_case_names_compile()
    {
        const string schema = """
        {"type":"record","name":"class","namespace":"my.ns","fields":[
          {"name":"class","type":"string"},
          {"name":"int","type":"int"},
          {"name":"Write","type":"int"},
          {"name":"user_name","type":"string"},
          {"name":"kind","type":{"type":"enum","name":"kind","symbols":["A_B","c","string"]}},
          {"name":"next","type":["null","class"]},
          {"name":"nested","type":{"type":"map","values":{"type":"array","items":["null","long"]}}},
          {"name":"unknownLogical","type":{"type":"long","logicalType":"made-up"}},
          {"name":"dur","type":{"type":"fixed","name":"dur","size":12,"logicalType":"duration"}}
        ]}
        """;
        var asm = CompileGenerated([("a.avsc", schema)]);
        asm.GetType("My.Ns.Class").Should().NotBeNull();
        Enum.GetNames(asm.GetType("My.Ns.Kind")!).Should().Equal("AB", "C", "String");
    }

    [Fact]
    public void Namespace_override_and_map_are_applied()
    {
        const string a = """{"type":"record","name":"A","namespace":"x.y","fields":[{"name":"b","type":"b.B"}]}""";
        const string b = """{"type":"record","name":"B","namespace":"b","fields":[]}""";
        var options = new CodeGenOptions { Namespace = "Default.Ns" };
        options.NamespaceMap["b"] = "Mapped.B";
        var asm = CompileGenerated([("a.avsc", a), ("b.avsc", b)], options);
        asm.GetType("Default.Ns.A").Should().NotBeNull();
        asm.GetType("Mapped.B.B").Should().NotBeNull();
    }

    [Fact]
    public void Schemas_may_reference_types_from_later_files()
    {
        const string a = """{"type":"record","name":"A","fields":[{"name":"b","type":"B"}]}""";
        const string b = """{"type":"record","name":"B","fields":[{"name":"x","type":"int"}]}""";
        var names = SchemaLoader.LoadJson([("a.avsc", a), ("b.avsc", b)]).Select(s => s.Fullname);
        names.Should().Equal("A", "B");
    }

    [Fact]
    public void Invalid_schema_reports_file_name()
    {
        Action load = () => SchemaLoader.LoadJson([("bad.avsc", """{"type":"record","name":"X","fields":[{"name":"f","type":"Missing"}]}""")]);
        load.Should().ThrowExactly<AvroException>().WithMessage("*bad.avsc*");
    }

    [Fact]
    public void Missing_path_throws()
    {
        Action load = () => SchemaLoader.Load("/definitely/not/here");
        load.Should().ThrowExactly<AvroException>();
    }

    [Fact]
    public void GenerateToDirectory_writes_files()
    {
        var dir = Path.Combine(Path.GetTempPath(), "avrogen-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = CodeGenerator.GenerateToDirectory(Path.Combine(AppContext.BaseDirectory, "../../../Schemas"), dir);
            paths.Should().AllSatisfy(p => File.Exists(p).Should().BeTrue("generated file {0} should exist", p));
            paths.Should().Contain(p => p.EndsWith("Test.Basic.Basic.cs"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Theory]
    [InlineData(0L, new byte[] { 0 })]
    [InlineData(-1L, new byte[] { 1 })]
    [InlineData(1L, new byte[] { 2 })]
    [InlineData(64L, new byte[] { 0x80, 0x01 })]
    public void Binary_varint_matches_spec(long value, byte[] expected)
    {
        using var ms = new MemoryStream();
        new AvroBinaryWriter(ms).WriteLong(value);
        ms.ToArray().Should().Equal(expected);
        new AvroBinaryReader(expected).ReadLong().Should().Be(value);
    }
}

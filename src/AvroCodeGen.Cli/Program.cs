using System.CommandLine;
using AvroCodeGen.Generation;

var schemaOption = new Option<string>("--schema", "-s") { Description = "Path to an .avsc file or a directory of .avsc files.", Required = true };
var outputOption = new Option<string>("--output", "-o") { Description = "Directory to write generated C# files to.", Required = true };
var namespaceOption = new Option<string?>("--namespace", "-n") { Description = "C# namespace for all generated types (overrides Avro namespaces)." };
var mapOption = new Option<string[]>("--namespace-map", "-m") { Description = "Map an Avro namespace to a C# namespace: avro.ns=CSharp.Ns (repeatable).", AllowMultipleArgumentsPerToken = true };

var generate = new Command("generate", "Generate C# models and serializers from Avro schema files.")
{
    schemaOption, outputOption, namespaceOption, mapOption
};
generate.SetAction(parse =>
{
    try
    {
        var options = new CodeGenOptions { Namespace = parse.GetValue(namespaceOption) };
        foreach (var entry in parse.GetValue(mapOption) ?? [])
        {
            var parts = entry.Split('=', 2);
            if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
            {
                Console.Error.WriteLine($"Invalid --namespace-map '{entry}'. Expected avro.ns=CSharp.Ns.");
                return 2;
            }
            options.NamespaceMap[parts[0]] = parts[1];
        }

        var files = CodeGenerator.GenerateToDirectory(parse.GetRequiredValue(schemaOption), parse.GetRequiredValue(outputOption), options);
        foreach (var f in files) Console.WriteLine(f);
        Console.WriteLine($"Generated {files.Count} file(s).");
        return 0;
    }
    catch (Exception ex) when (ex is AvroCodeGen.Runtime.AvroException or IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"error: {ex.Message}");
        return 1;
    }
});

var validateSchema = new Argument<string>("schema") { Description = "Path to an .avsc file or a directory of .avsc files." };
var validate = new Command("validate", "Validate Avro schema files without generating code.") { validateSchema };
validate.SetAction(parse =>
{
    try
    {
        var schemas = SchemaLoader.Load(parse.GetRequiredValue(validateSchema));
        Console.WriteLine($"OK: {schemas.Count} named type(s).");
        return 0;
    }
    catch (Exception ex) when (ex is AvroCodeGen.Runtime.AvroException or IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"error: {ex.Message}");
        return 1;
    }
});

var root = new RootCommand("Avro C# code generator") { generate, validate };
return root.Parse(args).Invoke();

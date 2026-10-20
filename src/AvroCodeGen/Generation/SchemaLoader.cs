using Avro;
using AvroException = AvroCodeGen.Runtime.AvroException;

namespace AvroCodeGen.Generation;

/// <summary>Loads .avsc files, resolving named types shared across files.</summary>
public static class SchemaLoader
{
    public static IReadOnlyList<NamedSchema> Load(string path)
    {
        var files = File.Exists(path)
            ? [path]
            : Directory.Exists(path)
                ? Directory.EnumerateFiles(path, "*.avsc", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList()
                : throw new AvroException($"Schema path '{path}' does not exist.");

        if (files.Count == 0) throw new AvroException($"No .avsc files found in '{path}'.");

        return LoadJson(files.Select(f => (f, File.ReadAllText(f))));
    }

    public static IReadOnlyList<NamedSchema> LoadJson(IEnumerable<(string Name, string Json)> sources)
    {
        var names = new SchemaNames();
        var pending = sources.ToList();

        // A file may reference types defined in another file, so retry until no progress is made.
        while (pending.Count > 0)
        {
            var failures = new List<(string Name, Exception Error)>();
            var next = new List<(string Name, string Json)>();
            bool progress = false;

            foreach (var source in pending)
            {
                var attempt = Clone(names);
                try
                {
                    Schema.Parse(source.Json, attempt, null);
                    names = attempt;
                    progress = true;
                }
                catch (Exception ex)
                {
                    failures.Add((source.Name, ex));
                    next.Add(source);
                }
            }

            if (!progress)
                throw new AvroException("Failed to parse schema(s):" + Environment.NewLine +
                    string.Join(Environment.NewLine, failures.Select(f => $"  {f.Name}: {f.Error.Message}")));

            pending = next;
        }

        var all = new List<NamedSchema>();
        foreach (var kv in names) all.Add(kv.Value);
        return all.DistinctBy(s => s.Fullname).OrderBy(s => s.Fullname, StringComparer.Ordinal).ToList();
    }

    private static SchemaNames Clone(SchemaNames source)
    {
        var copy = new SchemaNames();
        foreach (var kv in source) copy.Add(kv.Key, kv.Value);
        return copy;
    }
}

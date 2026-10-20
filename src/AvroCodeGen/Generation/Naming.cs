using System.Text;
using Avro;

namespace AvroCodeGen.Generation;

internal sealed class Naming(CodeGenOptions options)
{
    private static readonly HashSet<string> ReservedMembers =
        ["Write", "Read", "SchemaJson", "EqualityContract", "Equals", "GetHashCode", "ToString", "GetType", "Clone", "Deconstruct", "PrintMembers"];

    public static string Pascal(string name)
    {
        var sb = new StringBuilder();
        foreach (var segment in SplitSegments(name))
        {
            if (segment.Length == 0) continue;
            // ALL_CAPS segments (typical enum symbols) become Capitalised; mixed-case segments keep their humps.
            var rest = segment.Any(char.IsLower) ? segment[1..] : segment[1..].ToLowerInvariant();
            sb.Append(char.ToUpperInvariant(segment[0])).Append(rest);
        }
        if (sb.Length == 0) return "_";
        if (char.IsDigit(sb[0])) sb.Insert(0, '_');
        return sb.ToString();
    }

    private static IEnumerable<string> SplitSegments(string name) =>
        new string(name.Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray()).Split(' ');

    public string Namespace(string? avroNamespace)
    {
        if (!string.IsNullOrEmpty(avroNamespace) && options.NamespaceMap.TryGetValue(avroNamespace, out var mapped)) return mapped;
        if (!string.IsNullOrEmpty(options.Namespace)) return options.Namespace;
        if (string.IsNullOrEmpty(avroNamespace)) return "Avro.Generated";
        return string.Join('.', avroNamespace.Split('.').Select(Pascal));
    }

    public string TypeName(NamedSchema s) => Pascal(s.Name);
    public string FullTypeName(NamedSchema s) => $"global::{Namespace(s.Namespace)}.{TypeName(s)}";

    public static string Member(string avroName, string enclosingType)
    {
        var name = Pascal(avroName);
        return name == enclosingType || ReservedMembers.Contains(name) ? name + "_" : name;
    }

    public static string Literal(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (char c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 32 || c > 126) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.Append('"').ToString();
    }
}

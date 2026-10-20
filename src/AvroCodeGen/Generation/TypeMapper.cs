using Avro;

namespace AvroCodeGen.Generation;

internal sealed class TypeMapper(Naming naming)
{
    private static readonly HashSet<string> SupportedLogical =
        ["decimal", "uuid", "date", "time-millis", "time-micros", "timestamp-millis", "timestamp-micros"];

    /// <summary>Unwraps logical schemas we do not support (they are encoded as their base type).</summary>
    public static Schema Normalize(Schema s)
    {
        if (s is not LogicalSchema ls) return s;
        bool ok = SupportedLogical.Contains(ls.LogicalTypeName) && ls.BaseSchema.Tag switch
        {
            Schema.Type.Bytes or Schema.Type.Fixed => ls.LogicalTypeName == "decimal",
            Schema.Type.String => ls.LogicalTypeName == "uuid",
            Schema.Type.Int => ls.LogicalTypeName is "date" or "time-millis",
            Schema.Type.Long => ls.LogicalTypeName is "time-micros" or "timestamp-millis" or "timestamp-micros",
            _ => false
        };
        return ok ? s : Normalize(ls.BaseSchema);
    }

    /// <summary>Returns (inner, isNullable) when the union is [null, T] in either order.</summary>
    public static bool IsNullable(UnionSchema u, out Schema inner)
    {
        inner = null!;
        if (u.Count != 2) return false;
        var nullIdx = u.Schemas[0].Tag == Schema.Type.Null ? 0 : u.Schemas[1].Tag == Schema.Type.Null ? 1 : -1;
        if (nullIdx < 0) return false;
        inner = u.Schemas[1 - nullIdx];
        return true;
    }

    public string CsType(Schema schema)
    {
        var s = Normalize(schema);
        return s switch
        {
            LogicalSchema l => l.LogicalTypeName switch
            {
                "decimal" => "decimal",
                "uuid" => "global::System.Guid",
                "date" => "global::System.DateOnly",
                "time-millis" or "time-micros" => "global::System.TimeOnly",
                _ => "global::System.DateTimeOffset"
            },
            UnionSchema u => IsNullable(u, out var inner) ? CsType(inner) + "?" : u.Schemas.Any(x => x.Tag == Schema.Type.Null) ? "object?" : "object",
            ArraySchema a => $"global::System.Collections.Generic.IReadOnlyList<{CsType(a.ItemSchema)}>",
            MapSchema m => $"global::System.Collections.Generic.IReadOnlyDictionary<string, {CsType(m.ValueSchema)}>",
            NamedSchema n => naming.FullTypeName(n),
            _ => s.Tag switch
            {
                Schema.Type.Null => "object?",
                Schema.Type.Boolean => "bool",
                Schema.Type.Int => "int",
                Schema.Type.Long => "long",
                Schema.Type.Float => "float",
                Schema.Type.Double => "double",
                Schema.Type.Bytes => "byte[]",
                Schema.Type.String => "string",
                _ => throw new NotSupportedException($"Unsupported schema type {s.Tag}.")
            }
        };
    }

    /// <summary>Name used for this branch in a union (JSON encoding and binary index lookup).</summary>
    public static string BranchName(Schema schema)
    {
        var s = schema is LogicalSchema l ? l.BaseSchema : schema;
        return s switch
        {
            NamedSchema n => n.Fullname,
            ArraySchema => "array",
            MapSchema => "map",
            _ => s.Tag switch
            {
                Schema.Type.Null => "null",
                Schema.Type.Boolean => "boolean",
                Schema.Type.Int => "int",
                Schema.Type.Long => "long",
                Schema.Type.Float => "float",
                Schema.Type.Double => "double",
                Schema.Type.Bytes => "bytes",
                Schema.Type.String => "string",
                _ => throw new NotSupportedException($"Unsupported schema type {s.Tag}.")
            }
        };
    }
}

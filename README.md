# AvroCodeGen

Generate C# models from Avro `.avsc` schemas and (de)serialise them to Avro binary or Avro JSON, with no reflection.

- `src/AvroCodeGen` – class library: schema loading (via Apache.Avro), code generator, runtime codecs, `AvroSerializer`
- `src/AvroCodeGen.Cli` – `avrogen` dotnet tool
- `tests/AvroCodeGen.Tests` – xUnit tests with AwesomeAssertions (generated code is built from `samples/` and `tests/.../Schemas` at test build time and cross-checked against Apache.Avro)

## CLI

```
avrogen generate --schema <file|dir> --output <dir> [--namespace Ns] [--namespace-map avro.ns=Csharp.Ns]
avrogen validate <file|dir>
```

Install locally: `dotnet pack src/AvroCodeGen.Cli -o nupkg && dotnet tool install -g AvroCodeGen.Tool --add-source nupkg`.
Directories are scanned recursively; schemas may reference named types defined in other files.

## Generated code

One file per named type: records → `sealed record` implementing `IAvroRecord<T>`; enums → `enum`; fixed → record with `byte[] Value`.

| Avro | C# |
|---|---|
| null-union `["null", T]` | `T?` |
| other unions | `object` (branch chosen by runtime type) |
| array / map | `IReadOnlyList<T>` / `IReadOnlyDictionary<string, T>` |
| decimal, uuid, date, time-*, timestamp-* | `decimal`, `Guid`, `DateOnly`, `TimeOnly`, `DateTimeOffset` |

Unsupported logical types are encoded as their underlying type.

## Worked example: schema to generated records

The repository includes [`samples/address.avsc`](samples/address.avsc):

```json
{
  "type": "record",
  "name": "Address",
  "namespace": "com.example",
  "doc": "A postal address.",
  "fields": [
    {"name": "street", "type": "string"},
    {"name": "city", "type": "string"},
    {"name": "postal_code", "type": ["null", "string"], "default": null}
  ]
}
```

Generate the sample models into a consuming project's `Generated` directory:

```sh
avrogen generate --schema samples --output src/MyApp/Generated
```

This produces `Com.Example.Address.cs`, `Com.Example.User.cs`, `Com.Example.Status.cs`, `Com.Example.Token.cs` and `Com.Example.Fee.cs`. A normal SDK-style C# project automatically compiles these files; add a reference to the `AvroCodeGen` runtime library to that project.

### Address

The generated model has this shape (the embedded schema and generated method bodies are omitted here):

```csharp
namespace Com.Example;

/// <summary>
/// A postal address.
/// </summary>
public sealed record Address : global::AvroCodeGen.Runtime.IAvroRecord<Address>
{
    public static string SchemaJson => /* embedded Avro schema JSON */;

    public required string Street { get; init; }
    public required string City { get; init; }
    public string? PostalCode { get; init; }

    public void Write(global::AvroCodeGen.Runtime.IAvroWriter w) { /* generated */ }
    public static Address Read(global::AvroCodeGen.Runtime.IAvroReader r) { /* generated */ }
}
```

Avro field names become PascalCase properties: `postal_code` becomes `PostalCode`. Non-nullable fields are `required`; the nullable union becomes `string?`. The generated `Read` and `Write` methods handle field order and union branches for both encodings without reflection.

### Related record, enum and fixed types

[`samples/user.avsc`](samples/user.avsc) demonstrates references to `Address`, collections, nullable values and logical types. Selected properties from its generated `User` record are shown below; other required properties and generated methods are omitted:

```csharp
namespace Com.Example;

public sealed record User : global::AvroCodeGen.Runtime.IAvroRecord<User>
{
    public required global::System.Guid Id { get; init; }
    public required string Name { get; init; }
    public int? Age { get; init; }
    public required global::Com.Example.Status Status { get; init; }
    public required global::Com.Example.Token Token { get; init; }
    public required global::System.Collections.Generic.IReadOnlyList<string> Tags { get; init; }
    public required global::System.Collections.Generic.IReadOnlyList<global::Com.Example.Address> Addresses { get; init; }
    public global::Com.Example.Address? Primary { get; init; }
    public object? Payload { get; init; }
    public required global::System.DateOnly BirthDate { get; init; }
    public required decimal Balance { get; init; }

    // Other properties, SchemaJson, Read and Write omitted.
}
```

The same schema generates these supporting types (the internal enum-symbol helper is omitted):

```csharp
namespace Com.Example;

public enum Status
{
    Active,
    Suspended,
    Deleted,
}

public sealed record Token
{
    public const int Size = 4;
    public required byte[] Value { get; init; }
}
```

### Use the generated record

Once the generated files are part of your project, construct and serialise an `Address` directly:

```csharp
using AvroCodeGen.Runtime;
using Com.Example;

var address = new Address
{
    Street = "1 Main Street",
    City = "London",
    PostalCode = "N1"
};

byte[] binary = AvroSerializer.Serialize(address);
Address fromBinary = AvroSerializer.Deserialize<Address>(binary);

string json = AvroSerializer.SerializeToJson(address);
Address fromJson = AvroSerializer.DeserializeFromJson<Address>(json);

// Record properties are init-only; use "with" to create a changed copy.
var withoutPostalCode = address with { PostalCode = null };
```

The JSON output is Avro JSON, so the non-null union value is wrapped with its Avro branch name:

```json
{"street":"1 Main Street","city":"London","postal_code":{"string":"N1"}}
```

Serialising `withoutPostalCode` instead produces:

```json
{"street":"1 Main Street","city":"London","postal_code":null}
```

The JSON retains the original Avro field names, not the C# property names. Binary output is a raw Avro datum: it does not include a schema or container header, so deserialise it using the generated type for the same schema.

## Limitations (v1)

`.avpr`/`.avdl`, schema evolution (reader/writer resolution), object container files and field defaults on generated properties are not supported. Record equality for `byte[]`, lists and maps is by reference.

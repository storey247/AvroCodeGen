using Avro;
using Avro.Generic;
using Avro.IO;
using AvroCodeGen.Runtime;
using Test.Basic;
using Schema = Avro.Schema;

namespace AvroCodeGen.Tests;

/// <summary>Cross-checks our encoders against the official Apache.Avro implementation.</summary>
public class ApacheInteropTests
{
    private static readonly RecordSchema ApacheSchema = (RecordSchema)Schema.Parse(Basic.SchemaJson);

    private static Basic Sample(object? multi) => new()
    {
        I = -7, L = long.MaxValue, F = 1.5f, D = -2.25, B = true, S = "héllo", Bytes = [0, 127, 128, 255],
        Color = Color.Blue, Hash = new Hash { Value = [1, 2, 3] }, Ints = [1, -2, 300000],
        Map = new Dictionary<string, string> { ["k"] = "v" }, Opt = "o", Multi = multi,
        Inner = new Inner { X = 5 }, Inners = [new Inner { X = 1 }, new Inner { X = 2 }]
    };

    private static GenericRecord ApacheRecord(object? multi)
    {
        var rec = new GenericRecord(ApacheSchema);
        rec.Add("i", -7); rec.Add("l", long.MaxValue); rec.Add("f", 1.5f); rec.Add("d", -2.25); rec.Add("b", true);
        rec.Add("s", "héllo"); rec.Add("bytes", new byte[] { 0, 127, 128, 255 });
        rec.Add("color", new GenericEnum((EnumSchema)ApacheSchema["color"].Schema, "BLUE"));
        var fixedValue = new GenericFixed((FixedSchema)ApacheSchema["hash"].Schema);
        fixedValue.Value = [1, 2, 3];
        rec.Add("hash", fixedValue);
        rec.Add("ints", new object[] { 1, -2, 300000 });
        rec.Add("map", new Dictionary<string, object> { ["k"] = "v" });
        rec.Add("opt", "o");
        rec.Add("multi", multi);
        var innerSchema = (RecordSchema)ApacheSchema["inner"].Schema;
        GenericRecord Inner(long x) { var r = new GenericRecord(innerSchema); r.Add("x", x); return r; }
        rec.Add("inner", Inner(5));
        rec.Add("inners", new object[] { Inner(1), Inner(2) });
        return rec;
    }

    public static IEnumerable<object?[]> Multis => [[null], [12], ["str"]];

    [Theory, MemberData(nameof(Multis))]
    public void Binary_matches_apache(object? multi)
    {
        using var ms = new MemoryStream();
        new GenericDatumWriter<GenericRecord>(ApacheSchema).Write(ApacheRecord(multi), new BinaryEncoder(ms));
        var apacheBytes = ms.ToArray();

        AvroSerializer.Serialize(Sample(multi)).Should().Equal(apacheBytes);
        var back = AvroSerializer.Deserialize<Basic>(apacheBytes);
        back.Multi.Should().Be(multi);
        back.I.Should().Be(-7);
        back.Color.Should().Be(Color.Blue);
    }

    [Theory, MemberData(nameof(Multis))]
    public void Json_is_compatible_with_apache(object? multi)
    {
        using var ms = new MemoryStream();
        var encoder = new JsonEncoder(ApacheSchema, ms);
        new GenericDatumWriter<GenericRecord>(ApacheSchema).Write(ApacheRecord(multi), encoder);
        encoder.Flush();

        var back = AvroSerializer.Deserialize<Basic>(ms.ToArray(), AvroFormat.Json);
        back.Multi.Should().Be(multi);
        back.Bytes.Should().Equal([0, 127, 128, 255]);

        // And Apache can read ours.
        var ours = AvroSerializer.Serialize(Sample(multi), AvroFormat.Json);
        var decoded = new GenericDatumReader<GenericRecord>(ApacheSchema, ApacheSchema)
            .Read(null!, new JsonDecoder(ApacheSchema, System.Text.Encoding.UTF8.GetString(ours)));
        decoded["s"].Should().Be("héllo");
    }

    [Fact]
    public void Apache_reads_our_binary()
    {
        var bytes = AvroSerializer.Serialize(Sample(12));
        var decoded = new GenericDatumReader<GenericRecord>(ApacheSchema, ApacheSchema)
            .Read(null!, new BinaryDecoder(new MemoryStream(bytes)));
        decoded["multi"].Should().Be(12);
        decoded["l"].Should().Be(long.MaxValue);
    }
}

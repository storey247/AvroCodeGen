using AvroCodeGen.Runtime;
using Com.Example;

namespace AvroCodeGen.Tests;

public class RoundTripTests
{
    private static User Sample() => new()
    {
        Id = Guid.Parse("4f0a5a0e-6c1c-4a53-9d5e-0b8d1f6d2a11"),
        Name = "Ada \"Lovelace\" ü",
        Age = 36,
        Score = 99.5,
        Ratio = 0.25f,
        Active = true,
        Visits = -1234567890123,
        Avatar = [1, 2, 3, 255],
        Status = Status.Suspended,
        Token = new Token { Value = [9, 8, 7, 6] },
        Tags = ["a", "b"],
        Attributes = new Dictionary<string, long> { ["x"] = 1, ["y"] = -2 },
        Addresses = [new Address { Street = "1 Main", City = "London", PostalCode = "N1" }],
        Primary = new Address { Street = "2 Side", City = "Leeds" },
        Payload = 42L,
        BirthDate = new DateOnly(1815, 12, 10),
        Alarm = new TimeOnly(6, 30, 15, 250),
        Created = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_123),
        Updated = new DateTimeOffset(DateTimeOffset.UnixEpoch.UtcTicks + 1_700_000_000_123_456L * 10, TimeSpan.Zero),
        Balance = -1234.56m,
        Fee = 12.345m,
        Friends = [null, new User
        {
            Id = Guid.Empty, Name = "Friend", Score = 0, Ratio = 0, Active = false, Visits = 0,
            Status = Status.Active, Token = new Token { Value = [0, 0, 0, 0] }, Tags = [],
            Attributes = new Dictionary<string, long>(), Addresses = [], Payload = new Address { Street = "s", City = "c" },
            BirthDate = new DateOnly(1970, 1, 1), Alarm = TimeOnly.MinValue,
            Created = DateTimeOffset.UnixEpoch, Updated = DateTimeOffset.UnixEpoch, Balance = 0m, Fee = -0.001m, Friends = []
        }]
    };

    [Theory]
    [InlineData(AvroFormat.Binary)]
    [InlineData(AvroFormat.Json)]
    public void User_round_trips(AvroFormat format)
    {
        var user = Sample();
        var bytes = AvroSerializer.Serialize(user, format);
        AvroSerializer.Deserialize<User>(bytes, format).Should().BeEquivalentTo(user,
            options => options.ComparingRecordsByMembers().WithStrictOrdering());
    }

    [Fact]
    public void Json_uses_avro_union_encoding()
    {
        var json = AvroSerializer.SerializeToJson(Sample());
        json.Should().Contain("\"age\":{\"int\":36}");
        json.Should().Contain("\"payload\":{\"long\":42}");
        json.Should().Contain("\"status\":\"SUSPENDED\"");
    }

    [Fact]
    public void Null_union_round_trips()
    {
        var user = Sample() with { Age = null, Avatar = null, Primary = null, Payload = null };
        foreach (var format in new[] { AvroFormat.Binary, AvroFormat.Json })
        {
            var back = AvroSerializer.Deserialize<User>(AvroSerializer.Serialize(user, format), format);
            back.Age.Should().BeNull();
            back.Avatar.Should().BeNull();
            back.Primary.Should().BeNull();
            back.Payload.Should().BeNull();
        }
    }

    [Fact]
    public void Multi_branch_union_selects_branch_by_type()
    {
        foreach (object payload in new object[] { "text", 7L, new Address { Street = "a", City = "b" } })
        {
            var back = AvroSerializer.Deserialize<User>(AvroSerializer.Serialize(Sample() with { Payload = payload }));
            back.Payload.Should().Be(payload);
        }
    }

    [Fact]
    public void Truncated_binary_throws_AvroException()
    {
        var bytes = AvroSerializer.Serialize(Sample());
        Action deserialize = () => AvroSerializer.Deserialize<User>(bytes.AsMemory(0, bytes.Length / 2));
        deserialize.Should().ThrowExactly<AvroException>();
    }

    [Fact]
    public void Invalid_json_throws_AvroException()
    {
        Action deserialize = () => AvroSerializer.DeserializeFromJson<User>("{\"id\":1}");
        deserialize.Should().ThrowExactly<AvroException>();
    }

    [Fact]
    public void Stream_overloads_work()
    {
        using var ms = new MemoryStream();
        AvroSerializer.Serialize(Sample(), ms, AvroFormat.Json);
        ms.Position = 0;
        AvroSerializer.Deserialize<User>(ms, AvroFormat.Json).Should().BeEquivalentTo(Sample(),
            options => options.ComparingRecordsByMembers().WithStrictOrdering());
    }
}

using System.Numerics;

namespace AvroCodeGen.Runtime;

/// <summary>Conversions for Avro logical types, used by generated code.</summary>
public static class AvroLogical
{
    private static readonly DateOnly Epoch = new(1970, 1, 1);

    public static int FromDate(DateOnly d) => d.DayNumber - Epoch.DayNumber;
    public static DateOnly ToDate(int days) => DateOnly.FromDayNumber(Epoch.DayNumber + days);

    public static int FromTimeMillis(TimeOnly t) => (int)(t.Ticks / TimeSpan.TicksPerMillisecond);
    public static TimeOnly ToTimeMillis(int ms) => new(ms * TimeSpan.TicksPerMillisecond);
    public static long FromTimeMicros(TimeOnly t) => t.Ticks / 10;
    public static TimeOnly ToTimeMicros(long us) => new(us * 10);

    public static long FromTimestampMillis(DateTimeOffset d) => d.ToUnixTimeMilliseconds();
    public static DateTimeOffset ToTimestampMillis(long ms) => DateTimeOffset.FromUnixTimeMilliseconds(ms);
    public static long FromTimestampMicros(DateTimeOffset d) => (d.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) / 10;
    public static DateTimeOffset ToTimestampMicros(long us) => new(DateTimeOffset.UnixEpoch.UtcTicks + us * 10, TimeSpan.Zero);

    public static string FromUuid(Guid g) => g.ToString();
    public static Guid ToUuid(string s) => Guid.Parse(s);

    /// <summary>Encodes as big-endian two's complement unscaled value; pads to <paramref name="fixedSize"/> when &gt; 0.</summary>
    public static byte[] FromDecimal(decimal value, int scale, int fixedSize = 0)
    {
        var scaled = decimal.Round(value, scale, MidpointRounding.ToZero) * Pow10(scale);
        var unscaled = new BigInteger(scaled);
        var bytes = unscaled.ToByteArray(isUnsigned: false, isBigEndian: true);
        if (fixedSize <= 0) return bytes;
        if (bytes.Length > fixedSize) throw new AvroException("Decimal value too large for fixed size.");
        var padded = new byte[fixedSize];
        Array.Fill(padded, unscaled.Sign < 0 ? (byte)0xFF : (byte)0);
        bytes.CopyTo(padded, fixedSize - bytes.Length);
        return padded;
    }

    public static decimal ToDecimal(byte[] bytes, int scale)
    {
        if (bytes.Length == 0) return 0m;
        var unscaled = new BigInteger(bytes, isUnsigned: false, isBigEndian: true);
        return (decimal)unscaled / Pow10(scale);
    }

    private static decimal Pow10(int scale)
    {
        decimal r = 1m;
        for (int i = 0; i < scale; i++) r *= 10m;
        return r;
    }
}

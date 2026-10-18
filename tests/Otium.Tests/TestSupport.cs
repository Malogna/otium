using System.Security.Cryptography;
using Otium.Core.Wallets;

namespace Otium.Tests;

public static class TestAddresses
{
    /// <summary>The Monero project's published general-fund donation address (a real, valid mainnet address).</summary>
    public const string GeneralFund = "44AFFq5kSiGBoZ4NMDwYtN18obc8AemS33DBLWs3H7otXft3XjrpDtQGv7SqSsaBYBb98uNbr2VBBEt7f2wfn3RVGQBEP3A";

    private const string Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
    private static readonly int[] EncodedBlockSizes = [0, 2, 3, 5, 6, 7, 9, 10, 11];

    /// <summary>Builds a well-formed standard address from random keys (valid checksum, unspendable).</summary>
    public static string Generate(byte tag = 18)
    {
        var data = new byte[69];
        data[0] = tag;
        RandomNumberGenerator.Fill(data.AsSpan(1, 64));
        Keccak256.Hash(data.AsSpan(0, 65)).AsSpan(0, 4).CopyTo(data.AsSpan(65));
        return Encode(data);
    }

    /// <summary>A well-formed USDT payout target on TRON ("USDT:T…"), the same for the same seed.</summary>
    public static string Usdt(int seed)
    {
        var data = new byte[25];
        data[0] = 0x41;
        SHA256.HashData(BitConverter.GetBytes(seed)).AsSpan(0, 20).CopyTo(data.AsSpan(1));
        SHA256.HashData(SHA256.HashData(data.AsSpan(0, 21))).AsSpan(0, 4).CopyTo(data.AsSpan(21));
        var value = new System.Numerics.BigInteger(data, isUnsigned: true, isBigEndian: true);
        var sb = new System.Text.StringBuilder();
        while (value > 0)
        {
            sb.Insert(0, Alphabet[(int)(value % 58)]);
            value /= 58;
        }
        return "USDT:" + sb;
    }

    private static string Encode(byte[] data)
    {
        var sb = new System.Text.StringBuilder();
        for (var offset = 0; offset < data.Length; offset += 8)
        {
            var block = data.AsSpan(offset, Math.Min(8, data.Length - offset));
            UInt128 value = 0;
            foreach (var b in block)
                value = (value << 8) | b;
            var chars = new char[EncodedBlockSizes[block.Length]];
            for (var i = chars.Length - 1; i >= 0; i--)
            {
                chars[i] = Alphabet[(int)(value % 58)];
                value /= 58;
            }
            sb.Append(chars);
        }
        return sb.ToString();
    }
}

public sealed class ManualTime(DateTimeOffset start, TimeZoneInfo? localTimeZone = null) : TimeProvider
{
    private DateTimeOffset _now = start;
    private long _ticks;

    public override TimeZoneInfo LocalTimeZone => localTimeZone ?? base.LocalTimeZone;

    public override DateTimeOffset GetUtcNow() => _now;

    public override long GetTimestamp() => _ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public void Advance(TimeSpan by)
    {
        _now += by;
        _ticks += by.Ticks;
    }
}

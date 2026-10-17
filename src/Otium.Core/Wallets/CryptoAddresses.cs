using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Otium.Core.Accounts;

namespace Otium.Core.Wallets;

/// <summary>
/// Checks payout addresses before any money moves, so a typo is caught in the app and refused by the server instead of
/// being sent somewhere nobody controls. Mainnet only: Bitcoin (bech32 SegWit v0, bech32m Taproot, legacy 1… and 3…),
/// Ethereum and BNB Smart Chain (0x…, with the EIP-55 checksum when mixed case) and Solana (base58 public keys).
/// </summary>
public static class CryptoAddresses
{
    public static bool TryValidate(string method, string? address, out string? error)
    {
        var value = (address ?? "").Trim();
        error = null;
        if (value.Length == 0)
        {
            error = "Enter your address.";
            return false;
        }

        var ok = method switch
        {
            CashoutMethods.Bitcoin => IsBitcoin(value),
            CashoutMethods.Ethereum or CashoutMethods.Bnb => IsEvm(value, out error),
            CashoutMethods.Solana => IsSolana(value),
            CashoutMethods.Monero => MoneroAddress.TryValidate(value, out _, out error),
            _ => false,
        };
        if (!ok && error is null)
            error = $"That isn't a valid {CashoutMethods.Name(method)} address. Copy it from your wallet's Receive screen ({CashoutMethods.Network(method)} network).";
        return ok;
    }

    // ---------- Bitcoin ----------

    private static bool IsBitcoin(string value) =>
        value.StartsWith("bc1", StringComparison.OrdinalIgnoreCase) ? IsSegwit(value) : IsBase58Check(value);

    private const string Bech32Charset = "qpzry9x8gf2tvdw0s3jn54khce6mua7l";
    private const uint Bech32Constant = 1;
    private const uint Bech32mConstant = 0x2bc830a3;

    /// <summary>BIP-173/BIP-350: version 0 programs of 20 or 32 bytes (bech32), version 1 of 32 bytes (Taproot, bech32m).</summary>
    private static bool IsSegwit(string value)
    {
        if (value.Length > 90 || (value != value.ToLowerInvariant() && value != value.ToUpperInvariant()))
            return false;
        var lower = value.ToLowerInvariant();
        var separator = lower.LastIndexOf('1');
        if (separator < 1 || lower[..separator] != "bc" || lower.Length - separator - 1 < 7)
            return false;

        var data = new byte[lower.Length - separator - 1];
        for (var i = 0; i < data.Length; i++)
        {
            var index = Bech32Charset.IndexOf(lower[separator + 1 + i]);
            if (index < 0)
                return false;
            data[i] = (byte)index;
        }

        var check = Polymod([.. HrpExpand("bc"), .. data]);
        var version = data[0];
        var program = ConvertBits(data.AsSpan(1, data.Length - 7), 5, 8);
        if (program is null || program.Length is < 2 or > 40)
            return false;
        return version switch
        {
            0 => check == Bech32Constant && program.Length is 20 or 32,
            1 => check == Bech32mConstant && program.Length == 32,
            _ => false, // future versions: valid, but not something to pay to yet
        };
    }

    private static byte[] HrpExpand(string hrp) =>
        [.. hrp.Select(c => (byte)(c >> 5)), 0, .. hrp.Select(c => (byte)(c & 31))];

    private static uint Polymod(ReadOnlySpan<byte> values)
    {
        ReadOnlySpan<uint> generator = [0x3b6a57b2, 0x26508e6d, 0x1ea119fa, 0x3d4233dd, 0x2a1462b3];
        uint chk = 1;
        foreach (var v in values)
        {
            var top = chk >> 25;
            chk = ((chk & 0x1ffffff) << 5) ^ v;
            for (var i = 0; i < 5; i++)
            {
                if (((top >> i) & 1) != 0)
                    chk ^= generator[i];
            }
        }
        return chk;
    }

    private static byte[]? ConvertBits(ReadOnlySpan<byte> data, int from, int to)
    {
        var acc = 0;
        var bits = 0;
        var maxValue = (1 << to) - 1;
        var result = new List<byte>();
        foreach (var value in data)
        {
            acc = (acc << from) | value;
            bits += from;
            while (bits >= to)
            {
                bits -= to;
                result.Add((byte)((acc >> bits) & maxValue));
            }
        }
        // No padding allowed when decoding: leftover bits must be fewer than `from` and all zero.
        if (bits >= from || ((acc << (to - bits)) & maxValue) != 0)
            return null;
        return [.. result];
    }

    /// <summary>Legacy P2PKH (1…) and P2SH (3…) addresses.</summary>
    private static bool IsBase58Check(string value)
    {
        var bytes = DecodeBase58(value);
        if (bytes is not { Length: 25 } || bytes[0] is not (0x00 or 0x05))
            return false;
        var hash = SHA256.HashData(SHA256.HashData(bytes.AsSpan(0, 21)));
        return hash.AsSpan(0, 4).SequenceEqual(bytes.AsSpan(21, 4));
    }

    // ---------- Ethereum and BNB Smart Chain ----------

    private static bool IsEvm(string value, out string? error)
    {
        error = null;
        if (value.Length != 42 || !value.StartsWith("0x", StringComparison.Ordinal) || !value[2..].All(char.IsAsciiHexDigit))
            return false;

        var hex = value[2..];
        if (hex == hex.ToLowerInvariant() || hex == hex.ToUpperInvariant())
            return true; // no checksum to verify

        // EIP-55: a letter is upper case exactly when the matching nibble of keccak256(lowercase hex) is 8 or more.
        var hash = Keccak256.Hash(Encoding.ASCII.GetBytes(hex.ToLowerInvariant()));
        for (var i = 0; i < 40; i++)
        {
            if (!char.IsAsciiLetter(hex[i]))
                continue;
            var nibble = (hash[i / 2] >> (i % 2 == 0 ? 4 : 0)) & 0xF;
            if (char.IsUpper(hex[i]) != nibble >= 8)
            {
                error = "That address has a typo (its capital letters don't match its checksum). Copy and paste it from your wallet.";
                return false;
            }
        }
        return true;
    }

    // ---------- Solana ----------

    private static bool IsSolana(string value) => value.Length is >= 32 and <= 44 && DecodeBase58(value) is { Length: 32 };

    // ---------- shared ----------

    private const string Base58Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    internal static byte[]? DecodeBase58(string value)
    {
        BigInteger number = 0;
        foreach (var c in value)
        {
            var digit = Base58Alphabet.IndexOf(c);
            if (digit < 0)
                return null;
            number = number * 58 + digit;
        }
        var body = number.IsZero ? [] : number.ToByteArray(isUnsigned: true, isBigEndian: true);
        var leadingZeros = value.TakeWhile(c => c == '1').Count();
        return [.. new byte[leadingZeros], .. body];
    }
}

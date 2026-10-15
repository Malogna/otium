namespace Otium.Core.Wallets;

public enum MoneroAddressKind
{
    Standard,
    Subaddress,
    Integrated,
}

/// <summary>
/// Validates mainnet Monero addresses, including the Keccak checksum, so a mistyped payout address
/// is caught before any hashes are mined for it.
/// </summary>
public static class MoneroAddress
{
    private const string Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
    private const int FullBlockChars = 11;
    private const int FullBlockBytes = 8;

    // Encoded length (chars) for a block of N bytes, N = 0..8.
    private static readonly int[] EncodedBlockSizes = [0, 2, 3, 5, 6, 7, 9, 10, 11];

    private const byte StandardTag = 18;
    private const byte IntegratedTag = 19;
    private const byte SubaddressTag = 42;

    public static bool TryValidate(string? address, out MoneroAddressKind kind, out string? error)
    {
        kind = default;
        address = address?.Trim();

        if (string.IsNullOrEmpty(address))
        {
            error = "Enter your Monero (XMR) wallet address.";
            return false;
        }

        if (address.Length is not (95 or 106))
        {
            error = $"A Monero address is 95 characters long (yours has {address.Length}). Copy it again from your wallet.";
            return false;
        }

        var bytes = DecodeBase58(address);
        if (bytes is null)
        {
            error = "That address contains characters that can't appear in a Monero address.";
            return false;
        }

        var payload = bytes.AsSpan(0, bytes.Length - 4);
        var checksum = bytes.AsSpan(bytes.Length - 4);
        if (!Keccak256.Hash(payload).AsSpan(0, 4).SequenceEqual(checksum))
        {
            error = "That address has a typo (its checksum doesn't match). Copy it again from your wallet.";
            return false;
        }

        (kind, var expectedLength) = bytes[0] switch
        {
            StandardTag => (MoneroAddressKind.Standard, 69),
            SubaddressTag => (MoneroAddressKind.Subaddress, 69),
            IntegratedTag => (MoneroAddressKind.Integrated, 77),
            _ => (default(MoneroAddressKind), -1),
        };

        if (expectedLength != bytes.Length)
        {
            error = "That isn't a main-network Monero address. Check you copied the right one.";
            return false;
        }

        error = null;
        return true;
    }

    private static byte[]? DecodeBase58(string encoded)
    {
        var fullBlocks = encoded.Length / FullBlockChars;
        var lastChars = encoded.Length % FullBlockChars;
        var lastBytes = Array.IndexOf(EncodedBlockSizes, lastChars);
        if (lastBytes < 0)
            return null;

        var result = new byte[fullBlocks * FullBlockBytes + lastBytes];
        for (var block = 0; block < fullBlocks; block++)
        {
            if (!DecodeBlock(encoded.AsSpan(block * FullBlockChars, FullBlockChars), result.AsSpan(block * FullBlockBytes, FullBlockBytes)))
                return null;
        }

        if (lastChars > 0 &&
            !DecodeBlock(encoded.AsSpan(fullBlocks * FullBlockChars, lastChars), result.AsSpan(fullBlocks * FullBlockBytes, lastBytes)))
            return null;

        return result;
    }

    private static bool DecodeBlock(ReadOnlySpan<char> chars, Span<byte> output)
    {
        UInt128 value = 0;
        foreach (var ch in chars)
        {
            var digit = Alphabet.IndexOf(ch);
            if (digit < 0)
                return false;
            value = value * 58 + (uint)digit;
        }

        // The decoded number must fit in the block's byte count.
        if (output.Length < 16 && value >> (output.Length * 8) != 0)
            return false;

        for (var i = output.Length - 1; i >= 0; i--)
        {
            output[i] = (byte)(value & 0xFF);
            value >>= 8;
        }
        return true;
    }
}

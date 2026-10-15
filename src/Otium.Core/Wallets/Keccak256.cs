using System.Buffers.Binary;

namespace Otium.Core.Wallets;

/// <summary>
/// Original Keccak-256 (pre-NIST padding), as used by Monero for address checksums.
/// .NET's SHA3_256 uses the final FIPS-202 padding and produces different digests, so it cannot be used here.
/// </summary>
internal static class Keccak256
{
    private const int Rate = 136;

    private static readonly ulong[] RoundConstants =
    [
        0x0000000000000001UL, 0x0000000000008082UL, 0x800000000000808aUL, 0x8000000080008000UL,
        0x000000000000808bUL, 0x0000000080000001UL, 0x8000000080008081UL, 0x8000000000008009UL,
        0x000000000000008aUL, 0x0000000000000088UL, 0x0000000080008009UL, 0x000000008000000aUL,
        0x000000008000808bUL, 0x800000000000008bUL, 0x8000000000008089UL, 0x8000000000008003UL,
        0x8000000000008002UL, 0x8000000000000080UL, 0x000000000000800aUL, 0x800000008000000aUL,
        0x8000000080008081UL, 0x8000000000008080UL, 0x0000000080000001UL, 0x8000000080008008UL,
    ];

    private static readonly int[] RotationOffsets =
    [
        1, 3, 6, 10, 15, 21, 28, 36, 45, 55, 2, 14,
        27, 41, 56, 8, 25, 43, 62, 18, 39, 61, 20, 44,
    ];

    private static readonly int[] PiLane =
    [
        10, 7, 11, 17, 18, 3, 5, 16, 8, 21, 24, 4,
        15, 23, 19, 13, 12, 2, 20, 14, 22, 9, 6, 1,
    ];

    public static byte[] Hash(ReadOnlySpan<byte> data)
    {
        Span<ulong> state = stackalloc ulong[25];
        state.Clear();

        while (data.Length >= Rate)
        {
            Absorb(state, data[..Rate]);
            data = data[Rate..];
        }

        Span<byte> last = stackalloc byte[Rate];
        last.Clear();
        data.CopyTo(last);
        last[data.Length] ^= 0x01;
        last[Rate - 1] ^= 0x80;
        Absorb(state, last);

        var output = new byte[32];
        for (var i = 0; i < 4; i++)
            BinaryPrimitives.WriteUInt64LittleEndian(output.AsSpan(i * 8), state[i]);
        return output;
    }

    private static void Absorb(Span<ulong> state, ReadOnlySpan<byte> block)
    {
        for (var i = 0; i < Rate / 8; i++)
            state[i] ^= BinaryPrimitives.ReadUInt64LittleEndian(block[(i * 8)..]);
        Permute(state);
    }

    private static void Permute(Span<ulong> a)
    {
        Span<ulong> c = stackalloc ulong[5];
        for (var round = 0; round < 24; round++)
        {
            // Theta
            for (var x = 0; x < 5; x++)
                c[x] = a[x] ^ a[x + 5] ^ a[x + 10] ^ a[x + 15] ^ a[x + 20];
            for (var x = 0; x < 5; x++)
            {
                var d = c[(x + 4) % 5] ^ ulong.RotateLeft(c[(x + 1) % 5], 1);
                for (var y = 0; y < 25; y += 5)
                    a[y + x] ^= d;
            }

            // Rho and Pi
            var current = a[1];
            for (var i = 0; i < 24; i++)
            {
                var j = PiLane[i];
                var temp = a[j];
                a[j] = ulong.RotateLeft(current, RotationOffsets[i]);
                current = temp;
            }

            // Chi
            for (var y = 0; y < 25; y += 5)
            {
                for (var x = 0; x < 5; x++)
                    c[x] = a[y + x];
                for (var x = 0; x < 5; x++)
                    a[y + x] = c[x] ^ (~c[(x + 1) % 5] & c[(x + 2) % 5]);
            }

            // Iota
            a[0] ^= RoundConstants[round];
        }
    }
}

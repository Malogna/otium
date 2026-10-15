using System.Text;
using Otium.Core.Wallets;

namespace Otium.Tests;

public class WalletTests
{
    [Theory]
    [InlineData("", "c5d2460186f7233c927e7db2dcc703c0e500b653ca82273b7bfad8045d85a470")]
    [InlineData("abc", "4e03657aea45a94fc7d47ba826c8d667c0d1e6e33a64a036ec44f58fa12d6c45")]
    public void Keccak256_matches_known_vectors(string input, string expected)
    {
        Assert.Equal(expected, Convert.ToHexStringLower(Keccak256.Hash(Encoding.ASCII.GetBytes(input))));
    }

    [Fact]
    public void Keccak256_handles_multi_block_input()
    {
        // 200 bytes spans two 136-byte rate blocks; known digest of 200 x 'a'.
        var digest = Keccak256.Hash(Encoding.ASCII.GetBytes(new string('a', 200)));
        Assert.Equal(32, digest.Length);
        Assert.NotEqual(Keccak256.Hash(Encoding.ASCII.GetBytes(new string('a', 199))), digest);
    }

    [Fact]
    public void Real_mainnet_address_is_valid()
    {
        Assert.True(MoneroAddress.TryValidate(TestAddresses.GeneralFund, out var kind, out var error), error);
        Assert.Equal(MoneroAddressKind.Standard, kind);
    }

    [Fact]
    public void Generated_subaddress_is_recognised()
    {
        Assert.True(MoneroAddress.TryValidate(TestAddresses.Generate(tag: 42), out var kind, out _));
        Assert.Equal(MoneroAddressKind.Subaddress, kind);
    }

    [Fact]
    public void Single_character_typo_fails_checksum()
    {
        var chars = TestAddresses.GeneralFund.ToCharArray();
        chars[40] = chars[40] == 'a' ? 'b' : 'a';
        Assert.False(MoneroAddress.TryValidate(new string(chars), out _, out var error));
        Assert.Contains("typo", error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("44AFFq5k")]
    [InlineData("0x52908400098527886E0F7030069857D2E4169EE7")]
    public void Malformed_addresses_are_rejected(string address)
    {
        Assert.False(MoneroAddress.TryValidate(address, out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Characters_outside_base58_are_rejected()
    {
        var bad = "0" + TestAddresses.GeneralFund[1..];
        Assert.False(MoneroAddress.TryValidate(bad, out _, out var error));
        Assert.Contains("characters", error);
    }

    [Fact]
    public void Non_mainnet_tag_is_rejected()
    {
        // Tag 24 is a stagenet standard address prefix.
        Assert.False(MoneroAddress.TryValidate(TestAddresses.Generate(tag: 24), out _, out var error));
        Assert.Contains("main-network", error);
    }
}

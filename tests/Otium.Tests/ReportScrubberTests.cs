using Otium.Core.Support;

namespace Otium.Tests;

/// <summary>Problem reports leave the computer without payout addresses or keys.</summary>
public sealed class ReportScrubberTests
{
    [Fact]
    public void Known_values_are_removed_in_any_case()
    {
        var text = ReportScrubber.Scrub("payout USDT:TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t, secret abcdefgh12", ["USDT:TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t", "ABCDEFGH12"]);
        Assert.Equal("payout [removed], secret [removed]", text);
    }

    [Theory]
    [InlineData("0x5aAeb6053F3E94C9b9A09f33669435E7Ef1BeAed")]
    [InlineData("TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t")]
    [InlineData("bc1qar0srrr7xfkvy5l643lydnw9re59gtzzwf5mdq")]
    [InlineData("ltc1qg82ve6v7p8wdp2qgrkmg6w8d3vfmu3qxw7r3vl")]
    [InlineData("44AFFq5kSiGBoZ4NMDwYtN18obc8AemS33DBLWs3H7otXft3XjrpDtQGv7SqSsaBYBb98uNbr2VBBEt7f2wfn3RVGQBEP3A")]
    public void Unknown_addresses_are_replaced(string address)
    {
        var text = ReportScrubber.Scrub($"[xmrig] login {address}.ot12ab accepted", []);
        Assert.DoesNotContain(address, text);
        Assert.Contains("[address]", text);
    }

    [Theory]
    [InlineData("BC1QAR0SRRR7XFKVY5L643LYDNW9RE59GTZZWF5MDQ")] // bech32 is valid in capitals too (QR codes use them)
    [InlineData("LTC1QG82VE6V7P8WDP2QGRKMG6W8D3VFMU3QXW7R3VL")]
    public void Upper_case_addresses_are_replaced(string address)
    {
        var text = ReportScrubber.Scrub($"pay to {address} please", []);
        Assert.DoesNotContain(address, text);
        Assert.Contains("[address]", text);
    }

    [Fact]
    public void Emails_and_the_users_name_in_file_paths_are_removed()
    {
        var text = ReportScrubber.Scrub(
            "Antivirus: Norton; add an exception for C:\\Users\\Kari Nordmann\\AppData\\Local\\Otium\\engines\n"
            + "IO error: Access to the path 'c:\\users\\kari\\AppData\\Local\\Otium\\settings.json' is denied.\n"
            + "mac: /Users/kari/Library/Application Support/Otium; linux: /home/kari/.local/share/Otium\n"
            + "Signed in as kari.nordmann+otium@example.no, PayPal pay@firma.example.com",
            []);
        Assert.DoesNotContain("Kari", text);
        Assert.DoesNotContain("kari", text);
        Assert.DoesNotContain("Nordmann", text); // Windows account names can have spaces
        Assert.DoesNotContain("@", text);
        Assert.Contains("C:\\Users\\[user]\\AppData\\Local\\Otium\\engines", text);
        Assert.Contains("/Users/[user]/Library", text);
        Assert.Contains("/home/[user]/.local", text);
        Assert.Contains("Signed in as [email], PayPal [email]", text);
    }

    [Fact]
    public void Long_hex_keys_are_replaced_and_ordinary_log_lines_kept()
    {
        var key = new string('a', 20) + "0123456789abcdef0123";
        var text = ReportScrubber.Scrub($"token {key}\nspeed 10s/60s/15m 4120.3 n/a n/a H/s max 4200.1 H/s", []);
        Assert.Contains("token [key]", text);
        Assert.Contains("speed 10s/60s/15m 4120.3 n/a n/a H/s max 4200.1 H/s", text);
    }
}

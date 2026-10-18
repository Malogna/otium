using System.Text.RegularExpressions;

namespace Otium.Core.Support;

/// <summary>
/// Takes payout addresses and secrets out of a problem report before it leaves the computer. Known values (the
/// user's addresses, the device secret) are replaced exactly; anything else shaped like a crypto address or a long key
/// is replaced too, which may hide a harmless long word but never sends an address.
/// </summary>
public static partial class ReportScrubber
{
    public static string Scrub(string text, IEnumerable<string?> known)
    {
        foreach (var value in known.Where(v => !string.IsNullOrWhiteSpace(v) && v!.Length >= 8).Select(v => v!.Trim()).Distinct().OrderByDescending(v => v.Length))
            text = text.Replace(value, "[removed]", StringComparison.OrdinalIgnoreCase);
        text = HexKey().Replace(text, "[key]");
        return Address().Replace(text, "[address]");
    }

    /// <summary>EVM, TRON, bech32 (BTC/LTC), base58 (BTC/LTC legacy, Solana) and Monero addresses.</summary>
    [GeneratedRegex(@"\b(?:0x[0-9a-fA-F]{40}|(?:bc1|ltc1|tb1)[0-9a-z]{20,90}|[48][1-9A-HJ-NP-Za-km-z]{94,105}|[1-9A-HJ-NP-Za-km-z]{25,60})\b")]
    private static partial Regex Address();

    /// <summary>Long hex strings: access tokens, secrets, hashes.</summary>
    [GeneratedRegex(@"\b[0-9a-fA-F]{32,}\b")]
    private static partial Regex HexKey();
}

using System.Text.RegularExpressions;

namespace Otium.Core.Support;

/// <summary>
/// Takes payout addresses, secrets and personal details out of a problem report before it leaves the computer. Known
/// values (the user's addresses and email, the device secret) are replaced exactly; anything else shaped like a crypto
/// address, a long key or an email address is replaced too, and the user's name in file paths ("C:\Users\name\…")
/// becomes "[user]". That may hide a harmless long word, but never sends an address.
/// </summary>
public static partial class ReportScrubber
{
    public static string Scrub(string text, IEnumerable<string?> known)
    {
        foreach (var value in known.Where(v => !string.IsNullOrWhiteSpace(v) && v!.Length >= 8).Select(v => v!.Trim()).Distinct().OrderByDescending(v => v.Length))
            text = text.Replace(value, "[removed]", StringComparison.OrdinalIgnoreCase);
        text = Email().Replace(text, "[email]");
        text = UserFolder().Replace(text, "${root}[user]");
        text = HexKey().Replace(text, "[key]");
        return Address().Replace(text, "[address]");
    }

    /// <summary>EVM, TRON, bech32 (BTC/LTC, either case), base58 (BTC/LTC legacy, Solana) and Monero addresses.</summary>
    [GeneratedRegex(@"\b(?:0x[0-9a-fA-F]{40}|(?i:bc1|ltc1|tb1)(?:[0-9a-z]{20,90}|[0-9A-Z]{20,90})|[48][1-9A-HJ-NP-Za-km-z]{94,105}|[1-9A-HJ-NP-Za-km-z]{25,60})\b")]
    private static partial Regex Address();

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}")]
    private static partial Regex Email();

    /// <summary>
    /// The account name in a home folder path: Windows "C:\Users\name" (up to the next backslash, since Windows names
    /// can have spaces), macOS "/Users/name", Linux "/home/name".
    /// </summary>
    [GeneratedRegex(@"(?<root>(?i:[A-Z]:\\Users\\))(?!\[user\])(?:[^\\/:*?""<>|\r\n]+(?=\\)|[^\\/\s""':;]+)|(?<root>/Users/|/home/)(?!\[user\])[^\\/\s""':;]+")]
    private static partial Regex UserFolder();

    /// <summary>Long hex strings: access tokens, secrets, hashes.</summary>
    [GeneratedRegex(@"\b[0-9a-fA-F]{32,}\b")]
    private static partial Regex HexKey();
}

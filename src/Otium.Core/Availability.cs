using System.Globalization;

namespace Otium.Core;

/// <summary>
/// Countries where Otium doesn't run: comprehensive sanctions (EU, Norway, US) and countries that ban crypto mining.
/// The app checks the Windows region, and the server the visitor's country from Cloudflare, so neither alone decides.
/// Review the list yearly.
/// </summary>
public static class Availability
{
    private const string Sanctions = "sanctions";
    private const string MiningBan = "mining ban";

    private static readonly Dictionary<string, string> Blocked = new(StringComparer.OrdinalIgnoreCase)
    {
        // Comprehensive sanctions, and the EU/Norway ban on crypto-asset services to Russia and Belarus.
        ["CU"] = Sanctions, ["IR"] = Sanctions, ["KP"] = Sanctions, ["SY"] = Sanctions, ["RU"] = Sanctions, ["BY"] = Sanctions,
        // Crypto mining is banned or a crime.
        ["CN"] = MiningBan, ["DZ"] = MiningBan, ["NP"] = MiningBan, ["EG"] = MiningBan, ["BD"] = MiningBan, ["IQ"] = MiningBan,
        ["MA"] = MiningBan, ["QA"] = MiningBan, ["TN"] = MiningBan, ["AO"] = MiningBan, ["KW"] = MiningBan,
    };

    /// <summary>Two-letter codes of every country Otium doesn't run in.</summary>
    public static IReadOnlyCollection<string> BlockedCountries => Blocked.Keys;

    /// <summary>Why Otium can't run in this country (ISO 3166 two-letter code), or null when it can.</summary>
    public static string? BlockedReason(string? countryCode)
    {
        var code = countryCode?.Trim();
        if (code is not { Length: 2 } || !Blocked.TryGetValue(code, out var why))
            return null;
        var name = NameOf(code);
        return why == Sanctions
            ? $"Otium isn't available in {name}, because of international sanctions."
            : $"Otium isn't available in {name}, because crypto mining isn't allowed there.";
    }

    /// <summary>Why Otium can't run in this computer's Windows region, or null when it can.</summary>
    public static string? BlockedHere()
    {
        try
        {
            return BlockedReason(RegionInfo.CurrentRegion.TwoLetterISORegionName);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string NameOf(string code)
    {
        try
        {
            return new RegionInfo(code).EnglishName;
        }
        catch (ArgumentException)
        {
            return "your country";
        }
    }
}

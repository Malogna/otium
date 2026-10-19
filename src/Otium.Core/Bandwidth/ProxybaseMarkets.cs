using System.Globalization;

namespace Otium.Core.Bandwidth;

/// <summary>What Proxybase pays sellers in a country, and whether the country is one of its listed markets.</summary>
public sealed record ProxybaseMarket(string CountryName, double SellerUsdPerGb, bool Listed);

/// <summary>
/// Proxybase's seller rates for home (residential) connections, from its markets page (proxybase.xyz/markets,
/// October 2026; sellers get 60% of the buyer price). The live table behind it (api.proxybase.xyz/v2/catalog/pricing)
/// doesn't answer without an account, so the rates are built in. Used only to estimate before a computer has its own
/// earnings history, and by the website for its visitors' country.
/// </summary>
public static class ProxybaseMarkets
{
    public const string Source = "proxybase.xyz/markets, October 2026";

    /// <summary>The low end of Proxybase's "a residential node transfers 1–5 GB/day" on average.</summary>
    public const double EstimateGbPerDay = 1;

    /// <summary>Seller yield per GB for residential connections, by country.</summary>
    private static readonly Dictionary<string, double> ResidentialUsdPerGb = new(StringComparer.OrdinalIgnoreCase)
    {
        ["US"] = 1.80, ["CA"] = 1.80, ["MX"] = 1.50, ["BR"] = 1.50, ["AR"] = 1.50, ["CL"] = 1.50, ["CO"] = 1.50,
        ["GB"] = 1.92, ["DE"] = 2.10, ["FR"] = 1.98, ["IT"] = 1.98, ["ES"] = 1.98, ["NL"] = 1.80, ["CH"] = 2.10,
        ["AT"] = 2.10, ["BE"] = 2.10, ["IE"] = 1.80, ["PT"] = 1.80, ["SE"] = 2.10, ["DK"] = 2.10, ["NO"] = 2.10,
        ["FI"] = 2.10, ["PL"] = 1.50, ["CZ"] = 1.50, ["HU"] = 1.50, ["RO"] = 1.50, ["GR"] = 1.50, ["TR"] = 1.50,
        ["JP"] = 2.40, ["KR"] = 2.40, ["SG"] = 2.40, ["HK"] = 2.40, ["AU"] = 1.80, ["NZ"] = 1.80, ["IN"] = 1.20,
        ["TH"] = 1.20, ["VN"] = 1.20, ["PH"] = 1.20, ["MY"] = 1.20, ["ID"] = 1.20, ["AE"] = 1.20, ["IL"] = 1.20,
        ["ZA"] = 1.50,
    };

    /// <summary>Proxybase's "WorldWide" residential rate, for countries it doesn't list on their own.</summary>
    public const double WorldWideUsdPerGb = 1.50;

    /// <summary>The seller rate where this computer is; countries Proxybase doesn't list get its worldwide rate.</summary>
    public static ProxybaseMarket For(string? countryCode)
    {
        var code = countryCode?.Trim() ?? "";
        var name = NameOf(code);
        return ResidentialUsdPerGb.TryGetValue(code, out var rate)
            ? new ProxybaseMarket(name, rate, Listed: true)
            : new ProxybaseMarket(name, WorldWideUsdPerGb, Listed: false);
    }

    /// <summary>The estimate for a day: the market rate for <see cref="EstimateGbPerDay"/>, less Otium's share.</summary>
    public static double EstimatedUserUsdPerDay(ProxybaseMarket market, double otiumFeeShare) =>
        market.SellerUsdPerGb * EstimateGbPerDay * (1 - otiumFeeShare);

    private static string NameOf(string code)
    {
        try
        {
            return code.Length == 2 ? new RegionInfo(code).EnglishName : "your country";
        }
        catch (ArgumentException)
        {
            return "your country";
        }
    }
}

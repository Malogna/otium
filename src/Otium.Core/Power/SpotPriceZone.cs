using System.Globalization;

namespace Otium.Core.Power;

/// <summary>
/// A day-ahead electricity bidding zone we can fetch live hourly prices for, and the open API that serves them.
/// Norway and Sweden publish the same free JSON format (no key); more countries can be added here later.
/// </summary>
public sealed record SpotPriceZone(string Code, string Country, string Label)
{
    /// <summary>The day's hourly prices, e.g. https://www.hvakosterstrommen.no/api/v1/prices/2026/10-16_NO1.json</summary>
    public Uri UrlFor(DateOnly day)
    {
        var host = Country == "NO" ? "https://www.hvakosterstrommen.no" : "https://www.elprisetjustnu.se";
        var date = day.ToString("yyyy/MM-dd", CultureInfo.InvariantCulture);
        return new Uri($"{host}/api/v1/prices/{date}_{Code}.json");
    }

    /// <summary>Zones whose live prices we can fetch, in display order.</summary>
    public static readonly IReadOnlyList<SpotPriceZone> All =
    [
        new("NO1", "NO", "Norway · Oslo / east (NO1)"),
        new("NO2", "NO", "Norway · Kristiansand / south (NO2)"),
        new("NO3", "NO", "Norway · Trondheim / mid (NO3)"),
        new("NO4", "NO", "Norway · Tromsø / north (NO4)"),
        new("NO5", "NO", "Norway · Bergen / west (NO5)"),
        new("SE1", "SE", "Sweden · Luleå (SE1)"),
        new("SE2", "SE", "Sweden · Sundsvall (SE2)"),
        new("SE3", "SE", "Sweden · Stockholm (SE3)"),
        new("SE4", "SE", "Sweden · Malmö (SE4)"),
    ];

    /// <summary>The most populous zone for a country, used as the default until the user picks their own.</summary>
    private static readonly Dictionary<string, string> DefaultByCountry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NO"] = "NO1",
        ["SE"] = "SE3",
    };

    public static SpotPriceZone? Find(string? code) =>
        code is null ? null : All.FirstOrDefault(z => string.Equals(z.Code, code, StringComparison.OrdinalIgnoreCase));

    /// <summary>Live-priced zones in a country, or empty when we have no source there.</summary>
    public static IReadOnlyList<SpotPriceZone> ForCountry(string isoCountry) =>
        All.Where(z => string.Equals(z.Country, isoCountry, StringComparison.OrdinalIgnoreCase)).ToList();

    /// <summary>The default zone for a country, or null when live prices aren't available there.</summary>
    public static SpotPriceZone? DefaultFor(string isoCountry) =>
        DefaultByCountry.TryGetValue(isoCountry, out var code) ? Find(code) : null;

    public static SpotPriceZone? DefaultForCurrentRegion()
    {
        try
        {
            return DefaultFor(RegionInfo.CurrentRegion.TwoLetterISORegionName);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}

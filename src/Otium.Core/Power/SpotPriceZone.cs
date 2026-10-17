using System.Globalization;

namespace Otium.Core.Power;

/// <summary>How a zone's price API shapes its response.</summary>
internal enum SpotPriceFormat
{
    /// <summary>A JSON array of <c>{ "EUR_per_kWh", "time_start" }</c> (hvakosterstrommen.no and its Swedish and Danish twins).</summary>
    NordicDaily,

    /// <summary><c>{ "prices": [{ "date", "value" }] }</c> with values in EUR/MWh (sahkotin.fi).</summary>
    Sahkotin,
}

/// <summary>
/// A day-ahead electricity bidding zone we can fetch live hourly prices for, and the open API that serves them.
/// Norway, Sweden and Denmark publish the same free JSON format; Finland uses sahkotin.fi. None needs a key.
/// </summary>
public sealed record SpotPriceZone(string Code, string Country, string Label)
{
    private static readonly TimeZoneInfo Helsinki = FindTimeZone("Europe/Helsinki", "FLE Standard Time");

    internal SpotPriceFormat Format => Country == "FI" ? SpotPriceFormat.Sahkotin : SpotPriceFormat.NordicDaily;

    /// <summary>The day's hourly prices, e.g. https://www.hvakosterstrommen.no/api/v1/prices/2026/10-16_NO1.json</summary>
    public Uri UrlFor(DateOnly day)
    {
        if (Format == SpotPriceFormat.Sahkotin)
        {
            // sahkotin.fi takes a UTC range, so ask for exactly the Finnish calendar day (23 or 25 hours around DST).
            var start = LocalMidnightUtc(day, Helsinki);
            var end = LocalMidnightUtc(day.AddDays(1), Helsinki);
            return new Uri(string.Create(CultureInfo.InvariantCulture,
                $"https://sahkotin.fi/prices?start={start:yyyy-MM-ddTHH:mm:ss.fffZ}&end={end:yyyy-MM-ddTHH:mm:ss.fffZ}"));
        }

        var host = Country switch
        {
            "NO" => "https://www.hvakosterstrommen.no",
            "DK" => "https://www.elprisenligenu.dk",
            _ => "https://www.elprisetjustnu.se",
        };
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
        new("DK1", "DK", "Denmark · Jutland and Funen / west (DK1)"),
        new("DK2", "DK", "Denmark · Zealand and Copenhagen / east (DK2)"),
        new("FI", "FI", "Finland (FI)"),
    ];

    /// <summary>The most populous zone for a country, used as the default until the user picks their own.</summary>
    private static readonly Dictionary<string, string> DefaultByCountry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NO"] = "NO1",
        ["SE"] = "SE3",
        ["DK"] = "DK1",
        ["FI"] = "FI",
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

    private static DateTime LocalMidnightUtc(DateOnly day, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), zone);

    private static TimeZoneInfo FindTimeZone(string iana, string windows)
    {
        foreach (var id in (string[])[iana, windows])
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
            }
        }
        // Finland is UTC+2 with EU summer time; a fixed offset is close enough if the system has no zone data.
        return TimeZoneInfo.CreateCustomTimeZone("Otium/Helsinki", TimeSpan.FromHours(2), "Helsinki", "Helsinki");
    }
}

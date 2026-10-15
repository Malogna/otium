using System.Globalization;

namespace Otium.Core.Power;

/// <summary>
/// Rough household electricity prices (USD per kWh, 2025 averages, rounded) so the app can protect people from
/// mining at a loss without asking them a question they can't answer. Users can enter their real price in
/// Advanced settings. These are deliberately on the high side of each country's typical range.
/// </summary>
public static class ElectricityPrices
{
    public const double Fallback = 0.20;

    private static readonly Dictionary<string, double> ByCountry = new(StringComparer.OrdinalIgnoreCase)
    {
        ["US"] = 0.18, ["CA"] = 0.13, ["MX"] = 0.10, ["BR"] = 0.16, ["AR"] = 0.07, ["CL"] = 0.20, ["CO"] = 0.17,
        ["GB"] = 0.33, ["IE"] = 0.38, ["DE"] = 0.40, ["FR"] = 0.27, ["NL"] = 0.33, ["BE"] = 0.38, ["LU"] = 0.25,
        ["AT"] = 0.33, ["CH"] = 0.33, ["IT"] = 0.36, ["ES"] = 0.25, ["PT"] = 0.24, ["DK"] = 0.38, ["NO"] = 0.15,
        ["SE"] = 0.22, ["FI"] = 0.22, ["IS"] = 0.15, ["PL"] = 0.25, ["CZ"] = 0.33, ["SK"] = 0.22, ["HU"] = 0.12,
        ["RO"] = 0.22, ["BG"] = 0.13, ["GR"] = 0.25, ["HR"] = 0.17, ["SI"] = 0.23, ["EE"] = 0.25, ["LV"] = 0.24,
        ["LT"] = 0.24, ["UA"] = 0.08, ["TR"] = 0.10, ["RU"] = 0.07, ["IL"] = 0.17, ["AE"] = 0.08, ["SA"] = 0.05,
        ["ZA"] = 0.18, ["NG"] = 0.06, ["EG"] = 0.04, ["KE"] = 0.20, ["IN"] = 0.08, ["PK"] = 0.12, ["BD"] = 0.08,
        ["CN"] = 0.08, ["HK"] = 0.20, ["TW"] = 0.10, ["JP"] = 0.26, ["KR"] = 0.12, ["SG"] = 0.25, ["MY"] = 0.06,
        ["TH"] = 0.13, ["VN"] = 0.08, ["PH"] = 0.20, ["ID"] = 0.10, ["AU"] = 0.26, ["NZ"] = 0.23,
    };

    public static double ForCurrentRegion()
    {
        try
        {
            return ForCountry(RegionInfo.CurrentRegion.TwoLetterISORegionName);
        }
        catch (ArgumentException)
        {
            return Fallback;
        }
    }

    public static double ForCountry(string isoCode) => ByCountry.TryGetValue(isoCode, out var price) ? price : Fallback;
}

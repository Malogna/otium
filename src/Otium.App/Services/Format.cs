using System.Globalization;

namespace Otium.App.Services;

public static class Format
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static string Usd(double value)
    {
        var sign = value < 0 ? "-" : "";
        var abs = Math.Abs(value);
        // Most CPU earnings are fractions of a dollar, so show an extra digit below $1.
        return abs < 1 ? $"{sign}${abs.ToString("0.000", Culture)}" : $"{sign}${abs.ToString("#,0.00", Culture)}";
    }

    private static readonly string[] HashUnits = ["H/s", "kH/s", "MH/s", "GH/s", "TH/s", "PH/s", "EH/s"];

    /// <summary>Cents-precision dollars for balances and cash-outs, e.g. "$12.34", independent of the system locale.</summary>
    public static string Money(decimal usd) => $"${usd.ToString("#,0.00", Culture)}";

    public static string Hashrate(double hps)
    {
        var unit = 0;
        while (hps >= 1000 && unit < HashUnits.Length - 1)
        {
            hps /= 1000;
            unit++;
        }
        return unit == 0 ? $"{hps.ToString("0", Culture)} H/s" : $"{hps.ToString("0.00", Culture)} {HashUnits[unit]}";
    }

    public static string Xmr(double value) => $"{value.ToString("0.000000", Culture)} XMR";

    public static string Watts(double watts) => $"{watts.ToString("0", Culture)} W";

    public static string Kwh(double kwh) => $"{kwh.ToString("0.00", Culture)} kWh";

    public static string Date(DateTimeOffset value) => value.ToLocalTime().ToString("MMM d, yyyy", Culture);

    public static string Duration(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours} h {t.Minutes} min" : $"{t.Minutes} min";
}

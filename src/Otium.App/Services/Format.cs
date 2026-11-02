using System.Globalization;
using Otium.Core.Currency;

namespace Otium.App.Services;

public static class Format
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    /// <summary>The currency amounts are shown in (the computer's region, or the one picked in Settings); dollars until known.</summary>
    public static DisplayCurrency Display { get; set; } = DisplayCurrency.Usd;

    /// <summary>An amount of dollars in the currency shown, with an extra digit below one dollar (most earnings are fractions of one).</summary>
    public static string Usd(double value) => Display.Format(value);

    private static readonly string[] HashUnits = ["H/s", "kH/s", "MH/s", "GH/s", "TH/s", "PH/s", "EH/s"];

    /// <summary>Cents-precision amount for balances, e.g. "$12.34" or "11,34 €", in the currency shown.</summary>
    public static string Money(decimal usd) => Display.Format((double)usd, cents: true);

    /// <summary>Dollars exactly as they are, for prices and amounts that are really in dollars ("$0.15/kWh").</summary>
    public static string MoneyUsd(decimal usd) => DisplayCurrency.Usd.Format((double)usd, cents: true);

    /// <summary>"$5.00", or "$5.00 (≈ €4.60)" when another currency is shown: for cash-outs, which are paid out in dollars' worth.</summary>
    public static string MoneyBoth(decimal usd) => Display.FormatWithUsd(usd);

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

    /// <summary>A coin amount with as many decimals as it needs (up to 8), e.g. "0.8412 USDT".</summary>
    public static string Coin(double value, string ticker) => $"{value.ToString("0.########", Culture)} {ticker}";

    public static string Watts(double watts) => $"{watts.ToString("0", Culture)} W";

    public static string Kwh(double kwh) => $"{kwh.ToString("0.00", Culture)} kWh";

    public static string Date(DateTimeOffset value) => value.ToLocalTime().ToString("MMM d, yyyy", Culture);

    public static string Duration(TimeSpan t) =>
        t.TotalHours >= 1 ? $"{(int)t.TotalHours} h {t.Minutes} min" : $"{t.Minutes} min";
}

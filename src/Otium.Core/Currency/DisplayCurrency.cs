using System.Globalization;

namespace Otium.Core.Currency;

/// <summary>
/// The currency amounts are shown in. Everything Otium earns and costs is worked out in US dollars; this only changes
/// how an amount is written on screen (converted at the day's rate, in the number style of the user's own locale).
/// </summary>
public sealed class DisplayCurrency
{
    public const string UsdCode = "USD";

    // Currencies that have no minor unit, or are so small that nobody writes one.
    private static readonly HashSet<string> NoDecimals = new(StringComparer.Ordinal)
    {
        "JPY", "KRW", "VND", "CLP", "ISK", "UGX", "PYG", "XAF", "XOF", "XPF", "RWF", "KMF", "DJF", "GNF", "IDR", "HUF",
    };

    private static readonly Dictionary<string, string> WellKnownSymbols = new(StringComparer.Ordinal)
    {
        ["USD"] = "$", ["EUR"] = "€", ["GBP"] = "£", ["JPY"] = "¥", ["INR"] = "₹", ["KRW"] = "₩", ["ILS"] = "₪",
        ["PHP"] = "₱", ["VND"] = "₫", ["NGN"] = "₦", ["UAH"] = "₴", ["TRY"] = "₺", ["PLN"] = "zł", ["CZK"] = "Kč",
    };

    private readonly CultureInfo _culture;

    /// <param name="code">ISO 4217 code, e.g. "EUR".</param>
    /// <param name="perUsd">How many units of this currency one US dollar buys.</param>
    /// <param name="culture">Sets the digit grouping, decimal mark and symbol position (the user's own locale).</param>
    public DisplayCurrency(string code, double perUsd, CultureInfo culture)
    {
        Code = code.ToUpperInvariant();
        PerUsd = Code == UsdCode ? 1 : perUsd;
        _culture = culture;
        Symbol = SymbolFor(Code, culture);
    }

    /// <summary>US dollars in the invariant style ("$1,234.56"): the fallback, and what tests expect.</summary>
    public static DisplayCurrency Usd { get; } = new(UsdCode, 1, CultureInfo.InvariantCulture);

    public string Code { get; }

    public double PerUsd { get; }

    public string Symbol { get; }

    public bool IsUsd => Code == UsdCode;

    public double FromUsd(double usd) => usd * PerUsd;

    public double ToUsd(double amount) => PerUsd > 0 ? amount / PerUsd : 0;

    /// <summary>
    /// An amount of US dollars written in this currency. Earnings are mostly small, so below one dollar there is one
    /// more digit than usual ("€0.412"); currencies without a minor unit show two digits below 10 ("¥7.30").
    /// </summary>
    /// <param name="cents">Always the usual two digits (balances and cash-outs), never the extra one below a dollar.</param>
    public string Format(double usd, bool cents = false)
    {
        var amount = FromUsd(usd);
        var abs = Math.Abs(amount);
        var digits = NoDecimals.Contains(Code)
            ? abs < 10 ? 2 : 0
            : cents || Math.Abs(usd) >= 1 ? 2 : 3;
        var info = (NumberFormatInfo)_culture.NumberFormat.Clone();
        // Symbol and position come from the currency and the locale; a minus sign goes in front in every style.
        var number = abs.ToString("N" + digits, info);
        // A symbol made of letters ("SEK", "zł") is set apart from the number.
        var gap = Symbol.All(char.IsLetter) ? " " : "";
        var text = info.CurrencyPositivePattern switch
        {
            1 => $"{number}{gap}{Symbol}",
            2 => $"{Symbol} {number}",
            3 => $"{number} {Symbol}",
            _ => $"{Symbol}{gap}{number}",
        };
        return amount < 0 && Math.Round(abs, digits) > 0 ? "-" + text : text;
    }

    /// <summary>"$5.00", and when shown in another currency "$5.00 (≈ €4.60)": for amounts that are really owed in dollars.</summary>
    public string FormatWithUsd(decimal usd)
    {
        var dollars = Usd.Format((double)usd, cents: true);
        return IsUsd ? dollars : $"{dollars} (≈ {Format((double)usd, cents: true)})";
    }

    private static string SymbolFor(string code, CultureInfo culture)
    {
        // The user's own currency is written the way their locale writes it ("kr", "Fr.", "$" for a Canadian).
        try
        {
            if (!culture.Equals(CultureInfo.InvariantCulture) && new RegionInfo(culture.Name).ISOCurrencySymbol == code)
                return culture.NumberFormat.CurrencySymbol;
        }
        catch (ArgumentException)
        {
            // A neutral culture has no region: fall through to the known symbols.
        }
        return WellKnownSymbols.TryGetValue(code, out var symbol) ? symbol : code;
    }

    /// <summary>The currency of the region the computer is set to (Windows' "Region" setting); USD when it can't be told.</summary>
    public static string RegionCode(CultureInfo? culture = null)
    {
        try
        {
            var name = (culture ?? CultureInfo.CurrentCulture).Name;
            if (string.IsNullOrEmpty(name))
                return UsdCode;
            var code = new RegionInfo(name).ISOCurrencySymbol;
            return code.Length == 3 && code.All(char.IsLetter) ? code.ToUpperInvariant() : UsdCode;
        }
        catch (ArgumentException)
        {
            return UsdCode;
        }
    }
}

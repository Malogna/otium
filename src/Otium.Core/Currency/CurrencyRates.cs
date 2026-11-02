using System.Globalization;
using System.Net.Http.Json;

namespace Otium.Core.Currency;

/// <summary>What the server's <c>GET /api/v1/rates</c> returns: units of each currency per one US dollar.</summary>
public sealed record RatesResponse(string Base, DateTimeOffset AsOf, Dictionary<string, double> Rates);

/// <summary>The last rates the app got, kept in a file so amounts still convert offline.</summary>
public sealed class CurrencyRatesCache
{
    /// <summary>The day the rates are for (from the source).</summary>
    public DateTimeOffset AsOf { get; set; }

    /// <summary>When the app last asked the server.</summary>
    public DateTimeOffset FetchedAt { get; set; }

    public Dictionary<string, double> Rates { get; set; } = [];

    public bool Has(string code) => code == DisplayCurrency.UsdCode || Rates.TryGetValue(code, out var r) && r > 0;

    /// <summary>The rates are worth asking for again after this long.</summary>
    public static readonly TimeSpan RefreshAfter = TimeSpan.FromHours(12);

    public bool IsStale(DateTimeOffset now) => Rates.Count == 0 || now - FetchedAt >= RefreshAfter;
}

public sealed class RatesClient(HttpClient http)
{
    public async Task<RatesResponse?> GetAsync(CancellationToken ct)
    {
        using var response = await http.GetAsync("api/v1/rates", ct);
        if (!response.IsSuccessStatusCode)
            return null;
        return await response.Content.ReadFromJsonAsync<RatesResponse>(ct);
    }
}

/// <summary>One entry of the currency list in Settings.</summary>
public sealed record CurrencyOption(string Code, string Name)
{
    /// <summary>"Euro (EUR)"; the automatic entry is just its name.</summary>
    public string Label => Code == CurrencyChoice.Automatic || Code == Name ? Name : $"{Name} ({Code})";

    public override string ToString() => Label;
}

public static class CurrencyChoice
{
    /// <summary>The setting value for "follow the computer's region".</summary>
    public const string Automatic = "auto";

    /// <summary>
    /// The currency to show: the one chosen in Settings, or the region's when it is on "automatic". Falls back to US
    /// dollars when there is no rate for it (no rates yet, or a currency the source doesn't carry).
    /// </summary>
    public static DisplayCurrency Resolve(string? chosen, CultureInfo culture, CurrencyRatesCache? rates)
    {
        var code = string.IsNullOrWhiteSpace(chosen) || chosen == Automatic ? DisplayCurrency.RegionCode(culture) : chosen.ToUpperInvariant();
        if (code == DisplayCurrency.UsdCode || rates is null || !rates.Has(code))
            return new DisplayCurrency(DisplayCurrency.UsdCode, 1, culture);
        return new DisplayCurrency(code, rates.Rates[code], culture);
    }

    /// <summary>The currencies there are rates for, by name, with "automatic" first and then US dollars.</summary>
    public static IReadOnlyList<CurrencyOption> Options(CurrencyRatesCache? rates, CultureInfo? culture = null)
    {
        var names = Names.Value;
        var codes = (rates?.Rates.Keys ?? Enumerable.Empty<string>()).Append(DisplayCurrency.UsdCode).Distinct(StringComparer.Ordinal);
        var region = DisplayCurrency.RegionCode(culture);
        var automatic = rates is null || rates.Has(region) ? region : DisplayCurrency.UsdCode;
        var listed = codes
            .Select(c => new CurrencyOption(c, names.TryGetValue(c, out var n) ? n : c))
            .OrderBy(o => o.Code == DisplayCurrency.UsdCode ? 0 : 1)
            .ThenBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase);
        return listed.Prepend(new CurrencyOption(Automatic, $"Automatic: follow this computer's region ({automatic})")).ToList();
    }

    private static readonly Lazy<Dictionary<string, string>> Names = new(() =>
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
            {
                try
                {
                    var region = new RegionInfo(culture.Name);
                    if (region.ISOCurrencySymbol.Length == 3 && !names.ContainsKey(region.ISOCurrencySymbol))
                        names[region.ISOCurrencySymbol] = region.CurrencyEnglishName;
                }
                catch (ArgumentException)
                {
                    // A culture without a region.
                }
            }
        }
        catch (Exception)
        {
            // Globalization data isn't available (invariant mode): the list shows bare codes.
        }
        return names;
    });
}

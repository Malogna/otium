using System.Globalization;
using Otium.Core.Currency;
using Otium.Core.Settings;

namespace Otium.App.Services;

/// <summary>
/// Holds the exchange rates (kept in a file, refreshed from Otium's server twice a day) and works out the currency amounts
/// are shown in: the one picked in Settings, or the one of the computer's region. Everything is earned and worked out in
/// US dollars; this only changes how it reads on screen.
/// </summary>
public sealed class CurrencyService(string cacheFile, Func<string?> chosen)
{
    private readonly JsonFileStore<CurrencyRatesCache> _store = new(cacheFile);
    private CurrencyRatesCache? _rates;

    public CurrencyRatesCache Rates => _rates ??= _store.Load();

    public DisplayCurrency Current { get; private set; } = DisplayCurrency.Usd;

    /// <summary>Raised when the currency shown, or its rate, changed.</summary>
    public event Action? Changed;

    /// <summary>Works out the currency to show from the setting and the rates on hand, and tells the UI if it changed.</summary>
    public void Apply()
    {
        var next = CurrencyChoice.Resolve(chosen(), CultureInfo.CurrentCulture, Rates);
        var changed = next.Code != Current.Code || Math.Abs(next.PerUsd - Current.PerUsd) > 1e-12;
        Current = next;
        Format.Display = next;
        if (changed)
            Changed?.Invoke();
    }

    /// <summary>Asks the server for fresh rates when the ones on hand are older than twelve hours (or there are none).</summary>
    public async Task RefreshAsync(RatesClient client, DateTimeOffset now, CancellationToken ct)
    {
        if (!Rates.IsStale(now))
            return;
        RatesResponse? fresh;
        try
        {
            fresh = await client.GetAsync(ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return; // offline: the last rates keep working
        }
        if (fresh is null || fresh.Rates.Count == 0)
            return;
        _rates = new CurrencyRatesCache { AsOf = fresh.AsOf, FetchedAt = now, Rates = fresh.Rates };
        _store.Save(_rates);
        Apply();
    }
}

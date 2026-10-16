using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Otium.Core.Power;

/// <summary>One hour of day-ahead spot price (EUR/kWh; currency cancels out since only the shape is used).</summary>
public sealed record SpotPricePoint(DateTimeOffset Start, double EurPerKwh);

/// <summary>Wire shape of the open Nordic price APIs (hvakosterstrommen.no, elprisetjustnu.se, elprisenligenu.dk).</summary>
internal sealed record SpotPriceEntry(
    [property: JsonPropertyName("EUR_per_kWh")] double EurPerKwh,
    [property: JsonPropertyName("time_start")] DateTimeOffset TimeStart);

/// <summary>Wire shape of sahkotin.fi (Finland): hourly values in EUR/MWh, without VAT.</summary>
internal sealed record SahkotinResponse([property: JsonPropertyName("prices")] List<SahkotinEntry>? Prices);

internal sealed record SahkotinEntry(
    [property: JsonPropertyName("date")] DateTimeOffset Date,
    [property: JsonPropertyName("value")] double EurPerMwh);

/// <summary>
/// Turns a flat retail estimate into a live hourly price by applying today's day-ahead spot shape: the price for the
/// current hour relative to today's average. This lets the engine pause in expensive hours and mine in cheap ones,
/// instead of using one fixed number all day. Only the <em>shape</em> comes from the API; the price <em>level</em>
/// stays the user's own rate or the regional estimate, so no currency conversion is needed. Falls back to a flat
/// shape (1.0) when live data isn't available for the region or can't be fetched.
/// </summary>
public sealed class ElectricityPriceProvider(HttpClient http, TimeProvider? time = null)
{
    /// <summary>Guards against a flat or near-zero daily average producing a meaningless ratio.</summary>
    private const double MinAverageEur = 0.001;

    /// <summary>Spot can spike or go negative; keep the multiplier sane so one odd hour can't dominate decisions.</summary>
    private const double MinMultiplier = 0.0;
    private const double MaxMultiplier = 4.0;

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private SpotPriceZone? _zone;
    private DateOnly _day;
    private IReadOnlyList<SpotPricePoint> _points = [];
    private DateTimeOffset _fetchedAt;

    /// <summary>The zone whose prices are loaded, or null before any successful fetch.</summary>
    public SpotPriceZone? Zone => _zone;

    public bool HasLivePrices => _points.Count >= 12;

    /// <summary>
    /// Multiplier to apply to the retail price for the current hour (1.0 = today's average). Null when there's no
    /// live data, so callers use the flat price unchanged.
    /// </summary>
    public double? CurrentMultiplier()
    {
        if (!HasLivePrices)
            return null;
        var now = _time.GetUtcNow();
        var current = _points.LastOrDefault(p => p.Start <= now);
        if (current is null)
            return null;
        var average = _points.Average(p => p.EurPerKwh);
        if (average < MinAverageEur)
            return null;
        return Math.Clamp(current.EurPerKwh / average, MinMultiplier, MaxMultiplier);
    }

    /// <summary>
    /// Loads today's prices for <paramref name="zone"/> if not already loaded. Safe to call often (cached per day);
    /// a null zone, or a fetch failure, leaves it without live data so callers fall back to the flat price.
    /// </summary>
    public async Task RefreshAsync(SpotPriceZone? zone, CancellationToken ct)
    {
        if (zone is null)
        {
            _zone = null;
            _points = [];
            return;
        }

        var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
        if (_zone == zone && _day == today && HasLivePrices)
            return;

        await _lock.WaitAsync(ct);
        try
        {
            if (_zone == zone && _day == today && HasLivePrices)
                return;
            _points = (await FetchAsync(zone, today, ct)).OrderBy(p => p.Start).ToList();
            _zone = zone;
            _day = today;
            _fetchedAt = _time.GetUtcNow();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or InvalidOperationException or FormatException)
        {
            // Keep yesterday's shape if we have it; otherwise stay flat.
            if (_zone != zone)
            {
                _zone = null;
                _points = [];
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<IEnumerable<SpotPricePoint>> FetchAsync(SpotPriceZone zone, DateOnly day, CancellationToken ct)
    {
        var url = zone.UrlFor(day);
        if (zone.Format == SpotPriceFormat.Sahkotin)
        {
            var response = await http.GetFromJsonAsync<SahkotinResponse>(url, ct);
            return (response?.Prices ?? []).Select(e => new SpotPricePoint(e.Date, e.EurPerMwh / 1000));
        }

        var entries = await http.GetFromJsonAsync<List<SpotPriceEntry>>(url, ct);
        return (entries ?? []).Select(e => new SpotPricePoint(e.TimeStart, e.EurPerKwh));
    }

    /// <summary>A short note for the UI, e.g. "28% below today's average".</summary>
    public string? Describe()
    {
        if (CurrentMultiplier() is not { } m)
            return null;
        var pct = (int)Math.Round(Math.Abs(m - 1) * 100);
        if (pct < 5)
            return "about average for today";
        return m < 1
            ? string.Create(CultureInfo.InvariantCulture, $"{pct}% below today's average right now")
            : string.Create(CultureInfo.InvariantCulture, $"{pct}% above today's average right now");
    }
}

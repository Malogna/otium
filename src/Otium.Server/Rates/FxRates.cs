using System.Globalization;
using System.Text.Json;
using Otium.Core.Currency;

namespace Otium.Server.Rates;

public interface IFxFeed
{
    /// <summary>Units of each currency per one US dollar, or null when the source can't be reached.</summary>
    Task<RatesResponse?> GetAsync(CancellationToken ct);
}

/// <summary>
/// Daily exchange rates from open.er-api.com (about 160 currencies, no key), with Frankfurter (the European Central
/// Bank's reference rates, about 30 currencies) as the fallback.
/// </summary>
public sealed class FxFeedClient(HttpClient http) : IFxFeed
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    // Metals, IMF units and the like are not money anyone earns in.
    private static readonly HashSet<string> NotMoney = new(StringComparer.Ordinal) { "XAU", "XAG", "XPT", "XPD", "XDR", "XBT", "XTS", "XXX" };

    public async Task<RatesResponse?> GetAsync(CancellationToken ct)
    {
        foreach (var (url, parse) in new (string, Func<JsonElement, RatesResponse?>)[]
                 {
                     ("https://open.er-api.com/v6/latest/USD", ParseOpenErApi),
                     ("https://api.frankfurter.dev/v1/latest?base=USD", ParseFrankfurter),
                 })
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(Timeout);
            try
            {
                using var response = await http.GetAsync(url, timeout.Token);
                response.EnsureSuccessStatusCode();
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(timeout.Token));
                if (parse(doc.RootElement) is { } rates)
                    return rates;
            }
            catch (Exception e) when (!ct.IsCancellationRequested && e is HttpRequestException or TaskCanceledException or JsonException)
            {
                // Try the next source.
            }
        }
        return null;
    }

    /// <summary><c>{"result":"success","time_last_update_unix":…,"rates":{"EUR":0.92,…}}</c></summary>
    internal static RatesResponse? ParseOpenErApi(JsonElement root)
    {
        if (root.TryGetProperty("result", out var result) && result.GetString() != "success")
            return null;
        var asOf = root.TryGetProperty("time_last_update_unix", out var unix) && unix.TryGetInt64(out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : DateTimeOffset.UtcNow;
        return Rates(root, asOf);
    }

    /// <summary><c>{"base":"USD","date":"2026-11-02","rates":{"EUR":0.92,…}}</c></summary>
    internal static RatesResponse? ParseFrankfurter(JsonElement root)
    {
        var asOf = root.TryGetProperty("date", out var date)
                   && DateTimeOffset.TryParse(date.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : DateTimeOffset.UtcNow;
        return Rates(root, asOf);
    }

    private static RatesResponse? Rates(JsonElement root, DateTimeOffset asOf)
    {
        if (!root.TryGetProperty("rates", out var rates) || rates.ValueKind != JsonValueKind.Object)
            return null;
        var map = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var rate in rates.EnumerateObject())
        {
            var code = rate.Name;
            if (code.Length == 3 && code.All(c => c is >= 'A' and <= 'Z') && code != DisplayCurrency.UsdCode && !NotMoney.Contains(code)
                && rate.Value.ValueKind == JsonValueKind.Number && rate.Value.TryGetDouble(out var value) && double.IsFinite(value) && value > 0)
                map[code] = value;
        }
        return map.Count == 0 ? null : new RatesResponse(DisplayCurrency.UsdCode, asOf, map);
    }
}

/// <summary>The rates the apps ask for, fetched at most every few hours and kept for a week if the sources go down.</summary>
public sealed class FxRatesService(IFxFeed feed, TimeProvider time)
{
    public static readonly TimeSpan RefreshAfter = TimeSpan.FromHours(6);

    public static readonly TimeSpan KeepFor = TimeSpan.FromDays(7);

    /// <summary>When the sources are down, apps asking again don't each wait for them: one try every few minutes.</summary>
    public static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private RatesResponse? _rates;
    private DateTimeOffset _fetchedAt;
    private DateTimeOffset? _triedAt;

    public async Task<RatesResponse?> GetAsync(CancellationToken ct)
    {
        if (UseKept(time.GetUtcNow()))
            return _rates;
        await _gate.WaitAsync(ct);
        try
        {
            var now = time.GetUtcNow();
            if (UseKept(now))
                return _rates;
            _triedAt = now;
            if (await feed.GetAsync(ct) is { } fresh)
            {
                _rates = fresh;
                _fetchedAt = now;
            }
            else if (_rates is not null && now - _fetchedAt >= KeepFor)
            {
                _rates = null;
            }
            return _rates;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Whether to answer with what is kept (even nothing): it is fresh, or the last try was too recent to try again.</summary>
    private bool UseKept(DateTimeOffset now) =>
        _rates is not null && now - _fetchedAt < RefreshAfter || _triedAt is { } tried && now - tried < RetryAfter;
}

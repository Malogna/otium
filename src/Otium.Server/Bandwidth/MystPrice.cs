using Otium.Server.RealEarnings;

namespace Otium.Server.Bandwidth;

/// <summary>
/// MYST's dollar price, sent in every account summary so the app can show Mysterium sharing earnings (paid in MYST) in
/// dollars. Read from the coin price source at most hourly, in the background, so a summary never waits on it; until
/// the first read (or while the source is down) it's the last known price, or null.
/// </summary>
public sealed class MystPrice(IServiceProvider services, TimeProvider time, ILogger<MystPrice> logger)
{
    public const string Symbol = "MYST";

    public static readonly TimeSpan RefreshEvery = TimeSpan.FromHours(1);

    private double? _usd;
    private DateTimeOffset _readAt = DateTimeOffset.MinValue;
    private int _refreshing;

    /// <summary>The last known price, starting a background refresh when it's older than <see cref="RefreshEvery"/>.</summary>
    public double? Current
    {
        get
        {
            if (time.GetUtcNow() - _readAt >= RefreshEvery && Interlocked.CompareExchange(ref _refreshing, 1, 0) == 0)
                _ = Task.Run(RefreshAsync);
            return _usd;
        }
    }

    /// <summary>Reads the price now (the background refresh, and tests).</summary>
    public async Task RefreshAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var prices = services.GetRequiredService<ICoinPrices>();
            if ((await prices.GetUsdAsync(timeout.Token)).TryGetValue(Symbol, out var usd) && usd > 0)
                _usd = usd;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogWarning(e, "MYST price unavailable; keeping the last one");
        }
        finally
        {
            // A failed read waits the same hour: the app keeps showing MYST amounts meanwhile.
            _readAt = time.GetUtcNow();
            Interlocked.Exchange(ref _refreshing, 0);
        }
    }
}

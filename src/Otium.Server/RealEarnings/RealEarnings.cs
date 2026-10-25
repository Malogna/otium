using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Otium.Core.Accounts;
using Otium.Core.Pools;
using Otium.Core.Wallets;
using Otium.Server.Accounts;
using Otium.Server.Data;

namespace Otium.Server.RealEarnings;

/// <summary>
/// The website's real numbers: what the pools paid Otium users, and what real computers earned per graphics card,
/// from the reports of users who chose to share them.
/// </summary>
public sealed class RealEarningsOptions
{
    public const string Section = "RealEarnings";

    /// <summary>Reads what the pools paid each user's address. Off in tests and when the operator doesn't want it.</summary>
    public bool CheckPools { get; set; } = true;

    public double PoolCheckHours { get; set; } = 12;

    /// <summary>Pause between two addresses, to stay a polite user of the pools' public APIs.</summary>
    public double PoolRequestDelaySeconds { get; set; } = 2;

    /// <summary>
    /// A card (or country) appears on the site only with reports from computers of at least this many different accounts,
    /// so no one's numbers stand alone and no one can make up a card's figures with a few computers of their own.
    /// </summary>
    public int MinimumComputersPerCard { get; set; } = 3;

    /// <summary>
    /// Reports count only from accounts at least this old: making accounts is free, so a fresh batch of them can't move
    /// the site's figures right away.
    /// </summary>
    public int MinimumAccountAgeDays { get; set; } = 7;

    /// <summary>The period the per-card figures cover.</summary>
    public int WindowDays { get; set; } = 7;

    /// <summary>A computer counts for its card once it earned this many hours in the window.</summary>
    public double MinimumHours { get; set; } = 1;

    /// <summary>
    /// "Real numbers" stays off the front page until Otium users have earned at least this much (paid so far, or credited
    /// in the last 30 days, whichever is more), so it never shows a figure like "$1".
    /// </summary>
    public double MinimumEarnedUsd { get; set; } = 100;

    /// <summary>... and until at least this many users have been credited or paid by the pools.</summary>
    public int MinimumUsers { get; set; } = 50;
}

/// <summary>One graphics card model as real Otium computers ran it.</summary>
/// <param name="UsdPerEarningDay">Median earned per 24 hours of earning, after Otium's fee, before electricity.</param>
/// <param name="HoursPerDay">Median hours a day those computers earned.</param>
/// <param name="UsdPerDay">Median actually earned per calendar day (fewer hours, less money).</param>
public sealed record RealCard(string Model, int Computers, double UsdPerEarningDay, double HoursPerDay, double UsdPerDay);

/// <summary>Internet sharing in one country as real Otium computers did it (after Otium's fee).</summary>
/// <param name="Country">Two-letter country code.</param>
/// <param name="UsdPerSharingDay">Median earned per 24 hours of sharing online.</param>
/// <param name="UsdPerMonth">Median actually earned per month, at the hours those computers shared.</param>
public sealed record RealSharing(string Country, int Computers, double UsdPerSharingDay, double UsdPerMonth);

/// <param name="PaidUsd">Paid by the pools to Otium users' own addresses, in dollars at the time of each check.</param>
/// <param name="PaidUsers">Users' payout addresses (each counted once, however many accounts use it) that have received at
/// least one payout.</param>
/// <param name="Rewarded30dUsd">Credited to those addresses by the pools in the last 30 days.</param>
/// <param name="ShowSection">Enough users have earned enough for the front page's "Real numbers" (the per-country sharing
/// medians are used either way).</param>
public sealed record RealEarningsSummary(double PaidUsd, long PaidUsers, double Rewarded30dUsd, IReadOnlyList<RealCard> Cards, int WindowDays,
    IReadOnlyList<RealSharing>? Sharing = null, bool ShowSection = true)
{
    public IReadOnlyList<RealSharing> SharingByCountry => Sharing ?? [];

    public bool IsEmpty => PaidUsd < 1 && Rewarded30dUsd < 1 && Cards.Count == 0 && SharingByCountry.Count == 0;
}

/// <summary>Checks and stores the opt-in earnings reports.</summary>
public sealed class EarningsReportService(OtiumStore store, TimeProvider time)
{
    /// <summary>More than any desktop card or processor earns per day of mining today; anything above is a broken or forged report.</summary>
    public const double MaxGpuUsdPerDay = 50;

    public const double MaxCpuUsdPerDay = 5;

    /// <summary>Internet sharing: 5 GB a day at Proxybase's highest seller rate (mobile, $3.60/GB) is about $18.</summary>
    public const double MaxSharingUsdPerDay = 20;

    /// <summary>Also generous: a 1.5 kW machine at $1/kWh.</summary>
    public const double MaxElectricityUsdPerDay = 36;

    /// <summary>Internet sharing through Proxybase; the site's per-country figures are Proxybase's alone.</summary>
    public const string Sharing = "sharing";

    /// <summary>Internet sharing through Mysterium (MYST at the day's price), kept apart so the two networks never mix.</summary>
    public const string SharingMysterium = "sharing-mysterium";

    public void Report(Session session, EarningsReport report)
    {
        var now = time.GetUtcNow();
        if (report.Sequence <= 0 || report.PeriodEnd <= report.PeriodStart || report.PeriodEnd > now + TimeSpan.FromMinutes(10)
            || report.PeriodEnd - report.PeriodStart > TimeSpan.FromDays(31) || report.Devices is not { Count: > 0 and <= 4 })
            throw new OtiumException(StatusCodes.Status400BadRequest, "This earnings report doesn't look right.");

        var period = (report.PeriodEnd - report.PeriodStart).TotalSeconds;
        foreach (var d in report.Devices)
        {
            var days = d.EarningSeconds / 86400;
            // A computer with several cards reports "multi:N" (up to 16 cards) with the whole computer's earnings.
            var cards = d.Kind == "gpu" && MultiCardCount(d.Model) is { } n ? n : 1;
            var maxUsd = d.Kind switch { "gpu" => MaxGpuUsdPerDay * cards, Sharing or SharingMysterium => MaxSharingUsdPerDay, _ => MaxCpuUsdPerDay };
            // Sharing's "model" is the computer's country: two letters, nothing else.
            if (d.Kind is Sharing or SharingMysterium && (d.Model is not { Length: 2 } || !d.Model.All(char.IsAsciiLetter) || d.ElectricityUsd != 0))
                throw new OtiumException(StatusCodes.Status400BadRequest, "This earnings report doesn't look right.");
            if (d.Kind is not ("cpu" or "gpu" or Sharing or SharingMysterium) || d.Model is { Length: > 100 } || d.EarningSeconds < 0 || d.EarningSeconds > period + 600
                || d.UserUsd < 0 || d.UserUsd > maxUsd * days + 0.01 || d.ElectricityUsd < 0 || d.ElectricityUsd > MaxElectricityUsdPerDay * cards * days + 0.01)
                throw new OtiumException(StatusCodes.Status400BadRequest, "This earnings report doesn't look right.");
        }

        var clean = report with
        {
            Devices = report.Devices.Select(d => d with
            {
                Model = d.Kind is Sharing or SharingMysterium ? d.Model!.ToUpperInvariant() : MultiCardCount(d.Model) is not null ? d.Model : Clean(d.Model),
            }).ToList(),
        };
        if (store.AddEarningsReport(session.Device.DeviceId, session.Account.AccountId, clean, now) == OtiumStore.EarningsReportOutcome.Overlaps)
            throw new OtiumException(StatusCodes.Status400BadRequest, "This earnings report covers time an earlier report already did.");
    }

    /// <summary>The number of cards in a "multi:N" model (2–16), or null.</summary>
    public static int? MultiCardCount(string? model) =>
        model is { } m && m.StartsWith("multi:", StringComparison.Ordinal) && int.TryParse(m.AsSpan(6), out var n) && n is >= 2 and <= 16 ? n : null;

    /// <summary>Letters, digits and a few separators only, so a model name can't carry markup or anything else.</summary>
    private static string? Clean(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return null;
        var kept = new string(model.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '(' or ')' or '@' or '.').ToArray()).Trim();
        return kept.Length == 0 ? null : kept;
    }
}

/// <summary>Today's dollar price of the payout coins (CoinGecko's free API), cached for an hour.</summary>
public interface ICoinPrices
{
    Task<IReadOnlyDictionary<string, double>> GetUsdAsync(CancellationToken ct);
}

public sealed class CoinGeckoPrices(HttpClient http, TimeProvider time) : ICoinPrices
{
    /// <summary>Payout coin symbol → CoinGecko id, plus MYST for Mysterium sharing's earnings in dollars.</summary>
    public static readonly IReadOnlyDictionary<string, string> Ids = new Dictionary<string, string>
    {
        ["USDT"] = "tether",
        ["BTC"] = "bitcoin",
        ["LTC"] = "litecoin",
        ["BNB"] = "binancecoin",
        ["SOL"] = "solana",
        ["TRX"] = "tron",
        ["ETH"] = "ethereum",
        ["XMR"] = "monero",
        ["DOGE"] = "dogecoin",
        ["MYST"] = "mysterium",
    };

    private readonly SemaphoreSlim _lock = new(1, 1);
    private IReadOnlyDictionary<string, double>? _prices;
    private DateTimeOffset _fetchedAt;

    public async Task<IReadOnlyDictionary<string, double>> GetUsdAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_prices is not null && time.GetUtcNow() - _fetchedAt < TimeSpan.FromHours(1))
                return _prices;
            var url = $"https://api.coingecko.com/api/v3/simple/price?ids={string.Join(',', Ids.Values)}&vs_currencies=usd";
            var body = await http.GetFromJsonAsync<Dictionary<string, Dictionary<string, double>>>(url, ct) ?? [];
            _prices = Ids
                .Where(kv => body.TryGetValue(kv.Value, out var p) && p.TryGetValue("usd", out var usd) && usd > 0)
                .ToDictionary(kv => kv.Key, kv => body[kv.Value]["usd"]);
            _fetchedAt = time.GetUtcNow();
            return _prices;
        }
        finally
        {
            _lock.Release();
        }
    }
}

/// <summary>
/// Reads what the pools have paid each user's own address, a couple of times a day, for the site's "paid to Otium
/// users" figure. Mining the user does elsewhere isn't counted as Otium's: an unMineable address with workers that aren't
/// Otium's (named "ot" + device) is left out, and a MoneroOcean address counts only while the pool lists its workers and
/// they're all Otium's (its worker history keeps about three days, so an address that stopped mining drops out).
/// </summary>
public sealed class PoolPayoutChecker(
    OtiumStore store, IUnmineableApi unmineable, IMoneroPoolApi moneroOcean, ICoinPrices prices, IOptions<RealEarningsOptions> options,
    TimeProvider time, ILogger<PoolPayoutChecker> logger)
{
    public async Task CheckAllAsync(CancellationToken ct)
    {
        IReadOnlyDictionary<string, double> usd;
        try
        {
            usd = await prices.GetUsdAsync(ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(e, "Coin prices unavailable; pool payouts not checked this time");
            return;
        }

        var delay = TimeSpan.FromSeconds(Math.Max(0, options.Value.PoolRequestDelaySeconds));
        foreach (var (accountId, wallet) in store.OwnWallets())
        {
            try
            {
                await CheckAsync(accountId, wallet, usd, ct);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException or System.Text.Json.JsonException && !ct.IsCancellationRequested)
            {
                logger.LogDebug(e, "Pool payout check failed for one address");
            }
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay, time, ct);
        }
    }

    internal async Task CheckAsync(string accountId, string wallet, IReadOnlyDictionary<string, double> usd, CancellationToken ct)
    {
        // Monero is stored as the bare address: unMineable outside the US, MoneroOcean for US users.
        var target = PayoutTarget.ParseOrNull(wallet) ?? PayoutTarget.ParseOrNull($"XMR:{wallet}");
        if (target is null || !usd.TryGetValue(target.Coin.Symbol, out var price))
            return;

        var address = await unmineable.GetAddressAsync(target, ct);
        if (address.Uuid is { } uuid)
        {
            var workers = await unmineable.GetWorkersAsync(uuid, ct);
            // Which computers are mining (for the fee-slice check), before mixed addresses are left out of the totals.
            foreach (var worker in Working(workers))
                store.RecordWorkerSighting(worker, "own", time.GetUtcNow());
            if (workers.Any(w => !IsOtiumWorker(w.Name)))
            {
                store.DeletePoolPayout(accountId);
                return;
            }
            var stats = await unmineable.GetStatsAsync(uuid, ct);
            store.SetPoolPayout(accountId, target.ToString(), target.Coin.Symbol, (double)stats.Paid, (double)stats.Paid * price,
                (double)stats.Rewarded30d * price, time.GetUtcNow());
            return;
        }

        if (target.Coin != PayoutCoins.Monero)
            return;
        // MoneroOcean: its per-worker hashrate history is the only list of who mines to the address.
        var moWorkers = (await moneroOcean.GetWorkerHistoryAsync(target.Address, ct)).Keys.ToList();
        if (moWorkers.Count == 0 || moWorkers.Any(w => !IsOtiumWorker(w)) || await moneroOcean.GetMinerStatsAsync(target.Address, ct) is not { } mo)
        {
            store.DeletePoolPayout(accountId);
            return;
        }
        store.SetPoolPayout(accountId, target.ToString(), "XMR", mo.PaidXmr, mo.PaidXmr * price, 0, time.GetUtcNow());
    }

    /// <summary>The computers (8 hex digits of the device id) whose Otium workers the pool shows working.</summary>
    internal static IEnumerable<string> Working(IEnumerable<UnmineableWorker> workers) =>
        workers.Where(w => IsOtiumWorker(w.Name) && (w.Online || w.CalculatedHps > 0 || w.ReportedHps > 0))
            .Select(w => w.Name[2..10]).Distinct();

    /// <summary>Otium names workers "ot" + 8 hex digits of the device id, plus "g" for the graphics card.</summary>
    internal static bool IsOtiumWorker(string name) =>
        name.Length is 10 or 11 && name.StartsWith("ot", StringComparison.Ordinal)
        && name[2..10].All(Uri.IsHexDigit) && (name.Length == 10 || name[10] == 'g');
}

/// <summary>
/// Every hour, which computers' miners Otium's own fee address shows working: the fee slice is about 29 minutes per day
/// of mining, so a computer that mines for days without ever appearing here isn't mining Otium's fee (a modified app).
/// The admin dashboard lists those (<see cref="OtiumStore.FeeSliceGaps"/>).
/// </summary>
public sealed class FeeSliceWatcher(OtiumStore store, IUnmineableApi unmineable, IOptions<UnmineableOptions> options, TimeProvider time,
    ILogger<FeeSliceWatcher> logger) : BackgroundService
{
    /// <summary>How long sightings are kept (beyond the dashboard's 7-day window).</summary>
    public static readonly TimeSpan Memory = TimeSpan.FromDays(30);

    public async Task CheckAsync(CancellationToken ct)
    {
        if (PayoutTarget.ParseOrNull(options.Value.OtiumTarget) is not { } target || target.Coin == PayoutCoins.Monero)
            return;
        var address = await unmineable.GetAddressAsync(target, ct);
        if (address.Uuid is not { } uuid)
            return;
        var now = time.GetUtcNow();
        foreach (var worker in PoolPayoutChecker.Working(await unmineable.GetWorkersAsync(uuid, ct)))
            store.RecordWorkerSighting(worker, "fee", now);
        store.DeleteWorkerSightingsBefore(now - Memory);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(3), time, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAsync(stoppingToken);
            }
            catch (Exception e) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(e, "Fee slice check failed");
            }
            await Task.Delay(TimeSpan.FromHours(1), time, stoppingToken);
        }
    }
}

public sealed class PoolPayoutService(PoolPayoutChecker checker, IOptions<RealEarningsOptions> options, TimeProvider time, ILogger<PoolPayoutService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the server settle (and the GPU policy load) before the first round.
        await Task.Delay(TimeSpan.FromMinutes(2), time, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await checker.CheckAllAsync(stoppingToken);
            }
            catch (Exception e) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(e, "Pool payout round failed");
            }
            await Task.Delay(TimeSpan.FromHours(Math.Max(1, options.Value.PoolCheckHours)), time, stoppingToken);
        }
    }
}

/// <summary>The figures the website shows, recomputed at most every ten minutes.</summary>
public sealed class RealEarningsService(OtiumStore store, IOptions<RealEarningsOptions> options, TimeProvider time)
{
    private readonly Lock _lock = new();
    private RealEarningsSummary? _cached;
    private DateTimeOffset _cachedAt;

    public RealEarningsSummary Get()
    {
        var now = time.GetUtcNow();
        lock (_lock)
        {
            if (_cached is not null && now - _cachedAt < TimeSpan.FromMinutes(10))
                return _cached;
        }
        var summary = Compute(now);
        lock (_lock)
        {
            _cached = summary;
            _cachedAt = now;
        }
        return summary;
    }

    internal RealEarningsSummary Compute(DateTimeOffset now)
    {
        var o = options.Value;
        var (paidUsd, rewarded, paidUsers, earningUsers) = store.PoolPayoutTotals();
        var showSection = Math.Max(paidUsd, rewarded) >= o.MinimumEarnedUsd && earningUsers >= o.MinimumUsers;
        var window = TimeSpan.FromDays(o.WindowDays).TotalSeconds;
        var establishedBefore = now - TimeSpan.FromDays(Math.Max(0, o.MinimumAccountAgeDays));
        var rows = store.EarningsSince(now - TimeSpan.FromDays(o.WindowDays))
            .Where(r => r.AccountCreatedAt is { } created && created <= establishedBefore)
            .Select(r =>
            {
                // No computer earns for longer than the window; anything above it (reports stored before overlapping ones
                // were refused) is cut, with its dollars, so the computer's rate stays what it reported.
                var keep = r.Seconds > window ? window / r.Seconds : 1;
                return new Reported(r.DeviceId, r.Kind, r.Model, r.AccountId, r.Seconds * keep, r.UserUsd * keep);
            })
            .Where(r => r.Seconds >= o.MinimumHours * 3600)
            .ToList();
        var minimumAccounts = Math.Max(2, o.MinimumComputersPerCard);
        var cards = rows
            .Where(r => r.Kind == "gpu" && EarningsReportService.MultiCardCount(r.Model) is null)
            .GroupBy(r => r.Model)
            .Where(g => g.Select(r => r.AccountId).Distinct().Count() >= minimumAccounts)
            .Select(g => new RealCard(
                g.Key,
                g.Select(r => r.DeviceId).Distinct().Count(),
                AccountMedian(g, r => r.UserUsd / r.Seconds * 86400),
                AccountMedian(g, r => r.Seconds / 3600 / o.WindowDays),
                AccountMedian(g, r => r.UserUsd / o.WindowDays)))
            .OrderByDescending(c => c.UsdPerEarningDay)
            .ToList();
        var sharing = rows
            .Where(r => r.Kind == EarningsReportService.Sharing) // Proxybase only: Mysterium pays a different network's rates
            .GroupBy(r => r.Model)
            .Where(g => g.Select(r => r.AccountId).Distinct().Count() >= minimumAccounts)
            .Select(g => new RealSharing(
                g.Key,
                g.Select(r => r.DeviceId).Distinct().Count(),
                AccountMedian(g, r => r.UserUsd / r.Seconds * 86400),
                AccountMedian(g, r => r.UserUsd / o.WindowDays * 30)))
            .OrderByDescending(s => s.UsdPerMonth)
            .ToList();
        return new RealEarningsSummary(paidUsd, paidUsers, rewarded, cards, o.WindowDays, sharing, showSection);
    }

    private sealed record Reported(string DeviceId, string Kind, string Model, string AccountId, double Seconds, double UserUsd);

    /// <summary>
    /// The median over accounts of each account's own median: an account with many computers counts once, so it can't
    /// outvote the others.
    /// </summary>
    private static double AccountMedian(IEnumerable<Reported> rows, Func<Reported, double> value) =>
        Median(rows.GroupBy(r => r.AccountId).Select(a => Median(a.Select(value))));

    internal static double Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToList();
        if (sorted.Count == 0)
            return 0;
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }
}

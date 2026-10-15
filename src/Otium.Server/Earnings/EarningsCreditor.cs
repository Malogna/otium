using Microsoft.Extensions.Options;
using Otium.Core.Fees;
using Otium.Core.Pools;
using Otium.Server.Accounts;
using Otium.Server.Data;

namespace Otium.Server.Earnings;

public sealed record CreditRunResult(int WorkersCredited, decimal UsersUsd, decimal ReferralsUsd);

/// <summary>
/// Turns balance accounts' mining into dollars. For each pool address it reads MoneroOcean's per-worker hashrate
/// history, integrates the XMR-equivalent hashrate ("hs2") since the last run, and credits its expected value
/// (hashes × block reward ÷ difficulty × XMR price). Otium carries the pool's luck; the admin summary reconciles
/// what was credited against what the pool actually paid.
/// </summary>
public sealed class EarningsCreditor(
    OtiumStore store,
    IMoneroPoolApi pool,
    IOptions<EarningsOptions> options,
    TimeProvider time,
    ILogger<EarningsCreditor> logger)
{
    /// <summary>MoneroOcean writes a history point about every two minutes.</summary>
    public static readonly TimeSpan PointSpacing = TimeSpan.FromMinutes(2);

    /// <summary>Longer gaps between points (miner offline, or older sparse history) count as at most this much time.</summary>
    public static readonly TimeSpan MaxPointGap = TimeSpan.FromMinutes(10);

    private readonly EarningsOptions _options = options.Value;

    public async Task<CreditRunResult> CreditOnceAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        var network = await pool.GetNetworkStatsAsync(ct);
        if (network.Difficulty <= 0 || network.PriceUsd <= 0)
        {
            logger.LogWarning("Skipping crediting: network stats unavailable");
            return new CreditRunResult(0, 0, 0);
        }

        var usdPerHash = network.BlockRewardXmr / network.Difficulty * network.PriceUsd;
        var progress = store.WorkerProgress();
        var workers = 0;
        decimal users = 0, referrals = 0;

        foreach (var (address, _) in store.PoolWalletLoads())
        {
            IReadOnlyDictionary<string, IReadOnlyList<HashratePoint>> history;
            try
            {
                history = await pool.GetWorkerHistoryAsync(address, ct);
            }
            catch (HttpRequestException e)
            {
                logger.LogWarning(e, "Couldn't read worker history for a pool address; will retry next run");
                continue;
            }

            foreach (var (workerId, points) in history)
            {
                var device = store.FindByWorker(workerId);
                // Unknown workers (or "all_other_workers") can't be attributed; the operator keeps those earnings.
                if (device is null || device.PoolWallet != address)
                    continue;

                var lastMs = progress.TryGetValue(workerId, out var seen) ? seen : (long?)null;
                var (hashes, newestMs) = Integrate(points, lastMs);
                if (newestMs is null)
                    continue;
                store.SetWorkerProgress(workerId, newestMs.Value, now);
                if (hashes <= 0)
                    continue;

                var (user, referral) = Credit(device, hashes * usdPerHash, newestMs.Value, now);
                users += user;
                referrals += referral;
                workers++;
            }
        }

        if (workers > 0)
            logger.LogInformation("Credited {Workers} workers: ${Users} to users, ${Referrals} referral rewards", workers, users, referrals);
        return new CreditRunResult(workers, users, referrals);
    }

    /// <summary>Hashes (XMR-equivalent) represented by the points after <paramref name="lastMs"/>.</summary>
    internal static (double Hashes, long? NewestMs) Integrate(IReadOnlyList<HashratePoint> points, long? lastMs)
    {
        double hashes = 0;
        long? newest = null;
        DateTimeOffset? previous = lastMs is { } ms ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : null;

        foreach (var point in points)
        {
            var pointMs = point.Time.ToUnixTimeMilliseconds();
            if (lastMs is { } last && pointMs <= last)
                continue;

            var span = previous is { } p ? point.Time - p : PointSpacing;
            if (span > MaxPointGap)
                span = MaxPointGap;
            if (span > TimeSpan.Zero && point.NormalizedHps > 0)
                hashes += point.NormalizedHps * span.TotalSeconds;

            previous = point.Time;
            newest = pointMs;
        }
        return (hashes, newest);
    }

    private (decimal User, decimal Referral) Credit(DeviceRecord device, double grossUsd, long newestMs, DateTimeOffset now)
    {
        var feeFree = device.FeeFreeUntil is { } until && now < until;
        var fee = feeFree ? 0 : _options.FeePercent / 100.0;
        var availableAt = now + _options.HoldPeriod;

        var userMicros = (long)(grossUsd * (1 - fee) * OtiumStore.MicrosPerDollar);
        if (userMicros > 0)
            store.AddLedger(device.DeviceId, LedgerKinds.Earning, userMicros, now, availableAt, $"{device.WorkerId}@{newestMs}");

        long referralMicros = 0;
        if (device.ReferredByCode is { } code
            && device.ReferredAt is { } referredAt
            && now < referredAt + FeePolicy.Default.ReferrerRewardPeriod
            && store.FindByCode(code) is { IsBalanceAccount: true } referrer)
        {
            referralMicros = (long)(grossUsd * _options.ReferralPercent / 100.0 * OtiumStore.MicrosPerDollar);
            if (referralMicros > 0)
                store.AddLedger(referrer.DeviceId, LedgerKinds.Referral, referralMicros, now, availableAt, $"ref:{device.WorkerId}@{newestMs}");
        }

        return (AccountService.ToUsd(userMicros), AccountService.ToUsd(referralMicros));
    }
}

/// <summary>Runs the creditor on a timer.</summary>
public sealed class EarningsCreditorService(EarningsCreditor creditor, IOptions<EarningsOptions> options, ILogger<EarningsCreditorService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.CreditInterval);
        do
        {
            try
            {
                await creditor.CreditOnceAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Crediting run failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

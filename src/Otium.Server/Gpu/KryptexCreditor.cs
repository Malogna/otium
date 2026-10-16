using Microsoft.Extensions.Options;
using Otium.Server.Data;
using Otium.Server.Earnings;

namespace Otium.Server.Gpu;

/// <summary>
/// Credits balance accounts for GPU coins mined on Kryptex (Pearl, Quantus). Kryptex keeps per-worker hashrate history
/// for the operator's account, and each user mines under their own worker id, so each run integrates the new points
/// into hashes and credits their value at the policy's live rate, after the pool fee. As with MoneroOcean, Otium carries
/// the pool's luck and the price between crediting and Kryptex's auto-exchange; the admin summary shows what Kryptex
/// reports as paid and unpaid to reconcile against.
/// </summary>
public sealed class KryptexCreditor(
    OtiumStore store,
    IKryptexApi kryptex,
    GpuPolicyService policy,
    IOptions<KryptexOptions> kryptexOptions,
    IOptions<EarningsOptions> earningsOptions,
    TimeProvider time,
    ILogger<KryptexCreditor> logger)
{
    /// <summary>Used when a worker's history has too few points to tell how far apart they are.</summary>
    public static readonly TimeSpan DefaultPointSpacing = TimeSpan.FromMinutes(10);

    /// <summary>Workers credited this recently are checked even when Kryptex no longer lists them as active.</summary>
    private static readonly TimeSpan RecentWorker = TimeSpan.FromDays(2);

    public async Task<CreditRunResult> CreditOnceAsync(CancellationToken ct)
    {
        var options = kryptexOptions.Value;
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.Account))
            return new CreditRunResult(0, 0, 0);

        var account = options.Account.Trim();
        var now = time.GetUtcNow();
        var current = await policy.GetAsync(ct);
        var progress = store.WorkerProgress();
        var workers = 0;
        long userMicros = 0, referralMicros = 0;

        foreach (var (algorithm, coin) in options.Coins)
        {
            // Work already done is paid even if the algorithm was just switched off, at the last known rate.
            var rate = current.For(algorithm) is { UsdPerHashPerSecondDay: > 0 } a ? a : null;
            if (rate is null)
                continue;
            var usdPerHash = rate.UsdPerHashPerSecondDay / 86_400 * (1 - rate.PoolFeePercent / 100);

            var prefix = $"kx:{coin.Coin}:";
            IEnumerable<string> names;
            try
            {
                names = (await kryptex.GetWorkersAsync(coin.Coin, account, ct)).Select(w => w.Name);
            }
            catch (HttpRequestException e)
            {
                logger.LogWarning(e, "Couldn't list Kryptex workers for {Coin}; will retry next run", coin.Coin);
                continue;
            }
            var recent = progress
                .Where(p => p.Key.StartsWith(prefix, StringComparison.Ordinal)
                            && DateTimeOffset.FromUnixTimeMilliseconds(p.Value) > now - RecentWorker)
                .Select(p => p.Key[prefix.Length..]);

            foreach (var worker in names.Concat(recent).Distinct(StringComparer.Ordinal))
            {
                var owner = store.Resolve(store.FindAccountByWorker(worker));
                if (owner is not { IsBalanceAccount: true })
                    continue; // not one of ours (or an own-wallet account, which can't use Kryptex)

                IReadOnlyList<KryptexPoint> points;
                try
                {
                    points = await kryptex.GetWorkerChartAsync(coin.Coin, account, worker, ct);
                }
                catch (HttpRequestException e)
                {
                    logger.LogWarning(e, "Couldn't read Kryptex history for a worker; will retry next run");
                    continue;
                }

                var key = prefix + worker;
                var lastMs = progress.TryGetValue(key, out var seen) ? seen : (long?)null;
                var (hashes, newestMs) = Integrate(points, lastMs);
                if (newestMs is null)
                    continue;
                store.SetWorkerProgress(key, newestMs.Value, now);
                if (hashes <= 0)
                    continue;

                var (user, referral) = EarningsCreditor.Credit(store, earningsOptions.Value, owner, hashes * usdPerHash,
                    $"{key}@{newestMs.Value}", now);
                userMicros += user;
                referralMicros += referral;
                workers++;
            }
        }

        var result = new CreditRunResult(workers, Accounts.AccountService.ToUsd(userMicros), Accounts.AccountService.ToUsd(referralMicros));
        if (workers > 0)
            logger.LogInformation("Credited {Workers} Kryptex workers: ${Users} to users, ${Referrals} referral rewards", workers, result.UsersUsd, result.ReferralsUsd);
        return result;
    }

    /// <summary>
    /// Hashes represented by the points after <paramref name="lastMs"/>. Each point stands for the time since the
    /// previous one; gaps longer than twice the usual spacing (the worker was offline) count as twice the spacing.
    /// </summary>
    internal static (double Hashes, long? NewestMs) Integrate(IReadOnlyList<KryptexPoint> points, long? lastMs)
    {
        var spacing = TypicalSpacing(points);
        var maxGap = spacing * 2;
        double hashes = 0;
        long? newest = null;
        DateTimeOffset? previous = lastMs is { } ms ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : null;

        foreach (var point in points)
        {
            var pointMs = point.Time.ToUnixTimeMilliseconds();
            if (lastMs is { } last && pointMs <= last)
                continue;
            var span = previous is { } p ? point.Time - p : spacing;
            if (span > maxGap)
                span = maxGap;
            if (span > TimeSpan.Zero && point.Hashrate > 0)
                hashes += point.Hashrate * span.TotalSeconds;
            previous = point.Time;
            newest = pointMs;
        }
        return (hashes, newest);
    }

    private static TimeSpan TypicalSpacing(IReadOnlyList<KryptexPoint> points)
    {
        var gaps = points.Zip(points.Skip(1), (a, b) => b.Time - a.Time).Where(g => g > TimeSpan.Zero).Order().ToList();
        return gaps.Count >= 2 ? gaps[gaps.Count / 2] : DefaultPointSpacing;
    }
}

/// <summary>Runs the Kryptex creditor on a timer.</summary>
public sealed class KryptexCreditorService(KryptexCreditor creditor, IOptions<KryptexOptions> options, ILogger<KryptexCreditorService> logger)
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
                logger.LogError(e, "Kryptex crediting run failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

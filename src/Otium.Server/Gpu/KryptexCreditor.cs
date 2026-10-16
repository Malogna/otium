using Microsoft.Extensions.Options;
using Otium.Core.Pools;
using Otium.Server.Data;
using Otium.Server.Earnings;

namespace Otium.Server.Gpu;

/// <summary>
/// Credits balance accounts for coins mined on Kryptex: GPU coins (Pearl, Quantus) at the GPU policy's live rate, and
/// Monero from CPUs routed there at the same network value MoneroOcean crediting uses (block reward ÷ difficulty ×
/// price), so moving a user between the two pools doesn't change what they earn. Each user mines under their own worker
/// id in the operator's account; each run reads every worker's 30-minute average from Kryptex's public worker list,
/// turns it into the hashes done since the last run (see <see cref="HashesSince"/>) and credits their value after the
/// pool fee. As with MoneroOcean, Otium carries
/// the pool's luck and the price between crediting and Kryptex's auto-exchange; the admin summary shows what Kryptex
/// reports as paid and unpaid to reconcile against.
/// </summary>
public sealed class KryptexCreditor(
    OtiumStore store,
    IKryptexApi kryptex,
    GpuPolicyService policy,
    IMoneroPoolApi monero,
    IOptions<KryptexOptions> kryptexOptions,
    IOptions<EarningsOptions> earningsOptions,
    TimeProvider time,
    ILogger<KryptexCreditor> logger)
{
    /// <summary>The window behind a worker's "avg_hashrate_30m".</summary>
    public static readonly TimeSpan ShortWindow = TimeSpan.FromMinutes(30);

    /// <summary>The window behind "avg_hashrate_3h", used to cover a longer gap (for example a server restart).</summary>
    public static readonly TimeSpan LongWindow = TimeSpan.FromHours(3);

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

        var rates = new List<(string Coin, double UsdPerHash)>();
        foreach (var (algorithm, coin) in options.Coins)
        {
            // Work already done is paid even if the algorithm was just switched off, at the last known rate.
            if (current.For(algorithm) is { UsdPerHashPerSecondDay: > 0 } rate)
                rates.Add((coin.Coin, rate.UsdPerHashPerSecondDay / 86_400 * (1 - rate.PoolFeePercent / 100)));
        }
        if (options.Cpu.Coin.Length > 0 && await MoneroUsdPerHashAsync(options.Cpu.Coin, ct) is { } cpuRate)
            rates.Add((options.Cpu.Coin, cpuRate));

        foreach (var (coinSlug, usdPerHash) in rates)
        {
            var coin = new KryptexCoin { Coin = coinSlug };
            var prefix = $"kx:{coin.Coin}:";
            IReadOnlyList<KryptexWorker> listed;
            try
            {
                listed = await kryptex.GetWorkersAsync(coin.Coin, account, ct);
            }
            catch (HttpRequestException e)
            {
                logger.LogWarning(e, "Couldn't list Kryptex workers for {Coin}; will retry next run", coin.Coin);
                continue;
            }

            foreach (var worker in listed)
            {
                var owner = store.Resolve(store.FindAccountByWorker(worker.Name));
                if (owner is not { IsBalanceAccount: true })
                    continue; // not one of ours (or an own-wallet account, which can't use Kryptex)

                var key = prefix + worker.Name;
                var lastMs = progress.TryGetValue(key, out var seen) ? seen : (long?)null;
                var hashes = HashesSince(worker, lastMs is { } ms ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : null, now, options.CreditInterval);
                var nowMs = now.ToUnixTimeMilliseconds();
                store.SetWorkerProgress(key, nowMs, now);
                if (hashes <= 0)
                    continue;

                var (user, referral) = EarningsCreditor.Credit(store, earningsOptions.Value, owner, hashes * usdPerHash,
                    $"{key}@{nowMs}", now);
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
    /// Monero's value per hash after Kryptex's pool fee, from the same network data MoneroOcean crediting uses. Null when
    /// it can't be priced right now; the work is credited on a later run instead.
    /// </summary>
    private async Task<double?> MoneroUsdPerHashAsync(string coin, CancellationToken ct)
    {
        try
        {
            var network = await monero.GetNetworkStatsAsync(ct);
            if (network.Difficulty <= 0 || network.PriceUsd <= 0)
                return null;
            double fee;
            try
            {
                fee = (await kryptex.GetPoolInfoAsync(coin, ct)).FeePercent;
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                fee = DefaultCpuFeePercent;
            }
            return network.BlockRewardXmr / network.Difficulty * network.PriceUsd * (1 - fee / 100);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogWarning(e, "Monero network stats unavailable; Kryptex CPU work will be credited next run");
            return null;
        }
    }

    /// <summary>Kryptex's XMR pool fee (PPS+, October 2026), used when its pool info can't be read.</summary>
    public const double DefaultCpuFeePercent = 1.0;

    /// <summary>
    /// Hashes a worker did since the previous run, from its 30-minute average. Kryptex doesn't let account owners read
    /// per-worker history (the chart endpoint returns 403 for "krx…" accounts, October 2026), but its public worker list
    /// carries the 30-minute average. Sampled every <paramref name="interval"/>, average × time since the last sample
    /// adds up to the worker's real total: every hash sits in three consecutive 30-minute windows at a third each.
    /// A worker seen for the first time is credited one interval; a gap longer than 30 minutes (the server was down)
    /// uses the 3-hour average, for at most 3 hours.
    /// </summary>
    internal static double HashesSince(KryptexWorker worker, DateTimeOffset? last, DateTimeOffset now, TimeSpan interval)
    {
        var elapsed = last is { } l ? now - l : interval;
        if (elapsed <= TimeSpan.Zero)
            return 0;
        if (elapsed <= ShortWindow)
            return Math.Max(0, worker.Hashrate) * elapsed.TotalSeconds;
        var longGap = elapsed < LongWindow ? elapsed : LongWindow;
        return Math.Max(0, worker.Average3h ?? worker.Hashrate) * longGap.TotalSeconds;
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

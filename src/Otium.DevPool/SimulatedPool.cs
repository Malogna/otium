using System.Collections.Concurrent;

namespace Otium.DevPool;

/// <summary>What a simulated miner reports every few seconds.</summary>
/// <param name="Rig">Identifies one miner connection; several rigs (CPU, GPU, other PCs) can share a worker name.</param>
/// <param name="Hashrate">Raw hashrate in the miner's algorithm ("hs").</param>
/// <param name="NormalizedHashrate">XMR-equivalent hashrate ("hs2"), which is what earns.</param>
public sealed record MinerReport(string Wallet, string Worker, string Algo, double Hashrate, double NormalizedHashrate, string? Rig = null);

public sealed record ChartPoint(long Ts, double Hs, double Hs2);

/// <summary>
/// An in-memory imitation of MoneroOcean: real network numbers, but block difficulty divided by
/// <see cref="Speedup"/> so a simulated PC earns visibly in minutes instead of weeks. Like MoneroOcean it sums every
/// connection that uses the same worker name and writes one history point per worker every two minutes.
/// </summary>
public sealed class SimulatedPool(TimeProvider time, double speedup)
{
    /// <summary>Mainnet values from 2026-10-15.</summary>
    public const double RealDifficulty = 692_159_707_951;
    public const double BlockRewardXmr = 0.60893896;
    public const double PriceUsd = 544.59;

    /// <summary>MoneroOcean writes a worker history point about every two minutes.</summary>
    public static readonly TimeSpan PointSpacing = TimeSpan.FromMinutes(2);

    /// <summary>A rig that hasn't reported for this long is offline.</summary>
    public static readonly TimeSpan RigTimeout = TimeSpan.FromSeconds(30);

    private readonly ConcurrentDictionary<string, WalletState> _wallets = new(StringComparer.Ordinal);

    public double Speedup { get; } = speedup;

    public double Difficulty => RealDifficulty / Speedup;

    /// <summary>XMR earned per day for each 1 H/s of XMR-equivalent hashrate.</summary>
    public double XmrPerHashPerSecondDay => 86_400 * BlockRewardXmr / Difficulty;

    public void Report(MinerReport report)
    {
        var now = time.GetUtcNow();
        var wallet = _wallets.GetOrAdd(report.Wallet, _ => new WalletState());
        lock (wallet)
        {
            var worker = wallet.Workers.GetValueOrDefault(report.Worker) ?? (wallet.Workers[report.Worker] = new WorkerState());
            var rigKey = report.Rig ?? report.Algo;
            var rig = worker.Rigs.GetValueOrDefault(rigKey) ?? (worker.Rigs[rigKey] = new RigState());

            // The pool owes the wallet for this rig's work since its previous report.
            if (rig.LastReport is { } last && now - last <= RigTimeout)
            {
                var seconds = (now - last).TotalSeconds;
                wallet.EarnedAtomic += (long)(report.NormalizedHashrate * seconds / Difficulty * BlockRewardXmr * 1e12);
                worker.HashSeconds += report.Hashrate * seconds;
                worker.NormalizedHashSeconds += report.NormalizedHashrate * seconds;
            }
            rig.LastReport = now;
            rig.Hs = report.Hashrate;
            rig.Hs2 = report.NormalizedHashrate;
            worker.LastAlgo = report.Algo;

            // Like MoneroOcean: every two minutes the worker gets one history point holding its average rate over
            // that window (all of its rigs together). Nothing is written until the first window is complete.
            var live = worker.Rigs.Values.Where(r => now - r.LastReport <= RigTimeout).ToList();
            worker.Current = new ChartPoint(now.ToUnixTimeMilliseconds(), live.Sum(r => r.Hs), live.Sum(r => r.Hs2));
            worker.WindowStart ??= now;
            var window = (now - worker.WindowStart.Value).TotalSeconds;
            if (window >= PointSpacing.TotalSeconds)
            {
                worker.Points.Add(new ChartPoint(now.ToUnixTimeMilliseconds(), worker.HashSeconds / window, worker.NormalizedHashSeconds / window));
                worker.WindowStart = now;
                worker.HashSeconds = 0;
                worker.NormalizedHashSeconds = 0;
                var cutoff = now.ToUnixTimeMilliseconds() - (long)TimeSpan.FromHours(72).TotalMilliseconds;
                worker.Points.RemoveAll(p => p.Ts < cutoff);
            }
        }
    }

    public IReadOnlyDictionary<string, IReadOnlyList<ChartPoint>> WorkerCharts(string wallet)
    {
        if (!_wallets.TryGetValue(wallet, out var state))
            return new Dictionary<string, IReadOnlyList<ChartPoint>>();
        lock (state)
            return state.Workers.ToDictionary(w => w.Key, w => (IReadOnlyList<ChartPoint>)w.Value.Points.ToList());
    }

    public object MinerStats(string wallet)
    {
        var now = time.GetUtcNow();
        if (!_wallets.TryGetValue(wallet, out var state))
            return new { hash = 0, hash2 = 0, identifier = "global", totalHashes = 0, amtPaid = 0, amtDue = 0, txnCount = 0 };
        lock (state)
        {
            var rigs = state.Workers.Values.SelectMany(w => w.Rigs.Values).Where(r => now - r.LastReport <= RigTimeout).ToList();
            return new
            {
                hash = rigs.Sum(r => r.Hs),
                hash2 = rigs.Sum(r => r.Hs2),
                identifier = "global",
                totalHashes = 0,
                amtPaid = 0,
                amtDue = state.EarnedAtomic,
                txnCount = 0,
            };
        }
    }

    public object Summary() => new
    {
        speedup = Speedup,
        wallets = _wallets.Select(w => new
        {
            wallet = w.Key,
            earnedXmr = w.Value.EarnedAtomic / 1e12,
            workers = w.Value.Workers.Select(x => new { worker = x.Key, rigs = x.Value.Rigs.Keys, points = x.Value.Points.Count, current = x.Value.Current }),
        }),
    };

    private sealed class WalletState
    {
        public Dictionary<string, WorkerState> Workers { get; } = new(StringComparer.Ordinal);
        public long EarnedAtomic { get; set; }
    }

    private sealed class WorkerState
    {
        public List<ChartPoint> Points { get; } = [];
        public Dictionary<string, RigState> Rigs { get; } = new(StringComparer.Ordinal);
        public ChartPoint? Current { get; set; }
        public DateTimeOffset? WindowStart { get; set; }
        public double HashSeconds { get; set; }
        public double NormalizedHashSeconds { get; set; }
        public string LastAlgo { get; set; } = "rx/0";
    }

    private sealed class RigState
    {
        public DateTimeOffset LastReport { get; set; }
        public double Hs { get; set; }
        public double Hs2 { get; set; }
    }
}

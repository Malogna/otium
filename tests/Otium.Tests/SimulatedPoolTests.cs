using Otium.DevPool;

namespace Otium.Tests;

/// <summary>The simulated pool must behave like MoneroOcean, or dev-mode crediting would mislead us.</summary>
public class SimulatedPoolTests
{
    private const string Wallet = "wallet";
    private readonly ManualTime _time = new(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.Zero));

    private void ReportFor(SimulatedPool pool, TimeSpan duration, params (string Rig, double Hs2)[] rigs)
    {
        var end = _time.GetUtcNow() + duration;
        while (_time.GetUtcNow() < end)
        {
            foreach (var (rig, hs2) in rigs)
                pool.Report(new MinerReport(Wallet, "otworker", "rx/0", hs2, hs2, rig));
            _time.Advance(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public void Writes_one_point_per_two_minutes_holding_the_window_average()
    {
        var pool = new SimulatedPool(_time, speedup: 1);
        ReportFor(pool, TimeSpan.FromSeconds(110), ("cpu", 1000));
        Assert.Empty(pool.WorkerCharts(Wallet)["otworker"]); // first window not finished yet

        ReportFor(pool, TimeSpan.FromMinutes(4), ("cpu", 1000));
        var points = pool.WorkerCharts(Wallet)["otworker"];
        Assert.Equal(2, points.Count);
        Assert.All(points, p => Assert.InRange(p.Hs2, 950, 1000));
        Assert.Equal(120_000, points[1].Ts - points[0].Ts, tolerance: 5_000);
    }

    [Fact]
    public void Rigs_sharing_a_worker_name_add_up()
    {
        var pool = new SimulatedPool(_time, speedup: 1);
        ReportFor(pool, TimeSpan.FromMinutes(5), ("pc1-cpu", 1000), ("pc1-gpu", 3000), ("pc2-cpu", 500));
        var last = pool.WorkerCharts(Wallet)["otworker"][^1];
        Assert.InRange(last.Hs2, 4300, 4500);
    }

    [Fact]
    public void Speedup_makes_each_hash_worth_proportionally_more()
    {
        Assert.Equal(1000 * new SimulatedPool(_time, 1).XmrPerHashPerSecondDay, new SimulatedPool(_time, 1000).XmrPerHashPerSecondDay, precision: 9);
    }
}

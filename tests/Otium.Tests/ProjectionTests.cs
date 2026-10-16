using Otium.Core.Gpu;
using Otium.Core.Projections;

namespace Otium.Tests;

public class ProjectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Projection_scales_the_net_rate_by_hours_per_day()
    {
        var p = Projection.Create(2.40, 0.48, 12, onlyWhenIdle: true, towardsCashout: 0, cashoutMinimum: 1);

        Assert.True(p.HoursAreMeasured);
        Assert.Equal(0.96, p.PerDay, 6);
        Assert.Equal(0.96 * 7, p.PerWeek, 6);
        Assert.Equal(0.96 * 30, p.PerMonth, 6);
        Assert.NotNull(p.TimeToCashout);
        Assert.Equal(1 / 0.96, p.TimeToCashout.Value.TotalDays, 3);
    }

    [Fact]
    public void Projection_defaults_to_typical_hours_until_measured()
    {
        Assert.Equal(8, Projection.Create(1, 0, null, onlyWhenIdle: true, 0, 1).HoursPerDay);
        Assert.Equal(16, Projection.Create(1, 0, null, onlyWhenIdle: false, 0, 1).HoursPerDay);
        Assert.False(Projection.Create(1, 0, null, true, 0, 1).HoursAreMeasured);
    }

    [Fact]
    public void Projection_never_goes_negative_and_has_no_eta_when_unprofitable()
    {
        var p = Projection.Create(0.10, 0.50, 10, true, 0.2m, 1);

        Assert.Equal(0, p.PerMonth);
        Assert.Null(p.TimeToCashout);
    }

    [Fact]
    public void Projection_reports_zero_eta_once_the_minimum_is_reached()
    {
        Assert.Equal(TimeSpan.Zero, Projection.Create(0, 0, null, true, 2m, 1m).TimeToCashout);
    }

    [Fact]
    public void History_adds_only_the_growth_of_cumulative_totals()
    {
        var h = new EarningsHistory();
        h.Record(Now, 5.00, 1.00, TimeSpan.Zero);                         // baseline, nothing counted
        h.Record(Now.AddMinutes(1), 5.02, 1.01, TimeSpan.FromMinutes(1));
        h.Record(Now.AddMinutes(2), 5.05, 1.02, TimeSpan.FromMinutes(1));

        var bucket = Assert.Single(h.Hours);
        Assert.Equal(0.05, bucket.EarnedUsd, 6);
        Assert.Equal(0.02, bucket.ElectricityUsd, 6);
        Assert.Equal(120, bucket.EarningSeconds);
    }

    [Fact]
    public void History_ignores_a_reset_of_the_running_totals()
    {
        var h = new EarningsHistory();
        h.Record(Now, 5.00, 1.00, TimeSpan.Zero);
        h.Record(Now.AddMinutes(1), 0.01, 0.00, TimeSpan.FromMinutes(1)); // app restarted, totals start over
        h.Record(Now.AddMinutes(2), 0.03, 0.00, TimeSpan.FromMinutes(1));

        Assert.Equal(0.02, h.Hours.Sum(b => b.EarnedUsd), 6);
    }

    [Fact]
    public void Last_hours_lines_up_oldest_first_with_zeros_for_gaps()
    {
        var h = new EarningsHistory();
        h.Record(Now.AddHours(-3), 0, 0, TimeSpan.Zero);
        h.Record(Now.AddHours(-3).AddMinutes(5), 0.10, 0, TimeSpan.FromMinutes(5));
        h.Record(Now.AddMinutes(-1), 0.30, 0, TimeSpan.FromMinutes(5));

        var hours = h.LastHours(Now, 6);

        Assert.Equal(6, hours.Count);
        Assert.Equal(new[] { 0, 0, 0.10, 0, 0, 0.20 }, hours.Select(v => Math.Round(v, 6)));
    }

    [Fact]
    public void Average_hours_per_day_needs_a_full_day_then_uses_measured_time()
    {
        var h = new EarningsHistory();
        h.Record(Now, 0, 0, TimeSpan.Zero);
        h.Record(Now.AddHours(1), 0, 0, TimeSpan.FromHours(1));
        Assert.Null(h.AverageEarningHoursPerDay(Now.AddHours(2)));

        for (var i = 2; i <= 6; i++)
            h.Record(Now.AddHours(i), 0, 0, TimeSpan.FromHours(1));
        // 6 hours earned over 2 days of history.
        Assert.Equal(3, h.AverageEarningHoursPerDay(Now.AddDays(2))!.Value, 3);
    }

    [Fact]
    public void History_drops_hours_older_than_the_retention()
    {
        var h = new EarningsHistory();
        h.Record(Now, 0, 0, TimeSpan.Zero);
        h.Record(Now + EarningsHistory.Retention + TimeSpan.FromHours(2), 0, 0, TimeSpan.Zero);

        Assert.Single(h.Hours);
    }

    [Theory]
    [InlineData("NVIDIA GeForce RTX 4070 Ti", 42e6)]
    [InlineData("NVIDIA GeForce RTX 4070", 33e6)]
    [InlineData("AMD Radeon RX 7900 XTX", 45e6)]
    [InlineData("NVIDIA GeForce RTX 3060 Ti", 26e6)]
    public void Known_gpus_use_their_typical_hashrate(string name, double hps)
    {
        Assert.Equal(hps, HardwareEstimates.Gpu(new GpuInfo(GpuVendor.Nvidia, 0, name, 12)).Hps);
    }

    [Fact]
    public void Unknown_gpus_get_a_modest_default_by_memory()
    {
        Assert.Equal(20e6, HardwareEstimates.Gpu(new GpuInfo(GpuVendor.Amd, 0, "Mystery Card", 16)).Hps);
        Assert.Equal(12e6, HardwareEstimates.Gpu(new GpuInfo(GpuVendor.Amd, 0, "Mystery Card", 4)).Hps);
    }

    [Theory]
    [InlineData("rx/0", "Monero")]
    [InlineData("kawpow (switched from etchash)", "Ravencoin")]
    [InlineData("simulated autolykos2", "Ergo")]
    [InlineData(null, "crypto")]
    public void Algorithms_map_to_the_coin_people_recognise(string? algorithm, string coin)
    {
        Assert.Equal(coin, Coins.ForAlgorithm(algorithm).Name);
    }
}

using Otium.Core.Pools;
using Otium.Core.Power;

namespace Otium.Tests;

public class ProfitAndPowerTests
{
    // Network snapshot taken from MoneroOcean on 2026-10-15.
    private static readonly MoneroNetworkStats Network = new(692_159_707_951, 0.60893896, 544.59, DateTimeOffset.UnixEpoch);

    [Fact]
    public void Ten_kilohashes_earn_about_forty_cents_a_day_at_current_network_stats()
    {
        var xmr = MoneroProfit.XmrPerDay(10_000, Network);
        Assert.InRange(xmr, 0.00075, 0.00077);
        Assert.InRange(xmr * Network.PriceUsd, 0.40, 0.43);
    }

    [Fact]
    public void Pool_fee_reduces_revenue_proportionally()
    {
        var gross = MoneroProfit.XmrPerDay(10_000, Network);
        Assert.Equal(gross * 0.99, MoneroProfit.XmrPerDay(10_000, Network, poolFeePercent: 1), precision: 12);
    }

    [Fact]
    public void Zero_hashrate_or_difficulty_earns_nothing()
    {
        Assert.Equal(0, MoneroProfit.XmrPerDay(0, Network));
        Assert.Equal(0, MoneroProfit.XmrPerDay(10_000, Network with { Difficulty = 0 }));
    }

    [Fact]
    public void Electricity_cost_per_day()
    {
        // 100 W for 24 h = 2.4 kWh; at $0.15 = $0.36.
        Assert.Equal(0.36, Electricity.CostPerDay(100, 0.15), precision: 10);
        Assert.Equal(0.05, Electricity.KilowattHours(100, TimeSpan.FromMinutes(30)), precision: 10);
    }

    [Fact]
    public void Cpu_power_model_scales_extra_watts_with_usage()
    {
        var model = new CpuPowerModel(FullLoadWatts: 110, IdleWatts: 10);
        Assert.Equal(50, model.ExtraWatts(50), precision: 10);
        Assert.Equal(100, model.ExtraWatts(100), precision: 10);
        Assert.Equal(100, model.ExtraWatts(150), precision: 10); // clamped
    }

    [Fact]
    public void Power_model_honours_user_override()
    {
        Assert.Equal(180, CpuPowerModel.ForThisMachine(fullLoadOverride: 180).FullLoadWatts);
        Assert.InRange(CpuPowerModel.ForThisMachine().FullLoadWatts, 5, 250);
    }
}

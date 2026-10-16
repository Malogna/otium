using Otium.Core.Gpu;
using Otium.Core.Pools;
using Otium.Core.Power;
using Otium.Core.Providers;

namespace Otium.Core.Projections;

public sealed record DeviceEstimate(DeviceKind Device, string Name, string Coin, double Hashrate, double GrossUsdPerDay, double Watts);

public sealed record EarningsEstimate(IReadOnlyList<DeviceEstimate> Devices, double GrossUsdPerDay, double ElectricityUsdPerDay)
{
    public double NetUsdPerDay => GrossUsdPerDay - ElectricityUsdPerDay;
}

/// <summary>
/// Estimates what a computer can earn per day of mining, from its hardware and today's pool payouts. Used before
/// the first run (onboarding) and whenever there's no live measurement yet.
/// </summary>
public sealed class EarningsEstimator(IMoneroPoolApi pool)
{
    public async Task<EarningsEstimate> EstimateAsync(
        string cpuName,
        int cpuThreads,
        int cpuUsagePercent,
        CpuPowerModel cpuPower,
        IReadOnlyList<GpuInfo> gpus,
        double pricePerKwh,
        double feePercent,
        CancellationToken ct)
    {
        var network = await pool.GetNetworkStatsAsync(ct);
        var keep = 1 - feePercent / 100.0;
        var devices = new List<DeviceEstimate>();

        // CPU: RandomX, which is what the pool's base rate is quoted in.
        var cpuHps = HardwareEstimates.CpuHashrate(cpuThreads, cpuUsagePercent);
        var cpuUsd = MoneroProfit.XmrPerDay(cpuHps, network) * network.PriceUsd * keep;
        devices.Add(new DeviceEstimate(DeviceKind.Cpu, cpuName, "Monero", cpuHps, cpuUsd, cpuPower.ExtraWatts(cpuUsagePercent)));

        // GPUs: KawPow is a safe, widely paid baseline; the real miner then switches to whatever pays more.
        if (gpus.Count > 0)
        {
            var profits = await pool.GetAlgoProfitsAsync(ct);
            var kawpowPerHash = profits.For("kawpow"); // XMR per day per H/s
            foreach (var gpu in gpus.Where(g => g.Vendor is GpuVendor.Nvidia or GpuVendor.Amd))
            {
                var (hps, watts) = HardwareEstimates.Gpu(gpu);
                var usd = hps * kawpowPerHash * profits.XmrPriceUsd * keep;
                devices.Add(new DeviceEstimate(DeviceKind.Gpu, gpu.Name, "Ravencoin", hps, usd, watts));
            }
        }

        var electricity = devices.Sum(d => Electricity.CostPerDay(d.Watts, pricePerKwh));
        return new EarningsEstimate(devices, devices.Sum(d => d.GrossUsdPerDay), electricity);
    }
}

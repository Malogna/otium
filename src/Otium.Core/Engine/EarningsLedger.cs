using Otium.Core.Providers;

namespace Otium.Core.Engine;

/// <summary>Running totals. Revenue figures are estimates; the pool's own balance is the source of truth.</summary>
public sealed class EarningsLedger
{
    /// <summary>Total time spent mining, across all sessions. Drives the fee cycle position.</summary>
    public TimeSpan MiningTime { get; set; }

    public double EstimatedUserUsd { get; set; }

    public double EstimatedReferrerUsd { get; set; }

    public double EstimatedAppFeeUsd { get; set; }

    public double EnergyKwh { get; set; }

    public double ElectricityCostUsd { get; set; }

    /// <summary>Last steady hashrate, used to re-check profitability without having to mine.</summary>
    public double LastHashrateHps { get; set; }

    public DateOnly Day { get; set; }

    public double TodayUserUsd { get; set; }

    public double TodayElectricityUsd { get; set; }

    public void Record(PayoutRole role, double revenueUsd, double kwh, double costUsd, TimeSpan elapsed, DateOnly today)
    {
        if (Day != today)
        {
            Day = today;
            TodayUserUsd = 0;
            TodayElectricityUsd = 0;
        }

        MiningTime += elapsed;
        EnergyKwh += kwh;
        ElectricityCostUsd += costUsd;
        TodayElectricityUsd += costUsd;

        switch (role)
        {
            case PayoutRole.User:
                EstimatedUserUsd += revenueUsd;
                TodayUserUsd += revenueUsd;
                break;
            case PayoutRole.Referrer:
                EstimatedReferrerUsd += revenueUsd;
                break;
            case PayoutRole.App:
                EstimatedAppFeeUsd += revenueUsd;
                break;
        }
    }
}

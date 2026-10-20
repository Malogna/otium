using Otium.Core.Providers;

namespace Otium.Core.Engine;

/// <summary>Running totals. Revenue figures are estimates; the pool's own balance is the source of truth.</summary>
public sealed class EarningsLedger
{
    /// <summary>
    /// Total time spent mining, across all sessions. Drives the fee cycle position, so it is never negative: a negative
    /// value edited into ledger.json would otherwise keep the position before Otium's slice for ever. It only grows
    /// (see <see cref="Record"/> and <see cref="RaiseMiningTimeTo"/>).
    /// </summary>
    public TimeSpan MiningTime
    {
        get => _miningTime;
        set => _miningTime = value < TimeSpan.Zero ? TimeSpan.Zero : value;
    }

    private TimeSpan _miningTime;

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

    /// <summary>
    /// Raises <see cref="MiningTime"/> to <paramref name="floor"/> when it's below it (never lowers it): the mining time
    /// this computer already reported to Otium's server, so a deleted or edited ledger doesn't skip the fee slice.
    /// </summary>
    /// <returns>Whether it changed.</returns>
    public bool RaiseMiningTimeTo(TimeSpan floor)
    {
        if (floor <= MiningTime)
            return false;
        MiningTime = floor;
        return true;
    }

    public void Record(PayoutRole role, double revenueUsd, double kwh, double costUsd, TimeSpan elapsed, DateOnly today)
    {
        if (Day != today)
        {
            Day = today;
            TodayUserUsd = 0;
            TodayElectricityUsd = 0;
        }

        if (elapsed > TimeSpan.Zero)
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

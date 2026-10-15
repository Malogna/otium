namespace Otium.Core.Pools;

public static class MoneroProfit
{
    /// <summary>
    /// Expected XMR per day for a hashrate: share of network work (hashrate * seconds / difficulty) times block reward.
    /// </summary>
    public static double XmrPerDay(double hashrateHps, MoneroNetworkStats network, double poolFeePercent = 0)
    {
        if (hashrateHps <= 0 || network.Difficulty <= 0)
            return 0;
        var gross = hashrateHps * 86_400 * network.BlockRewardXmr / network.Difficulty;
        return gross * (1 - poolFeePercent / 100.0);
    }
}

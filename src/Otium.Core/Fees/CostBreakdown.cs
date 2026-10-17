using System.Globalization;
using Otium.Core.Pools;

namespace Otium.Core.Fees;

/// <summary>One party that takes a cut, and what it takes, in plain words.</summary>
public sealed record CostLine(string Who, string What);

/// <summary>
/// Everything that comes off a user's mining, in one place: Otium's fee, the mining software's developer fees, the
/// pool's fee and the total, plus what unMineable pays Otium for its referral code. Shown on the welcome screen and in
/// Settings, so nothing is hidden in a spread or small print.
/// </summary>
public static class CostBreakdown
{
    /// <summary>XMRig's developer donation (its minimum).</summary>
    public const double CpuMinerPercent = 1.0;

    /// <summary>SRBMiner's developer fees on the GPU algorithms each pool is used for (its README, October 2026).</summary>
    public static (double Min, double Max) GpuMinerPercent(PayoutPool pool) =>
        pool == PayoutPool.Unmineable ? (0.85, 2.0) : (0.65, 0.85);

    /// <summary>The pool's own fee: unMineable 0.75% with a referral code (1% without), MoneroOcean none.</summary>
    public static double PoolPercent(PayoutPool pool, bool referralCode) =>
        pool == PayoutPool.Unmineable ? referralCode ? 0.75 : 1.0 : 0;

    public static IReadOnlyList<CostLine> For(FeePolicy fees, PayoutPool pool, bool hasGpu, bool referralCode)
    {
        var minutes = fees.CycleLength.TotalMinutes * fees.FeePercent / 100;
        var lines = new List<CostLine>
        {
            new("Otium", $"{P(fees.FeePercent)} of mining time, about {minutes:0} minutes per day of mining, mined to Otium's own address. " +
                         "Nothing while Otium is paused, and nothing during fee-free days."),
        };

        var (gpuMin, gpuMax) = GpuMinerPercent(pool);
        lines.Add(new("Mining software", hasGpu
            ? $"XMRig keeps {P(CpuMinerPercent)} (processor); SRBMiner keeps {P(gpuMin)}–{P(gpuMax)} (graphics card, depending on the coin)."
            : $"XMRig keeps {P(CpuMinerPercent)}."));

        var poolFee = PoolPercent(pool, referralCode);
        lines.Add(new("Pool", pool == PayoutPool.Unmineable
            ? referralCode
                ? $"unMineable keeps {P(poolFee)} (1% without Otium's referral code)."
                : $"unMineable keeps {P(poolFee)}."
            : "MoneroOcean keeps nothing; each payout pays a tiny Monero network fee."));

        var cpuTotal = fees.FeePercent + CpuMinerPercent + poolFee;
        lines.Add(new("In total", hasGpu
            ? $"about {P(cpuTotal)} on the processor and {P(fees.FeePercent + gpuMin + poolFee)}–{P(fees.FeePercent + gpuMax + poolFee)} on the graphics card."
            : $"about {P(cpuTotal)}."));

        if (pool == PayoutPool.Unmineable && referralCode)
            lines.Add(new("Also", "unMineable pays Otium 0.25–0.5% of what you mine, out of its own fee. It doesn't come out of your " +
                                  "earnings: Otium's referral code is what lowers your pool fee from 1% to 0.75%."));
        return lines;
    }

    /// <summary>The lines as text, one per line ("Otium: …").</summary>
    public static string Text(IEnumerable<CostLine> lines) => string.Join("\n", lines.Select(l => $"{l.Who}: {l.What}"));

    private static string P(double percent) => percent.ToString("0.##", CultureInfo.InvariantCulture) + "%";
}

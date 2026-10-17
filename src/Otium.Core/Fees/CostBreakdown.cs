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

    /// <param name="cpuPool">The pool that pays the processor's mining.</param>
    /// <param name="gpuPool">The pool that pays the graphics card's mining, or null without one (Monero users outside the
    /// US mine their graphics card on unMineable, paid in XMR).</param>
    public static IReadOnlyList<CostLine> For(FeePolicy fees, PayoutPool cpuPool, PayoutPool? gpuPool, bool referralCode)
    {
        var minutes = fees.CycleLength.TotalMinutes * fees.FeePercent / 100;
        var lines = new List<CostLine>
        {
            new("Otium", $"{P(fees.FeePercent)} of mining time, about {minutes:0} minutes per day of mining, mined to Otium's own address. " +
                         "Nothing while Otium is paused, and nothing during fee-free days."),
        };

        if (gpuPool is { } gpu)
        {
            var (gpuMin, gpuMax) = GpuMinerPercent(gpu);
            lines.Add(new("Mining software",
                $"XMRig keeps {P(CpuMinerPercent)} (processor); SRBMiner keeps {P(gpuMin)}–{P(gpuMax)} (graphics card, depending on the coin)."));
        }
        else
        {
            lines.Add(new("Mining software", $"XMRig keeps {P(CpuMinerPercent)}."));
        }

        lines.Add(new("Pool", gpuPool is { } g && g != cpuPool
            ? $"{PoolText(cpuPool, referralCode, "processor")} {PoolText(g, referralCode, "graphics card")}"
            : PoolText(cpuPool, referralCode, null)));

        var cpuTotal = fees.FeePercent + CpuMinerPercent + PoolPercent(cpuPool, referralCode);
        if (gpuPool is { } pool)
        {
            var (gpuMin, gpuMax) = GpuMinerPercent(pool);
            var gpuFee = fees.FeePercent + PoolPercent(pool, referralCode);
            lines.Add(new("In total", $"about {P(cpuTotal)} on the processor and {P(gpuFee + gpuMin)}–{P(gpuFee + gpuMax)} on the graphics card."));
        }
        else
        {
            lines.Add(new("In total", $"about {P(cpuTotal)}."));
        }

        if (referralCode && (cpuPool == PayoutPool.Unmineable || gpuPool == PayoutPool.Unmineable))
            lines.Add(new("Also", "unMineable pays Otium 0.25–0.5% of what you mine there, out of its own fee. It doesn't come out of " +
                                  "your earnings: Otium's referral code is what lowers your pool fee from 1% to 0.75%."));
        return lines;
    }

    private static string PoolText(PayoutPool pool, bool referralCode, string? device)
    {
        var on = device is null ? "" : $" on the {device}";
        return pool == PayoutPool.Unmineable
            ? referralCode
                ? $"unMineable keeps {P(PoolPercent(pool, true))}{on} (1% without Otium's referral code)."
                : $"unMineable keeps {P(PoolPercent(pool, false))}{on}."
            : $"MoneroOcean keeps nothing{on}; each payout pays a tiny Monero network fee.";
    }

    /// <summary>The lines as text, one per line ("Otium: …").</summary>
    public static string Text(IEnumerable<CostLine> lines) => string.Join("\n", lines.Select(l => $"{l.Who}: {l.What}"));

    private static string P(double percent) => percent.ToString("0.##", CultureInfo.InvariantCulture) + "%";
}

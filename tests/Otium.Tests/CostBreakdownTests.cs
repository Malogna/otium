using Otium.Core.Fees;
using Otium.Core.Pools;

namespace Otium.Tests;

/// <summary>The cost stack users see: every cut, a total, and what unMineable pays Otium.</summary>
public class CostBreakdownTests
{
    private static string Line(IReadOnlyList<CostLine> lines, string who) => Assert.Single(lines, l => l.Who == who).What;

    [Fact]
    public void Unmineable_users_see_every_cut_and_the_referral_disclosure()
    {
        var lines = CostBreakdown.For(FeePolicy.Default, PayoutPool.Unmineable, PayoutPool.Unmineable, referralCode: true);

        Assert.Contains("2% of mining time, about 29 minutes per day", Line(lines, "Otium"));
        Assert.Contains("XMRig keeps 1%", Line(lines, "Mining software"));
        Assert.Contains("SRBMiner keeps 0.85%–2%", Line(lines, "Mining software"));
        Assert.Contains("unMineable keeps 0.75% (1% without Otium's referral code)", Line(lines, "Pool"));
        // 2 + 1 + 0.75 on the processor; 2 + 0.85..2 + 0.75 on the graphics card.
        Assert.Equal("about 3.75% on the processor and 3.6%–4.75% on the graphics card.", Line(lines, "In total"));
        Assert.Contains("unMineable pays Otium 0.25–0.5% of what you mine there, out of its own fee", Line(lines, "Also"));
    }

    [Fact]
    public void Monero_users_on_moneroocean_pay_no_pool_fee_and_see_no_referral_note()
    {
        var lines = CostBreakdown.For(FeePolicy.Default, PayoutPool.MoneroOcean, gpuPool: null, referralCode: true);

        Assert.Contains("MoneroOcean keeps nothing", Line(lines, "Pool"));
        Assert.Equal("about 3%.", Line(lines, "In total"));
        Assert.DoesNotContain(lines, l => l.Who == "Also");
    }

    [Fact]
    public void Monero_users_with_a_graphics_card_see_both_pools()
    {
        var lines = CostBreakdown.For(FeePolicy.Default, PayoutPool.MoneroOcean, PayoutPool.Unmineable, referralCode: true);

        Assert.Equal("MoneroOcean keeps nothing on the processor; each payout pays a tiny Monero network fee. " +
                     "unMineable keeps 0.75% on the graphics card (1% without Otium's referral code).", Line(lines, "Pool"));
        Assert.Equal("about 3% on the processor and 3.6%–4.75% on the graphics card.", Line(lines, "In total"));
        Assert.Contains("of what you mine there", Line(lines, "Also"));
    }

    [Fact]
    public void A_different_fee_from_the_server_shows_up_everywhere()
    {
        var fees = FeePolicy.FromTerms(new Otium.Core.Accounts.FeeTerms(3, 0.25, 180, 30, 30, 90));
        var lines = CostBreakdown.For(fees, PayoutPool.Unmineable, gpuPool: null, referralCode: false);

        Assert.Contains("3% of mining time, about 43 minutes per day", Line(lines, "Otium"));
        Assert.Contains("unMineable keeps 1%.", Line(lines, "Pool"));
        Assert.Equal("about 5%.", Line(lines, "In total"));
        Assert.StartsWith("Otium: 3% of mining time", CostBreakdown.Text(lines));
    }
}

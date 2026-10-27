using Otium.Core.Providers;

namespace Otium.Tests;

/// <summary>"You've been paid" shows once per payout: when the pool's paid total grows past what the user last closed.</summary>
public class PayoutNoticeTests
{
    private static PayoutBalance Paid(double paid, string coin = "USDT") => new(0.2, paid, coin, 1.5, null);

    [Fact]
    public void The_first_payout_shows_in_full()
    {
        Assert.Equal((1.5, 1.5), PayoutNotice.ToShow(Paid(1.5), null, null));
        Assert.Null(PayoutNotice.ToShow(Paid(0), null, null)); // nothing paid yet
        Assert.Null(PayoutNotice.ToShow(null, null, null)); // balance not read yet
    }

    [Fact]
    public void Each_later_payout_shows_what_is_new()
    {
        Assert.Null(PayoutNotice.ToShow(Paid(1.5), 1.5, "USDT")); // closed after this one
        var next = PayoutNotice.ToShow(Paid(3.0), 1.5, "USDT")!.Value;
        Assert.Equal(1.5, next.NewlyPaid, 9);
        Assert.Equal(3.0, next.Total, 9);
    }

    [Fact]
    public void A_new_payout_coin_counts_from_zero()
    {
        Assert.Equal((0.02, 0.02), PayoutNotice.ToShow(Paid(0.02, "LTC"), 3.0, "USDT"));
    }
}

using Otium.Core.Fees;
using Otium.Core.Providers;

namespace Otium.Tests;

public class FeeScheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly string User = TestAddresses.Generate();
    private static readonly string App = TestAddresses.Generate();
    private static readonly string Referrer = TestAddresses.Generate();
    private static readonly FeePolicy Policy = FeePolicy.Default; // 2% fee, half to referrer, 3h cycle

    private static ReferralStanding NoReferral => new(null, null, null);

    [Fact]
    public void Without_referrer_the_last_two_percent_of_each_cycle_goes_to_the_app()
    {
        var schedule = new FeeSchedule(Policy, User, App, NoReferral);
        var cycle = Policy.CycleLength;
        var fee = cycle * 0.02;

        Assert.Equal(PayoutRole.User, schedule.RoleAt(TimeSpan.Zero, Now));
        Assert.Equal(PayoutRole.User, schedule.RoleAt(cycle - fee - TimeSpan.FromSeconds(1), Now));
        Assert.Equal(PayoutRole.App, schedule.RoleAt(cycle - fee, Now));
        Assert.Equal(PayoutRole.App, schedule.RoleAt(cycle - TimeSpan.FromSeconds(1), Now));
        Assert.Equal(PayoutRole.User, schedule.RoleAt(cycle, Now)); // next cycle
        Assert.Equal(2.0, schedule.EffectiveFeePercent(Now), precision: 6);
        Assert.Equal(App, schedule.TargetAt(cycle - TimeSpan.FromSeconds(1), Now).WalletAddress);
    }

    [Fact]
    public void Active_referrer_gets_half_of_the_fee_slice()
    {
        var schedule = new FeeSchedule(Policy, User, App, new ReferralStanding(Referrer, Now.AddDays(-40), null));
        var cycle = Policy.CycleLength;
        var half = cycle * 0.01;

        Assert.Equal(PayoutRole.Referrer, schedule.RoleAt(cycle - 2 * half, Now));
        Assert.Equal(Referrer, schedule.TargetAt(cycle - 2 * half, Now).WalletAddress);
        Assert.Equal(PayoutRole.App, schedule.RoleAt(cycle - half, Now));
        Assert.Equal(2.0, schedule.EffectiveFeePercent(Now), precision: 6);
    }

    [Fact]
    public void Referrer_reward_expires_after_the_reward_period()
    {
        var schedule = new FeeSchedule(Policy, User, App, new ReferralStanding(Referrer, Now - Policy.ReferrerRewardPeriod - TimeSpan.FromDays(1), null));
        Assert.False(schedule.HasActiveReferrer(Now));
        Assert.Equal(PayoutRole.App, schedule.RoleAt(Policy.CycleLength - TimeSpan.FromSeconds(1), Now));
    }

    [Fact]
    public void Self_referral_is_ignored()
    {
        var schedule = new FeeSchedule(Policy, User, App, new ReferralStanding(User, Now, null));
        Assert.False(schedule.HasActiveReferrer(Now));
    }

    [Fact]
    public void Fee_free_period_mines_everything_for_the_user()
    {
        var schedule = new FeeSchedule(Policy, User, App, new ReferralStanding(Referrer, Now, Now.AddDays(30)));
        Assert.True(schedule.IsFeeFree(Now));
        Assert.Equal(0, schedule.EffectiveFeePercent(Now));
        Assert.Equal(PayoutRole.User, schedule.RoleAt(Policy.CycleLength - TimeSpan.FromSeconds(1), Now));

        // Once it lapses, fees resume.
        Assert.Equal(PayoutRole.App, schedule.RoleAt(Policy.CycleLength - TimeSpan.FromSeconds(1), Now.AddDays(31)));
    }

    [Fact]
    public void Without_app_wallet_the_app_share_stays_with_the_user_but_referrer_still_earns()
    {
        var noReferrer = new FeeSchedule(Policy, User, appWallet: null, NoReferral);
        Assert.Equal(0, noReferrer.EffectiveFeePercent(Now));
        Assert.Equal(PayoutRole.User, noReferrer.RoleAt(Policy.CycleLength - TimeSpan.FromSeconds(1), Now));

        var withReferrer = new FeeSchedule(Policy, User, appWallet: null, new ReferralStanding(Referrer, Now, null));
        Assert.Equal(Policy.ReferrerPercent, withReferrer.EffectiveFeePercent(Now), precision: 6);
        Assert.Equal(PayoutRole.Referrer, withReferrer.RoleAt(Policy.CycleLength - TimeSpan.FromSeconds(1), Now));
    }

    [Fact]
    public void Referrers_get_a_quarter_of_the_fee_for_six_months_by_default()
    {
        Assert.Equal(2.0, FeePolicy.Default.FeePercent);
        Assert.Equal(0.25, FeePolicy.Default.ReferrerShareOfFee);
        Assert.Equal(0.5, FeePolicy.Default.ReferrerPercent, precision: 6);
        Assert.Equal(TimeSpan.FromDays(180), FeePolicy.Default.ReferrerRewardPeriod);

        var schedule = new FeeSchedule(Policy, User, App, new ReferralStanding(Referrer, Now, null));
        Assert.True(schedule.HasActiveReferrer(Now.AddDays(179)));
        Assert.False(schedule.HasActiveReferrer(Now.AddDays(181)));
    }

    [Fact]
    public void Server_terms_apply_but_are_clamped()
    {
        var terms = new Otium.Core.Accounts.FeeTerms(3.5, 0.2, 90, 14, 7, 60);
        var policy = FeePolicy.FromTerms(terms);
        Assert.Equal(3.5, policy.FeePercent);
        Assert.Equal(0.7, policy.ReferrerPercent, precision: 6);
        Assert.Equal(TimeSpan.FromDays(90), policy.ReferrerRewardPeriod);
        Assert.Equal(TimeSpan.FromDays(14), policy.RefereeFeeHoliday);
        Assert.Equal(terms, policy.ToTerms());

        // A misconfigured or hostile server can't push the fee past 10%, nor send more than 1% of a user's mining time
        // to the referrer's address (which, unlike Otium's own fee address, comes from the server).
        var silly = FeePolicy.FromTerms(new Otium.Core.Accounts.FeeTerms(50, 3, -1, 9999, 0, 0));
        Assert.Equal(FeePolicy.MaxFeePercent, silly.FeePercent);
        Assert.Equal(FeePolicy.MaxReferrerPercent, silly.ReferrerPercent, precision: 6);
        Assert.Equal(0.1, silly.ReferrerShareOfFee, precision: 6);
        Assert.Equal(0.5, FeePolicy.FromTerms(new Otium.Core.Accounts.FeeTerms(2, 0.25, 180, 30, 30, 90)).ReferrerPercent, precision: 6); // today's terms
        Assert.Equal(1, FeePolicy.FromTerms(new Otium.Core.Accounts.FeeTerms(2, 1, 180, 30, 30, 90)).ReferrerPercent, precision: 6);
        Assert.Equal(TimeSpan.Zero, silly.ReferrerRewardPeriod);
        Assert.Equal(TimeSpan.FromDays(365), silly.RefereeFeeHoliday);

        Assert.Same(FeePolicy.Default, FeePolicy.FromTerms(null));
    }

    [Fact]
    public void Time_until_next_switch_points_at_the_next_boundary()
    {
        var schedule = new FeeSchedule(Policy, User, App, NoReferral);
        var cycle = Policy.CycleLength;
        var fee = cycle * 0.02;

        Assert.Equal(cycle - fee, schedule.TimeUntilNextSwitch(TimeSpan.Zero, Now));
        Assert.Equal(fee, schedule.TimeUntilNextSwitch(cycle - fee, Now));
    }
}

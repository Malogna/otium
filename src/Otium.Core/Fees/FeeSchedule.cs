using Otium.Core.Providers;

namespace Otium.Core.Fees;

/// <summary>Everything the schedule needs to know about the current user's referral standing.</summary>
public sealed record ReferralStanding(
    string? ReferrerWallet,
    DateTimeOffset? ReferredAt,
    DateTimeOffset? FeeFreeUntil);

/// <summary>
/// Decides whose wallet the hardware mines for at a given point in accumulated mining time.
/// Each cycle is laid out as [user ... | referrer slice | app slice].
/// </summary>
/// <param name="userQuantus">The user's Quantus target, mined by the graphics card when Quantus pays best; null without one.</param>
/// <param name="appQuantus">
/// Otium's Quantus target for its slice. Without one (or in the referrer's slice: referrers register only their main
/// address), Quantus mined during the slice stays with the user.
/// </param>
public sealed class FeeSchedule(
    FeePolicy policy,
    string userWallet,
    string? appWallet,
    ReferralStanding standing,
    string? userQuantus = null,
    string? appQuantus = null)
{
    public FeePolicy Policy { get; } = policy;

    public MiningTarget TargetAt(TimeSpan totalMiningTime, DateTimeOffset now)
    {
        var role = RoleAt(totalMiningTime, now);
        var quantus = userQuantus is null ? null : role == PayoutRole.App && appQuantus is not null ? appQuantus : userQuantus;
        return new MiningTarget(WalletFor(role), role, quantus);
    }

    public PayoutRole RoleAt(TimeSpan totalMiningTime, DateTimeOffset now)
    {
        var (referrerSlice, appSlice) = Slices(now);
        var cycle = Policy.CycleLength;
        var position = TimeSpan.FromTicks(totalMiningTime.Ticks % cycle.Ticks);
        var userEnd = cycle - referrerSlice - appSlice;

        if (position < userEnd)
            return PayoutRole.User;
        if (position < userEnd + referrerSlice)
            return PayoutRole.Referrer;
        return PayoutRole.App;
    }

    /// <summary>Mining time left until the payout role changes.</summary>
    public TimeSpan TimeUntilNextSwitch(TimeSpan totalMiningTime, DateTimeOffset now)
    {
        var (referrerSlice, appSlice) = Slices(now);
        var cycle = Policy.CycleLength;
        var position = TimeSpan.FromTicks(totalMiningTime.Ticks % cycle.Ticks);
        var userEnd = cycle - referrerSlice - appSlice;
        var referrerEnd = userEnd + referrerSlice;

        var boundaries = new[] { userEnd, referrerEnd, cycle };
        foreach (var boundary in boundaries)
        {
            if (position < boundary)
                return boundary - position;
        }
        return cycle - position;
    }

    /// <summary>Effective fee percent right now, for display.</summary>
    public double EffectiveFeePercent(DateTimeOffset now)
    {
        var (referrerSlice, appSlice) = Slices(now);
        return (referrerSlice + appSlice) / Policy.CycleLength * 100.0;
    }

    public bool IsFeeFree(DateTimeOffset now) => standing.FeeFreeUntil is { } until && now < until;

    public bool HasActiveReferrer(DateTimeOffset now) =>
        !string.IsNullOrWhiteSpace(standing.ReferrerWallet)
        && !Wallets.PayoutTarget.SameAs(standing.ReferrerWallet, userWallet)
        && standing.ReferredAt is { } referredAt
        && now < referredAt + Policy.ReferrerRewardPeriod;

    private (TimeSpan Referrer, TimeSpan App) Slices(DateTimeOffset now)
    {
        if (IsFeeFree(now))
            return (TimeSpan.Zero, TimeSpan.Zero);

        var fee = Policy.CycleLength * (Policy.FeePercent / 100.0);
        var referrer = HasActiveReferrer(now) ? fee * Policy.ReferrerShareOfFee : TimeSpan.Zero;
        // Without a configured app wallet the app's share simply stays with the user.
        var app = appWallet is null ? TimeSpan.Zero : fee - referrer;
        return (referrer, app);
    }

    private string WalletFor(PayoutRole role) => role switch
    {
        PayoutRole.Referrer => standing.ReferrerWallet!,
        PayoutRole.App => appWallet!,
        _ => userWallet,
    };
}

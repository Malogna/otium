namespace Otium.Core.Fees;

/// <summary>
/// The disclosed app fee and how referral rewards are carved out of it.
/// Rewards are always a share of a fee that was actually mined, so a referral can never pay out
/// more than the referred user really contributed. That keeps fake sign-ups worthless.
/// </summary>
public sealed record FeePolicy
{
    public static readonly FeePolicy Default = new();

    /// <summary>Share of mining time that goes to fees (app + referrer), in percent.</summary>
    public double FeePercent { get; init; } = 2.0;

    /// <summary>Fraction of the fee that is mined for the person who invited the user.</summary>
    public double ReferrerShareOfFee { get; init; } = 0.25;

    /// <summary>How long a referrer keeps earning from someone they invited.</summary>
    public TimeSpan ReferrerRewardPeriod { get; init; } = TimeSpan.FromDays(180);

    /// <summary>Fee-free period granted to a new user who joins with a referral code.</summary>
    public TimeSpan RefereeFeeHoliday { get; init; } = TimeSpan.FromDays(30);

    /// <summary>Fee-free period granted to a referrer for each invited friend who is verified by the pool.</summary>
    public TimeSpan ReferrerFeeHolidayPerVerifiedFriend { get; init; } = TimeSpan.FromDays(30);

    /// <summary>Upper bound on stacked fee-free time, so heavy self-referral can only ever buy a bounded discount.</summary>
    public TimeSpan MaxFeeHoliday { get; init; } = TimeSpan.FromDays(90);

    /// <summary>
    /// Length of one fee cycle in mining time. The fee is mined as one contiguous slice at the end of each cycle,
    /// which keeps engine restarts rare (two per cycle) and lets new users mine for themselves first. A day makes
    /// the 2% slice about 29 minutes, long enough that a pool's share difficulty settles and little is lost to
    /// the miner restarting.
    /// </summary>
    public TimeSpan CycleLength { get; init; } = TimeSpan.FromHours(24);

    /// <summary>Most the server may set the fee to; anything above is ignored by the app.</summary>
    public const double MaxFeePercent = 10;

    /// <summary>
    /// The terms the server announces, or <see cref="Default"/> when it hasn't. Out-of-range values are clamped, so a
    /// misconfigured server can't make the app mine an unreasonable fee.
    /// </summary>
    public static FeePolicy FromTerms(Accounts.FeeTerms? terms) => terms is null ? Default : new FeePolicy
    {
        FeePercent = Math.Clamp(terms.FeePercent, 0, MaxFeePercent),
        ReferrerShareOfFee = Math.Clamp(terms.ReferrerShareOfFee, 0, 1),
        ReferrerRewardPeriod = TimeSpan.FromDays(Math.Clamp(terms.ReferrerRewardDays, 0, 730)),
        RefereeFeeHoliday = TimeSpan.FromDays(Math.Clamp(terms.RefereeFeeFreeDays, 0, 365)),
        ReferrerFeeHolidayPerVerifiedFriend = TimeSpan.FromDays(Math.Clamp(terms.ReferrerFeeFreeDaysPerFriend, 0, 365)),
        MaxFeeHoliday = TimeSpan.FromDays(Math.Clamp(terms.MaxFeeFreeDays, 0, 365)),
    };

    public Accounts.FeeTerms ToTerms() => new(
        FeePercent,
        ReferrerShareOfFee,
        ReferrerRewardPeriod.TotalDays,
        RefereeFeeHoliday.TotalDays,
        ReferrerFeeHolidayPerVerifiedFriend.TotalDays,
        MaxFeeHoliday.TotalDays);

    /// <summary>The referrer's part of a referred friend's mining time, in percent (e.g. 0.5 for a quarter of 2%).</summary>
    public double ReferrerPercent => FeePercent * ReferrerShareOfFee;
}

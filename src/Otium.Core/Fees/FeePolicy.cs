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
    public double ReferrerShareOfFee { get; init; } = 0.5;

    /// <summary>How long a referrer keeps earning from someone they invited.</summary>
    public TimeSpan ReferrerRewardPeriod { get; init; } = TimeSpan.FromDays(365);

    /// <summary>Fee-free period granted to a new user who joins with a referral code.</summary>
    public TimeSpan RefereeFeeHoliday { get; init; } = TimeSpan.FromDays(30);

    /// <summary>Fee-free period granted to a referrer for each invited friend who is verified by the pool.</summary>
    public TimeSpan ReferrerFeeHolidayPerVerifiedFriend { get; init; } = TimeSpan.FromDays(30);

    /// <summary>Upper bound on stacked fee-free time, so heavy self-referral can only ever buy a bounded discount.</summary>
    public TimeSpan MaxFeeHoliday { get; init; } = TimeSpan.FromDays(90);

    /// <summary>
    /// Length of one fee cycle in mining time. The fee is mined as one contiguous slice at the end of each cycle,
    /// which keeps engine restarts rare (two per cycle) and lets new users mine for themselves first.
    /// </summary>
    public TimeSpan CycleLength { get; init; } = TimeSpan.FromHours(3);
}

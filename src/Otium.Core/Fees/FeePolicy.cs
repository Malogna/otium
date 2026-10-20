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
    /// Most of a user's mining time the server may give to the person who invited them, in percent (today a quarter of
    /// the 2% fee: 0.5%). The fee itself goes to Otium's address, built into the app, but the referrer's address comes
    /// from the server, so this caps what a compromised server could send to an address of its choosing.
    /// </summary>
    public const double MaxReferrerPercent = 1;

    /// <summary>
    /// The terms the server announces, or <see cref="Default"/> when it hasn't. Out-of-range values are clamped, so a
    /// misconfigured server can't make the app mine an unreasonable fee.
    /// </summary>
    public static FeePolicy FromTerms(Accounts.FeeTerms? terms)
    {
        if (terms is null)
            return Default;
        var fee = Math.Clamp(terms.FeePercent, 0, MaxFeePercent);
        return new FeePolicy
        {
            FeePercent = fee,
            ReferrerShareOfFee = Math.Clamp(terms.ReferrerShareOfFee, 0, fee > 0 ? Math.Min(1, MaxReferrerPercent / fee) : 1),
            ReferrerRewardPeriod = TimeSpan.FromDays(Math.Clamp(terms.ReferrerRewardDays, 0, 730)),
            RefereeFeeHoliday = TimeSpan.FromDays(Math.Clamp(terms.RefereeFeeFreeDays, 0, 365)),
            ReferrerFeeHolidayPerVerifiedFriend = TimeSpan.FromDays(Math.Clamp(terms.ReferrerFeeFreeDaysPerFriend, 0, 365)),
            MaxFeeHoliday = TimeSpan.FromDays(Math.Clamp(terms.MaxFeeFreeDays, 0, 365)),
        };
    }

    /// <summary>
    /// These terms within what this build was released with: the fee no lower than the built-in one, and fee-free time
    /// and the inviter's share no more generous. Release builds apply it to whatever the server sends, so lowering the
    /// fee takes a new release, not a server or a settings file.
    /// </summary>
    public FeePolicy WithinBuiltInLimits() => this with
    {
        FeePercent = Math.Max(FeePercent, Default.FeePercent),
        ReferrerShareOfFee = Math.Min(ReferrerShareOfFee, Default.ReferrerShareOfFee),
        ReferrerRewardPeriod = Min(ReferrerRewardPeriod, Default.ReferrerRewardPeriod),
        RefereeFeeHoliday = Min(RefereeFeeHoliday, Default.RefereeFeeHoliday),
        ReferrerFeeHolidayPerVerifiedFriend = Min(ReferrerFeeHolidayPerVerifiedFriend, Default.ReferrerFeeHolidayPerVerifiedFriend),
        MaxFeeHoliday = Min(MaxFeeHoliday, Default.MaxFeeHoliday),
    };

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

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

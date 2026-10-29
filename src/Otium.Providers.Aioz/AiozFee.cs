using Otium.Core.Fees;

namespace Otium.Providers.Aioz;

/// <summary>
/// Otium's share of what this node earns, kept in the node until it's sent to Otium's address. It builds up on new
/// earnings only: <see cref="ChargedThrough"/> is how far the node's lifetime earnings have been counted, so what was
/// earned before Otium started keeping a share (the first reading after the update) stays the user's in full.
/// </summary>
/// <param name="ChargedThrough">The node's lifetime earnings (AIOZ) already counted for the fee.</param>
/// <param name="Owed">Otium's share still in the node, not yet sent to Otium's address.</param>
/// <param name="Kept">All of Otium's share since it started, sent or not (the user's earnings are shown without it).</param>
public sealed record AiozFee(decimal ChargedThrough, decimal Owed = 0, decimal Kept = 0)
{
    /// <summary>Otium's share is sent once this much has built up (the node withdraws whole AIOZ only).</summary>
    public const decimal SendAtAioz = 1;

    /// <summary>Starts counting from the node's current earnings: everything before is fee-free.</summary>
    public static AiozFee StartAt(decimal totalEarned) => new(totalEarned);

    /// <summary>Adds Otium's share of what the node earned since the last reading (none while there's no fee address).</summary>
    public AiozFee Accrue(decimal totalEarned, double feePercent)
    {
        if (totalEarned <= ChargedThrough)
            return this;
        var fee = (totalEarned - ChargedThrough) * (decimal)Math.Clamp(feePercent, 0, FeePolicy.MaxFeePercent) / 100;
        return new AiozFee(totalEarned, Owed + fee, Kept + fee);
    }

    /// <summary>After <paramref name="amount"/> of Otium's share reached Otium's address.</summary>
    public AiozFee Sent(decimal amount) => this with { Owed = Math.Max(0, Owed - amount) };
}

/// <summary>One withdrawal from the node: whole AIOZ to an address, either the user's or Otium's.</summary>
public sealed record AiozWithdrawal(string Address, decimal Amount, bool ToOtium);

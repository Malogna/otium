namespace Otium.Core.Providers;

/// <summary>
/// When the overview says "You've been paid": the pool's paid-out total grew past what the user last closed the card
/// at (any payout the first time). Payouts come rarely, so each one is a moment to share with an invite.
/// </summary>
public static class PayoutNotice
{
    /// <param name="seenPaid">The paid-out total when the user last closed the card; null before the first one.</param>
    /// <param name="seenCoin">The coin that total was in: a new payout coin starts counting again.</param>
    /// <returns>What's new since then and the total paid, or null when there's nothing new.</returns>
    public static (double NewlyPaid, double Total)? ToShow(PayoutBalance? balance, double? seenPaid, string? seenCoin)
    {
        if (balance is not { Paid: > 0 } b)
            return null;
        var seen = string.Equals(seenCoin, b.Coin, StringComparison.OrdinalIgnoreCase) ? seenPaid ?? 0 : 0;
        return b.Paid > seen + 1e-9 ? (b.Paid - seen, b.Paid) : null;
    }
}

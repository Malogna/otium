namespace Otium.Core.Projections;

/// <summary>
/// When to tell a user they're close to a payout. Most users stop at a few cents of a 1.5 USDT threshold, so one
/// notification at four fifths of it, once per payout, shows that earning pays. It rearms when the pool balance drops
/// below half the threshold (after the payout).
/// </summary>
public static class PayoutNudge
{
    public enum Action { None, Notify, Reset }

    /// <summary>The share of the threshold that counts as close.</summary>
    public const double CloseAt = 0.8;

    /// <summary>The share the balance must fall below before the next notification can come.</summary>
    public const double RearmBelow = 0.5;

    public static Action Check(double pending, double threshold, bool alreadyNotified)
    {
        if (threshold <= 0)
            return Action.None;
        var share = pending / threshold;
        if (alreadyNotified)
            return share < RearmBelow ? Action.Reset : Action.None;
        return share >= CloseAt - 1e-9 && share < 1 ? Action.Notify : Action.None;
    }
}

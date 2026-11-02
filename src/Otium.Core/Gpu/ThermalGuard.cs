namespace Otium.Core.Gpu;

/// <summary>
/// Protects a graphics card from running too hot while it mines, and a test from being judged on a card that was too hot to
/// run at its real speed. It follows the card's own readings (no administrator rights needed): above
/// <see cref="StopAtC"/> mining stops until the card is back under <see cref="ResumeAtC"/>. A fan reading of nearly nothing
/// while the card is warm and busy counts the same: a fan that stops under load (a broken fan curve, a dead fan) ends in the
/// same place a few minutes later, and Windows reports the fan percentage of a card whose fan is being driven by another tool
/// all over the place (100%, 64%, 0% within seconds on an RTX 3080 running MSI Afterburner, November 2026).
/// </summary>
public static class ThermalGuard
{
    /// <summary>Mining stops at this temperature (°C). Cards throttle themselves at about 83 and shut down well above 90.</summary>
    public const double StopAtC = 90;

    /// <summary>Mining starts again at or below this temperature (°C).</summary>
    public const double ResumeAtC = 80;

    /// <summary>A fan at or below this speed while the card is warm and busy means it isn't cooling the card.</summary>
    public const double StalledFanPercent = 2;

    /// <summary>"Warm" for the stalled-fan rule (°C).</summary>
    public const double WarmC = 82;

    /// <summary>A test counts as run too hot when this share of its readings (percent) were thermally throttled.</summary>
    public const double ThrottledTestShare = 30;

    /// <summary>The card is hot enough that mining should stop, and why in a few words; null when it is fine.</summary>
    public static string? StopReason(IReadOnlyList<GpuStats> cards)
    {
        foreach (var card in cards)
        {
            if (card.TemperatureC >= StopAtC)
                return FormattableString.Invariant($"{ShortName(card)} reached {card.TemperatureC:0} °C");
            if (card.FanPercent <= StalledFanPercent && card.TemperatureC >= WarmC && card.LoadPercent >= 50)
                return FormattableString.Invariant($"{ShortName(card)}'s fan isn't spinning while it is at {card.TemperatureC:0} °C");
        }
        return null;
    }

    /// <summary>Every card is back at or below <see cref="ResumeAtC"/> (a card whose temperature can't be read counts as cool).</summary>
    public static bool CooledDown(IReadOnlyList<GpuStats> cards) =>
        cards.All(c => (c.TemperatureC ?? 0) <= ResumeAtC);

    /// <summary>
    /// Whether a test's readings say the card was too hot to show its real speed: a large share of them thermally throttled,
    /// or the temperature reached <see cref="StopAtC"/> minus a little.
    /// </summary>
    public static bool TestWasThrottled(int readings, int throttledReadings, double? peakTemperatureC) =>
        readings >= 3 && (throttledReadings * 100.0 / readings >= ThrottledTestShare || peakTemperatureC >= StopAtC - 3);

    private static string ShortName(GpuStats card) =>
        string.IsNullOrWhiteSpace(card.Name) ? "The graphics card" : card.Name.Replace("NVIDIA ", "", StringComparison.OrdinalIgnoreCase).Replace("GeForce ", "", StringComparison.OrdinalIgnoreCase);
}

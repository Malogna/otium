namespace Otium.Core.Engine;

/// <summary>
/// What Otium is doing with a device besides plain earning, so the app can say so on the device's row instead of just
/// showing a lower number: it is measuring, trying a setting, or protecting the hardware.
/// </summary>
public static class EnginePhases
{
    /// <summary>Measuring each coin on the card (the graphics card's first minutes, and "Test GPU again").</summary>
    public const string Testing = "testing";

    /// <summary>Trying another miner setting for a while to see if it earns more (the speeds shown then say little).</summary>
    public const string Tuning = "tuning";

    /// <summary>Running below full speed because the card's fan or temperature is high.</summary>
    public const string EasingOff = "easing-off";

    /// <summary>Paused, or waiting to test, until the card has cooled down.</summary>
    public const string CoolingDown = "cooling-down";

    /// <summary>The short label for the badge on a device's row; null for none.</summary>
    public static string? Label(string? phase) => phase switch
    {
        Testing => "Testing",
        Tuning => "Tuning",
        EasingOff => "Easing off",
        CoolingDown => "Cooling down",
        _ => null,
    };

    /// <summary>One sentence about what the phase means for the numbers shown, for the row's details.</summary>
    public static string? Explain(string? phase) => phase switch
    {
        Testing => "Otium is measuring each coin on this card to pick the best one. It still earns while it tests, but the speed and earnings shown now aren't the final ones.",
        Tuning => "Otium is trying another setting for a while to see whether it earns more. The speed shown now is for that setting and may be lower or higher.",
        EasingOff => "The card is running below full speed because its fan or temperature was high.",
        CoolingDown => "Mining is paused until the card has cooled down, to protect it.",
        _ => null,
    };
}

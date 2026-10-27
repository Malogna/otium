namespace Otium.Core.Engine;

/// <summary>
/// What a computer's processor or graphics card is doing, as one word from a fixed list, sent with the app's regular
/// check-in so the admin dashboard can count where computers get stuck (paused for electricity, waiting for the user to
/// step away, blocked by antivirus…). Only counts are shown; nothing else about the computer is in it.
/// </summary>
public static class EngineStateTags
{
    public const string Earning = "earning";
    public const string WaitingForIdle = "waiting-for-idle";
    public const string PausedForElectricity = "paused-electricity";
    public const string Antivirus = "antivirus";
    public const string Error = "error";
    public const string Installing = "installing";
    public const string Starting = "starting";
    public const string SwitchedOff = "switched-off";
    public const string PausedByUser = "paused-by-user";
    public const string NeedsSetup = "needs-setup";
    public const string None = "none";

    /// <summary>Every tag the server accepts; anything else is dropped.</summary>
    public static readonly IReadOnlySet<string> Known = new HashSet<string>(StringComparer.Ordinal)
    {
        Earning, WaitingForIdle, PausedForElectricity, Antivirus, Error, Installing, Starting, SwitchedOff, PausedByUser,
        NeedsSetup, None,
    };

    /// <param name="snapshot">The engine's state; null when the computer has no such device (no usable graphics card).</param>
    /// <param name="enabled">The device's own switch in Settings.</param>
    /// <param name="earningOn">The main Start/Pause switch.</param>
    /// <param name="needsSetup">Mining can't start yet (no payout address, unsupported region…).</param>
    public static string For(EngineSnapshot? snapshot, bool enabled, bool earningOn, bool needsSetup) => snapshot switch
    {
        null => None,
        _ when !enabled => SwitchedOff,
        _ when !earningOn => PausedByUser,
        _ when needsSetup => NeedsSetup,
        { EngineBlocked: true } => Antivirus,
        { State: EngineState.Earning } => Earning,
        { State: EngineState.WaitingForIdle } => WaitingForIdle,
        { State: EngineState.PausedUnprofitable } => PausedForElectricity,
        { State: EngineState.Installing } => Installing,
        { State: EngineState.Error } => Error,
        _ => Starting,
    };
}

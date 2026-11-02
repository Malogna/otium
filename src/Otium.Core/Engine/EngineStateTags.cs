using System.Text.RegularExpressions;
using Otium.Core.Providers;

namespace Otium.Core.Engine;

/// <summary>
/// What a computer's processor or graphics card is doing, as one word from a fixed list, sent with the app's regular
/// check-in so the admin dashboard can count where computers get stuck (paused for electricity, waiting for the user to
/// step away, blocked by antivirus…). Only counts are shown; nothing else about the computer is in it.
/// </summary>
public static partial class EngineStateTags
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

    /// <summary>"error-memory", "error-failed"…: an error whose kind the provider named (<see cref="ProblemKinds"/>).</summary>
    public static string ErrorOf(string kind) => $"{Error}-{kind}";

    /// <summary>Every tag the server accepts; anything else is dropped.</summary>
    public static readonly IReadOnlySet<string> Known = new HashSet<string>(
        new[] { Earning, WaitingForIdle, PausedForElectricity, Antivirus, Error, Installing, Starting, SwitchedOff, PausedByUser, NeedsSetup, None }
            .Concat(ProblemKinds.All.Select(ErrorOf)),
        StringComparer.Ordinal);

    /// <summary>The longest reason sent with a check-in.</summary>
    public const int ReasonMaxLength = 120;

    /// <summary>
    /// Why a device is in an "error…" state, in a few words, sent with the check-in so the operator can see what went wrong
    /// without asking the user. Null for every other state.
    /// </summary>
    public static string? ReasonFor(EngineSnapshot? snapshot, string tag) =>
        snapshot is not null && tag.StartsWith(Error, StringComparison.Ordinal) ? Scrub(snapshot.Problem ?? snapshot.StatusText) : null;

    /// <summary>
    /// Makes an error message safe to send: file paths (they hold the user's name) become "&lt;path&gt;", whitespace is
    /// collapsed and the text is cut to <see cref="ReasonMaxLength"/>. Null when nothing is left.
    /// </summary>
    public static string? Scrub(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var clean = WhitespaceRun().Replace(PathLike().Replace(text, "<path>"), " ").Trim();
        if (clean.Length == 0)
            return null;
        return clean.Length <= ReasonMaxLength ? clean : clean[..(ReasonMaxLength - 1)] + "…";
    }

    // C:\Users\Anna Smith\x.dll (folders may hold spaces), \\server\share, /home/name/x, ~/x
    [GeneratedRegex(@"(?:(?:[A-Za-z]:[\\/]|\\\\)(?:[^\\/:*?""'<>|\r\n]+[\\/])*[^\\/:*?""'<>|\s]*|(?:~/|(?<![\w.])/(?=[\w.\-]+/))[^\s""'<>|]*)")]
    private static partial Regex PathLike();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();

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
        // A problem the provider named, whether it stopped the device or it keeps trying (a blocked pool).
        { State: EngineState.Error or EngineState.Starting, ProblemKind: { } kind } when Known.Contains(ErrorOf(kind)) => ErrorOf(kind),
        { State: EngineState.Earning } => Earning,
        { State: EngineState.WaitingForIdle } => WaitingForIdle,
        { State: EngineState.PausedUnprofitable } => PausedForElectricity,
        { State: EngineState.Installing } => Installing,
        { State: EngineState.Error } => Error,
        _ => Starting,
    };
}

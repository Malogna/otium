using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Otium.Core.Platform;

/// <summary>What the person at the computer is doing, as far as it matters for mining while they're there.</summary>
public enum ForegroundState
{
    /// <summary>The platform can't tell.</summary>
    Unknown,

    /// <summary>Ordinary work: browsing, documents, video in a window.</summary>
    Calm,

    /// <summary>A full-screen app, game (Direct3D full screen) or presentation: the graphics card is theirs.</summary>
    Busy,
}

public interface IForegroundMonitor
{
    ForegroundState Current();
}

public static class ForegroundMonitor
{
    public static IForegroundMonitor ForThisPlatform() =>
        OperatingSystem.IsWindows() ? new WindowsForegroundMonitor() : new UnknownForeground();

    private sealed class UnknownForeground : IForegroundMonitor
    {
        public ForegroundState Current() => ForegroundState.Unknown;
    }
}

/// <summary>
/// Windows' own answer to "is the user in a full-screen app or game right now?" (SHQueryUserNotificationState, the
/// signal Windows uses to hold back notifications during games and presentations).
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed partial class WindowsForegroundMonitor : IForegroundMonitor
{
    private const int NotPresent = 1;            // QUNS_NOT_PRESENT: screen saver or locked
    private const int Busy = 2;                  // QUNS_BUSY: a full-screen app
    private const int RunningD3DFullScreen = 3;  // QUNS_RUNNING_D3D_FULL_SCREEN: a game
    private const int PresentationMode = 4;      // QUNS_PRESENTATION_MODE
    private const int AcceptsNotifications = 5;  // QUNS_ACCEPTS_NOTIFICATIONS: ordinary desktop use
    private const int QuietTime = 6;             // QUNS_QUIET_TIME
    private const int App = 7;                   // QUNS_APP: a full-screen Store app

    [LibraryImport("shell32.dll")]
    private static partial int SHQueryUserNotificationState(out int state);

    public ForegroundState Current()
    {
        if (SHQueryUserNotificationState(out var state) != 0)
            return ForegroundState.Unknown;
        return state switch
        {
            AcceptsNotifications or QuietTime or NotPresent => ForegroundState.Calm,
            Busy or RunningD3DFullScreen or PresentationMode or App => ForegroundState.Busy,
            _ => ForegroundState.Unknown,
        };
    }
}

/// <summary>
/// "Earn while I browse" (opt-in) for the graphics card: while the user is at the computer doing ordinary things, the
/// card counts as free, so it mines; the moment a full-screen app or game starts, or the laptop runs on battery, it's the
/// user's again (the engine pauses it like when they come back). The processor isn't affected.
/// </summary>
public sealed class BrowsingIdleDetector(IIdleDetector inner, Func<bool> enabled, IForegroundMonitor foreground, Func<bool> onBattery) : IIdleDetector
{
    /// <summary>What counts as "away long enough" while browsing is allowed: well past any idle threshold.</summary>
    public static readonly TimeSpan AsIfAway = TimeSpan.FromDays(1);

    public TimeSpan? GetIdleTime()
    {
        var idle = inner.GetIdleTime();
        return enabled() && !onBattery() && foreground.Current() == ForegroundState.Calm ? AsIfAway : idle;
    }
}

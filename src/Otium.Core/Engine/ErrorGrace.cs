namespace Otium.Core.Engine;

/// <summary>
/// Keeps a one-off hiccup out of the "error…" state sent with the check-in. The engine restarts a stopped miner a minute
/// later, so a miner that Windows ended at shutdown or logoff, or that antivirus closed once, would otherwise show up in
/// the admin dashboard as a computer stuck on a problem. A device is reported as in error only when its errors have gone
/// on for <see cref="Grace"/>: it was seen in an error state, and again within <see cref="Gap"/> of the last time, since
/// the first.
/// </summary>
public sealed class ErrorGrace
{
    /// <summary>How long errors must go on before they're reported.</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(5);

    /// <summary>The longest time between two sightings of an error that still count as the same run of errors: a miner that
    /// keeps failing is in error for about a minute, then starting again, then in error again.</summary>
    public static readonly TimeSpan Gap = TimeSpan.FromMinutes(3);

    private readonly Dictionary<string, (DateTimeOffset Since, DateTimeOffset LastSeen)> _runs = new(StringComparer.Ordinal);

    /// <summary>Notes what the device is doing now; call it every minute or so.</summary>
    public void Observe(string device, string tag, DateTimeOffset now)
    {
        if (!IsError(tag))
            return;
        _runs[device] = _runs.TryGetValue(device, out var run) && now - run.LastSeen <= Gap
            ? (run.Since, now)
            : (now, now);
    }

    /// <summary>The tag to send: <paramref name="tag"/>, or "starting" for an error that hasn't lasted <see cref="Grace"/> yet.</summary>
    public string Report(string device, string tag, DateTimeOffset now) =>
        IsError(tag) && !(_runs.TryGetValue(device, out var run) && now - run.LastSeen <= Gap && now - run.Since >= Grace)
            ? EngineStateTags.Starting
            : tag;

    private static bool IsError(string tag) => tag.StartsWith(EngineStateTags.Error, StringComparison.Ordinal);
}

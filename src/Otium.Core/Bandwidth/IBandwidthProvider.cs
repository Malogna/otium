using Otium.Core.Providers;

namespace Otium.Core.Bandwidth;

/// <summary>
/// Shares the computer's unused internet bandwidth through a bandwidth partner's SDK, running inside Otium.
/// Like compute providers, implementations are compiled in and registered explicitly, never loaded from arbitrary files.
/// </summary>
public interface IBandwidthProvider : IAsyncDisposable
{
    /// <summary>Stable identifier, reported to the server with usage, e.g. "simulated".</summary>
    string Id { get; }

    string DisplayName { get; }

    /// <summary>
    /// Null when the provider can run here; otherwise a short user-facing reason (unsupported OS, or a build without
    /// the partner SDK).
    /// </summary>
    string? UnavailableReason { get; }

    bool IsInstalled { get; }

    /// <summary>Downloads and verifies the partner's component, if it has one. Safe to call when already installed.</summary>
    Task InstallAsync(IProgress<InstallProgress>? progress, CancellationToken ct);

    Task StartAsync(BandwidthRunOptions options, CancellationToken ct);

    Task StopAsync(CancellationToken ct);

    Task<BandwidthStatus> GetStatusAsync(CancellationToken ct);

    /// <summary>
    /// The user opted in through Otium's consent screen; tells the partner component, which won't share otherwise.
    /// </summary>
    Task OptInAsync(CancellationToken ct);

    /// <summary>The user switched sharing off: withdraws consent with the partner component as well.</summary>
    Task OptOutAsync(CancellationToken ct);

    /// <summary>
    /// For providers that pay the user directly: the user's share of everything shared so far, as last known (kept
    /// across restarts), so the earnings history never sees the total drop to zero while the provider starts. Null
    /// when the Otium server credits sharing instead.
    /// </summary>
    double? LastKnownUserUsd => null;
}

/// <param name="PublisherUserId">
/// Opaque id the partner attributes this computer's traffic to (the account's worker id), so the operator can
/// reconcile the partner's statements with what users were credited.
/// </param>
public sealed record BandwidthRunOptions(string PublisherUserId);

public enum BandwidthProviderState
{
    Stopped,
    Starting,

    /// <summary>Connected and available; traffic flows whenever the partner has demand.</summary>
    Online,
    Error,
}

/// <param name="BytesSinceStart">Traffic relayed since the last start; resets when the provider restarts.</param>
/// <param name="Earnings">
/// What the partner network reports this computer has earned, in its own words (e.g. "0.42 MYST, paid to 0x12…ab at 5
/// MYST"), for providers that pay the user directly; null when the Otium server credits sharing instead.
/// </param>
/// <param name="UserUsd">For providers that pay the user directly: the user's share of everything earned so far,
/// after Otium's fee (paid out or still waiting).</param>
/// <param name="UserUsdPerDay">The same, per day at the recent pace; null until there's enough history to tell.</param>
public sealed record BandwidthStatus(BandwidthProviderState State, long BytesSinceStart, string? Message = null, string? Earnings = null,
    double? UserUsd = null, double? UserUsdPerDay = null);

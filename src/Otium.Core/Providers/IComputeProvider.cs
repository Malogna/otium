namespace Otium.Core.Providers;

/// <summary>
/// A backend that turns local hardware into money (a miner, a rental-marketplace agent, an AI network node).
/// Providers are compiled into the app and registered explicitly; the app never loads provider code
/// from arbitrary DLLs, so a third party cannot slip a malicious "plugin" onto a user's machine.
/// </summary>
public interface IComputeProvider : IAsyncDisposable
{
    /// <summary>Stable identifier, e.g. "xmrig-cpu".</summary>
    string Id { get; }

    string DisplayName { get; }

    DeviceKind Device { get; }

    /// <summary>Coin or currency the provider pays out in, e.g. "XMR".</summary>
    string PayoutCurrency { get; }

    /// <summary>False when this OS / CPU architecture has no build of the provider.</summary>
    bool IsSupportedOnThisPlatform { get; }

    bool IsInstalled { get; }

    /// <summary>Downloads and verifies the provider's engine. Safe to call when already installed.</summary>
    Task InstallAsync(IProgress<InstallProgress>? progress, CancellationToken ct);

    Task StartAsync(ProviderRunOptions options, CancellationToken ct);

    Task StopAsync(CancellationToken ct);

    Task<ProviderStatus> GetStatusAsync(CancellationToken ct);

    /// <summary>Estimated gross revenue for a given hashrate, using live network data and prices.</summary>
    Task<RevenueEstimate> EstimateRevenueAsync(double hashrateHps, CancellationToken ct);

    /// <summary>
    /// Best expected outcome without running, or null when the provider has nothing measured yet
    /// (the engine then falls back to its last observed hashrate).
    /// </summary>
    Task<ProfitOffer?> GetExpectedOfferAsync(CancellationToken ct);

    /// <summary>Balance as reported by the third party that actually pays the user (pool, marketplace).</summary>
    Task<PayoutBalance?> GetPayoutBalanceAsync(string walletAddress, CancellationToken ct);

    /// <summary>Checks a payout address for this provider. Returns null when valid, otherwise a user-facing error.</summary>
    string? ValidatePayoutAddress(string address);
}

/// <summary>
/// A provider whose engine can stop hashing without exiting, keeping what it spent time preparing (XMRig's 2 GB
/// RandomX dataset, the pool connection). When the user comes back for a moment, the engine pauses it instead of
/// stopping it, so it earns again the moment they leave instead of after another warm-up.
/// </summary>
public interface IPausableProvider
{
    /// <summary>Stops hashing; the engine keeps running. Returns false when it couldn't (the engine then stops it).</summary>
    Task<bool> PauseAsync(CancellationToken ct);

    /// <summary>Hashing again after <see cref="PauseAsync"/>. Returns false when it couldn't (the engine then restarts it).</summary>
    Task<bool> ResumeAsync(CancellationToken ct);
}

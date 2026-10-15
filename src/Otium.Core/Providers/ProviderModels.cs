namespace Otium.Core.Providers;

public enum DeviceKind
{
    Cpu,
    Gpu,
}

public enum ProviderState
{
    NotInstalled,
    Ready,
    Starting,
    Running,
    Error,
}

/// <summary>Who the hashes are currently being mined for.</summary>
public enum PayoutRole
{
    User,
    Referrer,
    App,
}

public sealed record MiningTarget(string WalletAddress, PayoutRole Role);

/// <param name="UsagePercent">Share of the device to use (CPU threads for CPU providers; GPU providers may ignore it).</param>
public sealed record ProviderRunOptions(MiningTarget Target, string WorkerName, int UsagePercent);

/// <param name="Detail">Short provider-specific note for the UI, e.g. the algorithm being mined.</param>
/// <param name="IsCalibrating">
/// True while the provider is measuring itself (e.g. benchmarking algorithms). The engine doesn't judge
/// profitability or remember the hashrate during calibration, because the numbers aren't representative yet.
/// </param>
public sealed record ProviderStatus(
    ProviderState State,
    double HashrateHps,
    string? Message = null,
    string? Detail = null,
    bool IsCalibrating = false)
{
    public static ProviderStatus Stopped(bool installed) =>
        new(installed ? ProviderState.Ready : ProviderState.NotInstalled, 0);
}

/// <summary>
/// What a provider expects to earn without running, from earlier measurements. Lets the engine skip starting
/// when the best known option wouldn't cover electricity.
/// </summary>
/// <param name="ExtraWatts">Measured extra power for this option, if known; otherwise the engine uses its power meter.</param>
public sealed record ProfitOffer(double GrossUsdPerDay, double? ExtraWatts, string Description);

public sealed record InstallProgress(string Stage, double Fraction);

public sealed record RevenueEstimate(double CoinPerDay, double UsdPerDay, string Coin);

public sealed record PayoutBalance(double Pending, double Paid, string Coin, double MinimumPayout, Uri? DashboardUrl);

/// <summary>Thrown when antivirus software removes or blocks an engine binary.</summary>
public sealed class EngineBlockedException(string message, string enginePath) : Exception(message)
{
    public string EnginePath { get; } = enginePath;
}

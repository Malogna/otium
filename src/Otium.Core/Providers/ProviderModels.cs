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

/// <param name="WalletAddress">The payout target this slice mines to ("USDT:T…", or the operator's pool wallet).</param>
/// <param name="QuantusTarget">
/// Where Quantus mined by the graphics card goes during this slice ("QTC:qz…"), or null when the user hasn't added a
/// Quantus address (the graphics card then doesn't mine Quantus).
/// </param>
public sealed record MiningTarget(string WalletAddress, PayoutRole Role, string? QuantusTarget = null);

/// <param name="UsagePercent">Share of the device to use (CPU threads for CPU providers; GPU providers may ignore it).</param>
/// <param name="Pool">The server's pool route for this CPU, overriding the provider's default pool; null for own-wallet mining.</param>
public sealed record ProviderRunOptions(MiningTarget Target, string WorkerName, int UsagePercent, Accounts.CpuPoolRoute? Pool = null);

/// <param name="Detail">Short provider-specific note for the UI, e.g. the algorithm being mined.</param>
/// <param name="IsCalibrating">
/// True while the provider is measuring itself (e.g. benchmarking algorithms). The engine doesn't judge
/// profitability or remember the hashrate during calibration, because the numbers aren't representative yet.
/// </param>
/// <param name="Problem">
/// A problem in plain words that the app shows as is, e.g. "Graphics card isn't mining: …". With
/// <see cref="ProviderState.Error"/> the engine stops and retries later; while starting, it explains the wait.
/// </param>
public sealed record ProviderStatus(
    ProviderState State,
    double HashrateHps,
    string? Message = null,
    string? Detail = null,
    bool IsCalibrating = false,
    string? Problem = null)
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

/// <summary>What the pool holds for a payout address and how it pays it out.</summary>
/// <param name="Pending">Mined and not yet paid, in <paramref name="Coin"/>.</param>
/// <param name="MinimumPayout">The pool pays once <paramref name="Pending"/> reaches this.</param>
/// <param name="AutoPay">False when the pool won't pay by itself (unMineable's per-address "Auto pay" switch is off).</param>
/// <param name="Problems">Plain-language problems the pool reports for the address (failed payment, network paused…).</param>
/// <param name="PoolName">"unMineable" or "MoneroOcean", for the UI.</param>
public sealed record PayoutBalance(
    double Pending,
    double Paid,
    string Coin,
    double MinimumPayout,
    Uri? DashboardUrl,
    bool AutoPay = true,
    IReadOnlyList<string>? Problems = null,
    DateTimeOffset? LastPayment = null,
    string PoolName = "MoneroOcean");

/// <summary>
/// Thrown when a provider can't run for a reason it has already put in plain words (shown without "Something went
/// wrong"). The engine retries after a short wait.
/// </summary>
public sealed class ProviderProblemException(string message) : Exception(message);

/// <summary>Thrown when antivirus software removes or blocks an engine binary.</summary>
public sealed class EngineBlockedException(string message, string enginePath) : Exception(message)
{
    public string EnginePath { get; } = enginePath;
}

using Otium.Core.Fees;
using Otium.Core.Providers;

namespace Otium.Core.Engine;

public enum EngineState
{
    Off,
    Installing,
    Starting,
    Earning,
    WaitingForIdle,
    PausedUnprofitable,
    Error,
}

/// <summary>Per-tick configuration, re-read every tick so settings changes apply without a restart.</summary>
public sealed record EngineConfig(
    string PayoutAddress,
    int UsagePercent,
    bool OnlyWhenIdle,
    TimeSpan IdleThreshold,
    bool NeverRunAtLoss,
    double PricePerKwh,
    FeeSchedule FeeSchedule,
    string WorkerName,
    Accounts.CpuPoolRoute? CpuPool = null,
    bool PauseWhenBack = false,
    bool RunsWhileBrowsing = false,
    bool FullSpeedWhenAway = false);

public sealed record EngineSnapshot
{
    public EngineState State { get; init; }

    public string StatusText { get; init; } = "";

    public double HashrateHps { get; init; }

    /// <summary>Revenue the hardware generates per day before the app fee.</summary>
    public double GrossUsdPerDay { get; init; }

    /// <summary>The user's share per day after the fee.</summary>
    public double UserUsdPerDay { get; init; }

    public double ExtraWatts { get; init; }

    public bool PowerMeasured { get; init; }

    public string PowerSource { get; init; } = "";

    public double ElectricityUsdPerDay { get; init; }

    public double NetUsdPerDay => UserUsdPerDay - ElectricityUsdPerDay;

    public PayoutRole CurrentRole { get; init; }

    public double EffectiveFeePercent { get; init; }

    public InstallProgress? Install { get; init; }

    public bool EngineBlocked { get; init; }

    public string? EngineBlockedPath { get; init; }

    public TimeSpan? IdleTime { get; init; }

    /// <summary>Provider-specific detail, e.g. "kawpow" or "Testing algorithm 2 of 4".</summary>
    public string? Detail { get; init; }

    /// <summary>A problem the provider explained in plain words (e.g. "Graphics card isn't mining: …"), shown as is.</summary>
    public string? Problem { get; init; }

    /// <summary>The provider is still measuring itself (e.g. testing graphics card algorithms one by one).</summary>
    public bool IsCalibrating { get; init; }

    /// <summary>Earning while the user is at the computer, because they started the "measure my PC" test.</summary>
    public bool RunningNow { get; init; }

    public bool IdleDetectionAvailable { get; init; } = true;

    public double TotalUserUsd { get; init; }

    public double TotalElectricityUsd { get; init; }

    public double TotalEnergyKwh { get; init; }

    public double TodayUserUsd { get; init; }

    public double TodayElectricityUsd { get; init; }

    public TimeSpan TotalMiningTime { get; init; }
}

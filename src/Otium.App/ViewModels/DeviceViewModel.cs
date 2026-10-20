using CommunityToolkit.Mvvm.ComponentModel;
using Otium.App.Services;
using Otium.Core.Engine;
using Otium.Core.Providers;

namespace Otium.App.ViewModels;

/// <summary>One row on the dashboard: a device (CPU or GPU) and what its engine is doing.</summary>
public partial class DeviceViewModel(string title, string subtitle, EarningsEngine engine) : ViewModelBase
{
    public string Title { get; } = title;

    public string Subtitle { get; } = subtitle;

    public EarningsEngine Engine { get; } = engine;

    public EngineSnapshot Snapshot { get; private set; } = engine.Snapshot;

    [ObservableProperty] public partial string StateLabel { get; set; } = "Off";
    [ObservableProperty] public partial string StatusText { get; set; } = "";
    [ObservableProperty] public partial string Detail { get; set; } = "";
    [ObservableProperty] public partial bool IsEarning { get; set; }
    [ObservableProperty] public partial bool IsPaused { get; set; }
    [ObservableProperty] public partial bool IsProblem { get; set; }
    [ObservableProperty] public partial bool IsInstalling { get; set; }
    [ObservableProperty] public partial double InstallPercent { get; set; }
    [ObservableProperty] public partial string Hashrate { get; set; } = "";
    [ObservableProperty] public partial string EarnPerDay { get; set; } = "";
    [ObservableProperty] public partial string PowerText { get; set; } = "";

    /// <summary>Load, temperature and the like right now ("98% load · 62 °C · fan 54%…"), one line per card; empty if unknown.</summary>
    [ObservableProperty] public partial string Stats { get; set; } = "";

    public bool HasNumbers => Snapshot.State is EngineState.Earning or EngineState.PausedUnprofitable;

    public void Apply(EngineSnapshot s)
    {
        Snapshot = s;
        StatusText = s.State == EngineState.Off ? "" : s.StatusText;
        Detail = s.Detail ?? "";
        IsEarning = s.State is EngineState.Earning or EngineState.Starting;
        IsPaused = s.State is EngineState.WaitingForIdle or EngineState.PausedUnprofitable;
        IsProblem = s.State is EngineState.Error;
        StateLabel = Label(s.State);
        IsInstalling = s.State == EngineState.Installing;
        InstallPercent = (s.Install?.Fraction ?? 0) * 100;
        // A device that's off or waiting shows its state alone, not a column of dashes.
        Hashrate = s.HashrateHps > 0 ? Format.Hashrate(s.HashrateHps) : "";
        EarnPerDay = HasNumbers ? $"{Format.Usd(s.UserUsdPerDay)}/day" : "";
        PowerText = s.ExtraWatts > 0 ? $"+{Format.Watts(s.ExtraWatts)} {(s.PowerMeasured ? "measured" : "est.")}" : "";
        OnPropertyChanged(nameof(HasNumbers));
    }

    public static string Label(EngineState state) => state switch
    {
        EngineState.Earning => "Earning",
        EngineState.Starting => "Starting",
        EngineState.Installing => "Setting up",
        EngineState.WaitingForIdle => "Waiting",
        EngineState.PausedUnprofitable => "Paused",
        EngineState.Error => "Needs attention",
        _ => "Off",
    };

    public static string RoleText(PayoutRole role) => role switch
    {
        PayoutRole.Referrer => "the friend who invited you",
        PayoutRole.App => "Otium (fee)",
        _ => "you",
    };
}

using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Otium.App.Services;
using Otium.Core.Accounts;
using Otium.Core.Engine;
using Otium.Core.Projections;
using Otium.Core.Providers;

namespace Otium.App.ViewModels;

/// <summary>One device's line in the test result: "RTX 4090 · Pearl · $3.41 a day".</summary>
public sealed record MeasuredRow(string Name, string What, string PerDay);

/// <summary>
/// The results that show the user (and their friends) what the PC's idle time is worth, on Home: the two-minute
/// "measure my PC" test, "while you were away" after each stretch of earning, and last month's report early in a month.
/// Each can be copied as a message with the user's invite, or saved as a picture.
/// </summary>
public partial class ResultsViewModel : ViewModelBase
{
    private readonly AppHost _host;
    private bool _measureClosed;

    public ResultsViewModel(AppHost host)
    {
        _host = host;
        host.MeasurementChanged += () => Dispatcher.UIThread.Post(Refresh);
        host.AwayChanged += () => Dispatcher.UIThread.Post(Refresh);
        host.HistoryChanged += () => Dispatcher.UIThread.Post(Refresh);
        host.AccountChanged += () => Dispatcher.UIThread.Post(Refresh);
        Refresh();
    }

    // ---------- measure my PC ----------

    [ObservableProperty] public partial bool ShowMeasureOffer { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMeasureResultNow), nameof(ShowAwayNow), nameof(ShowMonthlyNow))]
    public partial bool IsMeasuring { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMeasureResultNow), nameof(ShowAwayNow), nameof(ShowMonthlyNow))]
    public partial bool ShowMeasureResult { get; set; }
    [ObservableProperty] public partial double MeasureProgress { get; set; }
    [ObservableProperty] public partial string MeasureStatus { get; set; } = "";
    [ObservableProperty] public partial string MeasureAmount { get; set; } = "";
    [ObservableProperty] public partial string MeasureDetail { get; set; } = "";

    /// <summary>Why the processor was left off after the first test, when it was.</summary>
    [ObservableProperty] public partial string MeasureNote { get; set; } = "";
    [ObservableProperty] public partial string? MeasureError { get; set; }

    public ObservableCollection<MeasuredRow> MeasuredDevices { get; } = [];

    /// <summary>"Measure again" under the earnings: whenever the test isn't running and could.</summary>
    [ObservableProperty] public partial bool CanMeasureAgain { get; set; }

    /// <summary>The overview's line about the last test, e.g. "Measured Oct 18: $41 a month at 16 hours a day".</summary>
    [ObservableProperty] public partial string MeasuredLine { get; set; } = "";

    // ---------- while you were away ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowAwayNow), nameof(ShowMonthlyNow))]
    public partial bool ShowAway { get; set; }
    [ObservableProperty] public partial string AwayAmount { get; set; } = "";
    [ObservableProperty] public partial string AwayText { get; set; } = "";

    // ---------- last month ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMonthlyNow))]
    public partial bool ShowMonthly { get; set; }

    // The overview shows one result at a time, so they don't push "Right now" out of sight: the running test, then
    // its result, then "while you were away", then last month. Closing one shows the next.
    public bool ShowMeasureResultNow => ShowMeasureResult && !IsMeasuring;

    public bool ShowAwayNow => ShowAway && !IsMeasuring && !ShowMeasureResult;

    public bool ShowMonthlyNow => ShowMonthly && !IsMeasuring && !ShowMeasureResult && !ShowAway;
    [ObservableProperty] public partial string MonthlyTitle { get; set; } = "";
    [ObservableProperty] public partial string MonthlyAmount { get; set; } = "";
    [ObservableProperty] public partial string MonthlyText { get; set; } = "";

    /// <summary>Feedback after copying or saving, shown under whichever card was shared.</summary>
    [ObservableProperty] public partial string ShareStatus { get; set; } = "";

    /// <summary>Hours a day this computer earns: measured once there's a day of history, otherwise the usual guess.</summary>
    private double HoursPerDay => _host.History.AverageEarningHoursPerDay(DateTimeOffset.Now) is { } measured
        ? measured
        : _host.Settings.OnlyWhenIdle ? 8 : 16;

    private void Refresh()
    {
        var settings = _host.Settings;
        var run = _host.MeasurementRun;
        IsMeasuring = _host.IsMeasuring;
        var finishedThisSession = run is { Phase: MeasurementPhase.Done } && !_measureClosed;
        ShowMeasureResult = finishedThisSession;
        ShowMeasureOffer = !IsMeasuring && !finishedThisSession && settings.Measurement is null && !settings.MeasurementSkipped
                           && settings.OnboardingComplete;
        CanMeasureAgain = !IsMeasuring && !ShowMeasureOffer && !ShowMeasureResult && _host.MeasurementBlocker is null;

        if (IsMeasuring && run is not null)
        {
            MeasureProgress = run.Progress * 100;
            MeasureStatus = run.Phase == MeasurementPhase.Preparing
                ? _host.Engines.Any(e => e.Snapshot.State == EngineState.Installing)
                    ? "Downloading the mining engines (one time)…"
                    : "Starting the miners…"
                : run.Remaining > TimeSpan.FromSeconds(5)
                    ? $"Measuring… about {Math.Ceiling(run.Remaining.TotalMinutes):0} min left"
                    : _host.Engines.Any(e => e.Snapshot.IsCalibrating)
                        ? "Your graphics card is still trying coins; the test finishes when it has settled."
                        : "Almost done…";
        }

        if (finishedThisSession)
        {
            MeasureError = run!.Result() is null ? run.Failure ?? "Nothing could be measured. " + DescribeEngines() : null;
            if (settings.Measurement is { } result && MeasureError is null)
                ShowResult(result);
        }

        MeasuredLine = settings.Measurement is { } last && !ShowMeasureResult
            ? $"Measured {Format.Date(last.MeasuredAt)}: about {Format.Money((decimal)Counted(last).PerMonth(HoursPerDay))} a month at {HoursPerDay.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)} hours a day."
            : "";

        // While you were away
        if (_host.LastAway is { } away)
        {
            ShowAway = true;
            AwayAmount = Format.Usd(away.EarnedUsd);
            var electricity = settings.PayForElectricity && away.ElectricityUsd > 0
                ? $" Electricity for it: about {Format.Usd(away.ElectricityUsd)}."
                : "";
            AwayText = $"in {Format.Duration(away.Earning)} of earning, {DescribeWhen(away.Ended)}, after Otium's fee.{electricity}";
        }
        else
            ShowAway = false;

        // Last month
        if (MonthlyReport.Due(_host.History, DateTimeOffset.Now, settings.MonthlyReportDismissed) is { } month)
        {
            ShowMonthly = true;
            MonthlyTitle = $"{month.MonthName} in numbers";
            MonthlyAmount = Format.Usd(month.EarnedUsd);
            var electricity = settings.PayForElectricity && month.ElectricityUsd > 0
                ? $", and used about {Format.Usd(month.ElectricityUsd)} of electricity"
                : "";
            MonthlyText = $"earned by this PC in {month.EarningHours:0} hours of earning, after Otium's fee{electricity}.";
        }
        else
            ShowMonthly = false;
    }

    /// <summary>What the test's numbers come to as the computer is set up now: a processor switched off doesn't count.</summary>
    private PcMeasurement Counted(PcMeasurement measured) =>
        !_host.Settings.CpuEnabled && measured.Devices.Any(d => d.Device != DeviceKind.Cpu) ? measured.WithoutProcessor() : measured;

    private void ShowResult(PcMeasurement measured)
    {
        var result = Counted(measured);
        var cpuOff = !ReferenceEquals(result, measured);
        MeasureNote = _host.CpuLeftOffByTest && measured.Devices.FirstOrDefault(d => d.Device == DeviceKind.Cpu) is { } cpu
            ? $"Otium left your processor off: it would add about {Format.Usd(Math.Max(0, cpu.NetUsdPerDay))} a day next to your graphics card's "
              + $"{Format.Usd(result.NetUsdPerDay)}, and your computer stays cooler and quieter without it. You can turn it on in Advanced → Settings → Devices."
            : "";
        var hours = HoursPerDay;
        MeasureAmount = Format.Money((decimal)result.PerMonth(hours));
        MeasureDetail = result.NetUsdPerDay <= 0
            ? "At your electricity price, mining would cost more than it earns, so Otium will mostly wait. Prices change daily; Otium checks every 30 minutes."
            : $"a month, earning about {hours.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)} hours a day while you're away. That's {Format.Usd(result.NetUsdPerDay)} per full day of earning, after Otium's fee"
              + (_host.Settings.PayForElectricity ? " and electricity." : ".");
        var rows = measured.Devices.Select(d => new MeasuredRow(
            d.Name,
            d.HashrateHps > 0 ? $"{d.Coin} at {Format.Hashrate(d.HashrateHps)}" : "Wouldn't pay at your electricity price",
            cpuOff && d.Device == DeviceKind.Cpu ? "Off" : d.NetUsdPerDay > 0 ? $"{Format.Usd(d.NetUsdPerDay)}/day" : "—")).ToList();
        if (!rows.SequenceEqual(MeasuredDevices))
        {
            MeasuredDevices.Clear();
            foreach (var row in rows)
                MeasuredDevices.Add(row);
        }
    }

    private string DescribeEngines() =>
        _host.Engines.Select(e => e.Snapshot).FirstOrDefault(s => s.State is EngineState.Error or EngineState.PausedUnprofitable) is { } s
            ? s.Problem ?? s.StatusText
            : "Check the status above and try again.";

    private static string DescribeWhen(DateTimeOffset ended)
    {
        var local = ended.ToLocalTime();
        var today = DateTimeOffset.Now.Date;
        return local.Date == today
            ? local.Hour < 12 ? "overnight" : "today"
            : local.Date == today.AddDays(-1) ? "yesterday" : $"until {Format.Date(local)}";
    }

    // ---------- commands ----------

    /// <summary>Why the test couldn't start, shown next to the button that tried.</summary>
    [ObservableProperty] public partial string? StartError { get; set; }

    [RelayCommand]
    private void StartMeasure()
    {
        _measureClosed = false;
        ShareStatus = "";
        StartError = _host.StartMeasurement();
        Refresh();
    }

    [RelayCommand]
    private void StopMeasure() => _host.StopMeasurement();

    [RelayCommand]
    private void SkipMeasure()
    {
        _host.Settings.MeasurementSkipped = true;
        _host.SaveSettings();
        Refresh();
    }

    [RelayCommand]
    private void CloseMeasure()
    {
        _measureClosed = true;
        MeasureError = null;
        ShareStatus = "";
        Refresh();
    }

    [RelayCommand]
    private void CloseAway()
    {
        ShareStatus = "";
        _host.DismissAway();
    }

    [RelayCommand]
    private void CloseMonthly()
    {
        if (MonthlyReport.Due(_host.History, DateTimeOffset.Now, null) is { } month)
        {
            _host.Settings.MonthlyReportDismissed = month.Month;
            _host.SaveSettings();
        }
        ShareStatus = "";
        Refresh();
    }

    private string MonthName => MonthlyTitle.Replace(" in numbers", "", StringComparison.Ordinal);

    [RelayCommand]
    private Task CopyMeasure() => CopyAsync($"I measured it: my PC earns about {MeasureAmount} a month");

    [RelayCommand]
    private Task CopyAway() => CopyAsync($"My PC earned {AwayAmount} while I was away");

    [RelayCommand]
    private Task CopyMonthly() => CopyAsync($"My PC earned {MonthlyAmount} in {MonthName}");

    [RelayCommand]
    private Task SaveMeasurePicture() => SavePictureAsync("My PC earns", MeasureAmount, "a month while I'm away, measured by Otium");

    [RelayCommand]
    private Task SaveAwayPicture() => SavePictureAsync("While I was away, my PC earned", AwayAmount, AwayText);

    [RelayCommand]
    private Task SaveMonthlyPicture() => SavePictureAsync($"{MonthName}: my PC earned", MonthlyAmount, "while I wasn't using it, with Otium");

    private async Task CopyAsync(string claim)
    {
        var referral = _host.Settings.Referral;
        await ClipboardService.CopyAsync(InviteMessages.Earned(claim, referral.MyCode, referral.ShareUrl, _host.Fees.RefereeFeeHoliday));
        ShareStatus = "Copied, with your invite code. Paste it anywhere.";
    }

    private async Task SavePictureAsync(string caption, string amount, string detail)
    {
        try
        {
            var png = ShareCard.Render(caption, amount, detail, _host.Settings.Referral.MyCode);
            var saved = await SaveFileService.SaveAsync("Save picture", "otium-earnings.png", "png", png);
            if (saved is not null)
                ShareStatus = "Saved. Post it with your invite code.";
        }
        catch (InvalidOperationException e)
        {
            ShareStatus = e.Message;
        }
    }
}

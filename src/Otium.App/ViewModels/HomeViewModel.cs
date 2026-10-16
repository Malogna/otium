using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Otium.App.Services;
using Otium.Core.Engine;
using Otium.Core.Projections;
using Otium.Core.Providers;
using Otium.Core.Settings;

namespace Otium.App.ViewModels;

/// <summary>One line of "what is Otium doing right now", per device.</summary>
public partial class ActivityRow : ObservableObject
{
    public required Geometry? Icon { get; init; }

    [ObservableProperty] public partial string Title { get; set; } = "";
    [ObservableProperty] public partial string What { get; set; } = "";
    [ObservableProperty] public partial string Speed { get; set; } = "";
    [ObservableProperty] public partial string PerDay { get; set; } = "";
    [ObservableProperty] public partial bool IsActive { get; set; }
}

/// <summary>
/// The everyday screen: balance and the next cash-out, what each device is doing right now in plain words,
/// and what to expect today, this week and this month. Technical detail lives under Advanced.
/// </summary>
public partial class HomeViewModel : ViewModelBase
{
    private readonly AppHost _host;
    private readonly Action _openAdvanced;
    private readonly CountUp _balance;
    private readonly CountUp _month;
    private double _lastRate;          // $/day while earning (after fee), remembered for when it's paused
    private double _lastElectricity;   // $/day of electricity while earning
    private EarningsEstimate? _estimate;

    public HomeViewModel(AppHost host, Action openAdvanced)
    {
        _host = host;
        _openAdvanced = openAdvanced;
        CashOut = new CashOutViewModel(host, () => IsCashingOut = false);
        SignIn = new EmailSignInViewModel(host);
        _balance = new CountUp(v => Balance = Format.Money((decimal)v));
        _month = new CountUp(v => ProjectedMonth = $"≈ {Format.Money((decimal)v)}");

        Activity.Add(new ActivityRow { Icon = Icon("IconCpu"), Title = "Processor" });
        if (host.GpuEngine is not null)
            Activity.Add(new ActivityRow { Icon = Icon("IconGpu"), Title = host.GpuProvider?.Gpus.FirstOrDefault()?.Name ?? "Graphics card" });

        foreach (var engine in host.Engines)
            engine.SnapshotChanged += _ => Dispatcher.UIThread.Post(Refresh);
        host.AccountChanged += () => Dispatcher.UIThread.Post(Refresh);
        host.Updates.Changed += () => Dispatcher.UIThread.Post(Refresh);
        host.HistoryChanged += () => Dispatcher.UIThread.Post(Refresh);

        Refresh();
        _ = LoadEstimateAsync();
    }

    public CashOutViewModel CashOut { get; }

    public EmailSignInViewModel SignIn { get; }

    public ObservableCollection<ActivityRow> Activity { get; } = [];

    // ---------- balance ----------

    [ObservableProperty] public partial string Balance { get; set; } = "$0.00";
    [ObservableProperty] public partial string BalanceCaption { get; set; } = "Your balance";
    [ObservableProperty] public partial string PendingText { get; set; } = "";
    [ObservableProperty] public partial bool IsBalanceMode { get; set; }
    [ObservableProperty] public partial string ProjectedMonth { get; set; } = "";
    [ObservableProperty] public partial string ProjectedMonthCaption { get; set; } = "";

    // ---------- status ----------

    [ObservableProperty] public partial string StateLabel { get; set; } = "Paused";
    [ObservableProperty] public partial string StatusText { get; set; } = "";
    [ObservableProperty] public partial bool IsEarning { get; set; }
    [ObservableProperty] public partial bool IsWaiting { get; set; }
    [ObservableProperty] public partial bool IsProblem { get; set; }
    [ObservableProperty] public partial bool IsRunning { get; set; }
    [ObservableProperty] public partial string ToggleText { get; set; } = "Start";
    [ObservableProperty] public partial bool NeedsAntivirusFix { get; set; }
    [ObservableProperty] public partial string ActivityHeadline { get; set; } = "";

    // ---------- projections & chart ----------

    [ObservableProperty] public partial string TodayText { get; set; } = "$0.00";
    [ObservableProperty] public partial string WeekText { get; set; } = "—";
    [ObservableProperty] public partial string MonthText { get; set; } = "—";
    [ObservableProperty] public partial string ProjectionBasis { get; set; } = "";
    [ObservableProperty] public partial IReadOnlyList<double> ChartValues { get; set; } = new double[24];
    [ObservableProperty] public partial string ChartCaption { get; set; } = "";

    // ---------- cash-out goal ----------

    [ObservableProperty] public partial bool CanCashOut { get; set; }
    [ObservableProperty] public partial string GoalText { get; set; } = "";
    [ObservableProperty] public partial double GoalPercent { get; set; }
    [ObservableProperty] public partial string GoalEta { get; set; } = "";
    [ObservableProperty] public partial bool IsCashingOut { get; set; }

    // ---------- account, invite, update ----------

    [ObservableProperty] public partial bool NeedsEmail { get; set; }
    [ObservableProperty] public partial string AccountLine { get; set; } = "";
    [ObservableProperty] public partial string InviteTitle { get; set; } = "";
    [ObservableProperty] public partial string InviteCode { get; set; } = "";
    [ObservableProperty] public partial bool HasInviteCode { get; set; }
    [ObservableProperty] public partial string CopiedText { get; set; } = "";
    [ObservableProperty] public partial bool UpdateReady { get; set; }
    [ObservableProperty] public partial string UpdateText { get; set; } = "";

    private void Refresh()
    {
        var settings = _host.Settings;
        var account = settings.Account;
        var engines = _host.Engines.ToList();
        var snapshots = engines.Select(e => e.Snapshot).ToList();
        var earningNow = snapshots.Where(s => s.State == EngineState.Earning).ToList();

        // Rates: live while earning; otherwise the last live rate; otherwise the hardware estimate.
        if (earningNow.Count > 0)
        {
            _lastRate = earningNow.Sum(s => s.UserUsdPerDay);
            _lastElectricity = earningNow.Sum(s => s.ElectricityUsdPerDay);
        }
        var rate = _lastRate > 0 ? _lastRate : _estimate?.GrossUsdPerDay ?? 0;
        var electricity = _lastRate > 0 ? _lastElectricity : _estimate?.ElectricityUsdPerDay ?? 0;

        // Balance
        IsBalanceMode = settings.PayoutMode == PayoutMode.Balance;
        decimal balanceValue;
        if (IsBalanceMode)
        {
            balanceValue = account.AvailableUsd;
            BalanceCaption = "Your balance";
            PendingText = account.PendingUsd > 0 ? $"+ {Format.Money(account.PendingUsd)} on the way" : "";
        }
        else
        {
            balanceValue = (decimal)snapshots.Sum(s => s.TotalUserUsd);
            BalanceCaption = "Earned so far · paid straight to your wallet";
            PendingText = "";
        }
        _balance.AnimateTo((double)balanceValue);

        // Projection
        var minimum = Math.Min(account.MinPayPalUsd, account.MinCryptoUsd);
        var projection = Projection.Create(rate, electricity, _host.History.AverageEarningHoursPerDay(DateTimeOffset.Now),
            settings.OnlyWhenIdle, account.AvailableUsd + account.PendingUsd, minimum);
        _month.AnimateTo(projection.PerMonth);
        ProjectedMonthCaption = projection.PerMonth > 0 ? "projected per month" : "";
        var today = DateTimeOffset.Now.Date;
        TodayText = Format.Usd(_host.History.EarnedSince(new DateTimeOffset(today, DateTimeOffset.Now.Offset)));
        WeekText = projection.PerWeek > 0 ? $"≈ {Format.Usd(projection.PerWeek)}" : "—";
        MonthText = projection.PerMonth > 0 ? $"≈ {Format.Usd(projection.PerMonth)}" : "—";
        var away = settings.OnlyWhenIdle ? " away" : "";
        ProjectionBasis = rate > 0 && projection.PerDay <= 0
            ? "At your electricity price, mining currently costs more than it earns, so no profit is projected."
            : projection.HoursAreMeasured
            ? $"Based on your PC earning about {projection.HoursPerDay:0.#} hours a day, after electricity."
            : _lastRate > 0
                ? $"Assuming about {projection.HoursPerDay:0} hours a day{away}. Otium learns your real pattern within a day."
                : $"Estimated from your hardware, about {projection.HoursPerDay:0} hours a day{away}. Real numbers appear once it runs.";

        var hours = _host.History.LastHours(DateTimeOffset.Now, 24);
        if (!hours.SequenceEqual(ChartValues))
            ChartValues = hours;
        var last24 = hours.Sum();
        ChartCaption = last24 > 0 ? $"{Format.Usd(last24)} in the last 24 hours" : "Your earnings will show up here hour by hour.";

        // Cash-out goal
        CanCashOut = IsBalanceMode && account.AvailableUsd >= minimum && account.AvailableUsd > 0;
        if (!IsBalanceMode)
        {
            GoalText = "The pool pays your wallet automatically.";
            GoalPercent = 100;
            GoalEta = "";
        }
        else if (CanCashOut)
        {
            GoalText = "You can cash out now";
            GoalPercent = 100;
            GoalEta = $"PayPal from {Format.Money(account.MinPayPalUsd)}, crypto from {Format.Money(account.MinCryptoUsd)}";
        }
        else
        {
            var towards = account.AvailableUsd + account.PendingUsd;
            GoalText = $"{Format.Money(towards)} of {Format.Money(minimum)} to your first cash-out";
            GoalPercent = minimum > 0 ? (double)Math.Clamp(towards / minimum * 100, 0, 100) : 0;
            GoalEta = projection.TimeToCashout is { } eta && eta > TimeSpan.Zero ? $"about {Friendly(eta)} at this pace" : "";
        }

        // Status and activity
        RefreshActivity(engines);
        IsRunning = settings.EarningEnabled;
        ToggleText = settings.EarningEnabled ? "Pause" : "Start";
        var states = snapshots.Select(s => s.State).ToList();
        NeedsAntivirusFix = snapshots.Any(s => s.EngineBlocked);
        IsEarning = states.Contains(EngineState.Earning);
        IsWaiting = !IsEarning && settings.EarningEnabled && states.Contains(EngineState.WaitingForIdle);
        IsProblem = NeedsAntivirusFix || (!IsEarning && states.Contains(EngineState.Error));
        (StateLabel, StatusText) = Describe(settings, snapshots, earningNow.Sum(s => s.UserUsdPerDay));
        var netNow = earningNow.Sum(s => s.NetUsdPerDay);
        ActivityHeadline = !IsEarning ? StatusText
            : netNow > 0 ? $"Earning {Format.Usd(netNow)} a day right now, after electricity"
            : $"Earning {Format.Usd(earningNow.Sum(s => s.UserUsdPerDay))} a day, but electricity costs {Format.Usd(earningNow.Sum(s => s.ElectricityUsdPerDay))}. Otium pauses if that lasts.";

        // Account, invite, update
        NeedsEmail = IsBalanceMode && account.Email is null && account.WorkerId is not null;
        AccountLine = account.Email is null
            ? ""
            : account.DeviceCount > 1 ? $"Signed in as {account.Email} · {account.DeviceCount} computers" : $"Signed in as {account.Email}";
        var referralPercent = _host.Account?.Balance.ReferralPercent ?? 0;
        InviteTitle = referralPercent > 0 ? $"Get {referralPercent:0.#}% of everything your friends earn" : "Invite friends";
        InviteCode = settings.Referral.MyCode ?? "";
        HasInviteCode = InviteCode.Length > 0;
        UpdateReady = _host.Updates.ReadyVersion is not null;
        UpdateText = UpdateReady ? $"Otium {_host.Updates.ReadyVersion} is ready." : "";
    }

    private void RefreshActivity(IReadOnlyList<EarningsEngine> engines)
    {
        var settings = _host.Settings;
        for (var i = 0; i < Activity.Count && i < engines.Count; i++)
        {
            var row = Activity[i];
            var s = engines[i].Snapshot;
            var enabled = i == 0 ? settings.CpuEnabled : settings.GpuEnabled;
            row.IsActive = s.State == EngineState.Earning;
            row.Speed = s.HashrateHps > 0 ? Format.Hashrate(s.HashrateHps) : "";
            row.PerDay = s.State == EngineState.Earning ? $"{Format.Usd(s.UserUsdPerDay)}/day" : "";
            row.What = s.State switch
            {
                _ when !enabled => "Switched off in settings",
                EngineState.Earning => $"Mining {Coins.ForAlgorithm(s.Detail ?? (i == 0 ? "rx/0" : null)).Name}" +
                                       (s.CurrentRole != PayoutRole.User ? " (short fee slice)" : ""),
                EngineState.Starting when s.Detail?.Contains("Testing", StringComparison.Ordinal) == true => s.Detail,
                EngineState.Starting => "Starting up…",
                EngineState.Installing => "Downloading its engine (one time)…",
                EngineState.WaitingForIdle => WaitingText(s),
                EngineState.PausedUnprofitable => "Paused: electricity would cost more than it earns",
                EngineState.Error => s.EngineBlocked ? "Blocked by antivirus: see Advanced" : "Hit a problem, retrying…",
                _ => settings.EarningEnabled ? "Waiting…" : "Paused",
            };
        }
    }

    private string WaitingText(EngineSnapshot s)
    {
        var threshold = TimeSpan.FromMinutes(Math.Max(1, _host.Settings.IdleMinutes));
        if (s.IdleTime is { } idle && idle < threshold)
        {
            var left = threshold - idle;
            return $"Waiting for you to step away · starts in {(int)left.TotalMinutes}:{left.Seconds:00}";
        }
        return "Waiting for you to step away";
    }

    private (string Label, string Text) Describe(AppSettings settings, IReadOnlyList<EngineSnapshot> snapshots, double userPerDay)
    {
        var states = snapshots.Select(s => s.State).ToList();
        if (!settings.EarningEnabled)
            return ("Paused", "Press Start to earn while you're away from your computer.");
        if (NeedsAntivirusFix)
            return ("Needs your OK", "Your antivirus blocked the earning engine. Open Advanced to allow it.");
        if (_host.MiningBlocker is { } blocker)
            return ("Connecting", blocker);
        if (states.Contains(EngineState.Earning))
            return ("Earning", $"Earning about {Format.Usd(userPerDay)} a day right now.");
        if (states.Contains(EngineState.Installing))
            return ("Setting up", "Downloading the earning engines. This only happens once.");
        if (states.Contains(EngineState.Starting))
            return ("Starting", "Getting ready. Your first earnings show up within a few minutes.");
        if (states.Contains(EngineState.WaitingForIdle))
            return ("Ready", "Otium starts earning when you step away from your computer.");
        if (states.Contains(EngineState.PausedUnprofitable))
            return ("Paused", "Electricity would cost more than you'd earn right now. Otium checks again every 30 minutes.");
        if (states.Contains(EngineState.Error))
            return ("Needs attention", "Something went wrong. Otium retries automatically; see Advanced for details.");
        return ("Starting", "Getting ready…");
    }

    private async Task LoadEstimateAsync()
    {
        try
        {
            _estimate = await _host.EstimateAsync(CancellationToken.None);
            Dispatcher.UIThread.Post(Refresh);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            // Projections fall back to live numbers once mining starts.
        }
    }

    private static string Friendly(TimeSpan t) =>
        t.TotalMinutes < 1.5 ? "a minute"
        : t.TotalHours < 1 ? $"{(int)Math.Round(t.TotalMinutes)} minutes"
        : t.TotalHours < 1.5 ? "an hour"
        : t.TotalDays < 1 ? $"{t.TotalHours:0} hours"
        : t.TotalDays < 2 ? "a day"
        : $"{t.TotalDays:0} days";

    private static Geometry? Icon(string key) =>
        Avalonia.Application.Current?.TryGetResource(key, null, out var value) == true ? value as Geometry : null;

    [RelayCommand]
    private void Toggle() => _host.SetEarning(!_host.Settings.EarningEnabled);

    [RelayCommand]
    private void OpenAdvanced() => _openAdvanced();

    [RelayCommand]
    private void StartCashOut()
    {
        CashOut.Reset();
        IsCashingOut = true;
    }

    [RelayCommand]
    private async Task CopyInvite()
    {
        var link = _host.Settings.Referral.ShareUrl;
        if (string.IsNullOrEmpty(link))
            return;
        await ClipboardService.CopyAsync($"I earn money with my computer's spare power using Otium. Join with my code {InviteCode} and mine fee-free for 30 days: {link}");
        CopiedText = "Invite copied. Paste it to a friend!";
    }

    [RelayCommand]
    private Task RestartToUpdate() => _host.ApplyUpdateAndRestartAsync();
}

using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Otium.App.Services;
using Otium.Core.Accounts;
using Otium.Core.Bandwidth;
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
    private readonly ActivityRow _internetRow;

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

        Results = new ResultsViewModel(host);
        host.MeasurementChanged += () => Dispatcher.UIThread.Post(Refresh);

        _internetRow = new ActivityRow { Icon = Icon("IconGlobe"), Title = "Internet connection" };
        InternetSharing = new InternetSharingViewModel(host);
        InternetSharing.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(InternetSharingViewModel.IsOn))
                Refresh();
        };

        foreach (var engine in host.Engines)
            engine.SnapshotChanged += _ => Dispatcher.UIThread.Post(Refresh);
        host.Bandwidth.SnapshotChanged += _ => Dispatcher.UIThread.Post(Refresh);
        host.AccountChanged += () => Dispatcher.UIThread.Post(Refresh);
        host.Updates.Changed += () => Dispatcher.UIThread.Post(Refresh);
        host.HistoryChanged += () => Dispatcher.UIThread.Post(Refresh);
        host.AntivirusChanged += () => Dispatcher.UIThread.Post(Refresh);

        Refresh();
        _ = LoadEstimateAsync();
    }

    public CashOutViewModel CashOut { get; }

    public EmailSignInViewModel SignIn { get; }

    public ObservableCollection<ActivityRow> Activity { get; } = [];

    /// <summary>The test, "while you were away" and the monthly report.</summary>
    public ResultsViewModel Results { get; }

    /// <summary>Opt-in internet sharing: the suggestion card and its consent screen.</summary>
    public InternetSharingViewModel InternetSharing { get; }

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
    [ObservableProperty] public partial string? AllowResult { get; set; }
    [ObservableProperty] public partial bool IsAllowing { get; set; }

    /// <summary>The one-click fix only changes Microsoft Defender, so it's offered only while Defender is the antivirus.</summary>
    public bool CanAllowAutomatically => WindowsSecurity.IsSupported && (_host.AntivirusReport?.DefenderIsActive ?? true);

    /// <summary>What the antivirus removed and when, and how to fix it when it isn't Defender.</summary>
    [ObservableProperty] public partial string AntivirusDetail { get; set; } = "";
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
    /// <summary>Why the pool's confirmed amount trails what Otium has counted, when it does.</summary>
    [ObservableProperty] public partial string GoalNote { get; set; } = "";
    /// <summary>"At this pace: about $30 a week, $128 a month." Kept apart from the real totals above it.</summary>
    [ObservableProperty] public partial string ProjectionText { get; set; } = "";
    [ObservableProperty] public partial bool IsCashingOut { get; set; }

    /// <summary>Own-address payouts: something the user must fix at the pool (auto-pay off, failed payment…), or "".</summary>
    [ObservableProperty] public partial string PoolWarning { get; set; } = "";

    // ---------- cash-out history ----------

    public ObservableCollection<CashoutRow> Cashouts { get; } = [];
    [ObservableProperty] public partial bool HasCashouts { get; set; }
    [ObservableProperty] public partial string CashoutsSummary { get; set; } = "";

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

        // Rates: live while earning; otherwise the last live rate; otherwise the test's measurement; otherwise the
        // hardware estimate. A device the test found unprofitable counts as zero: Otium doesn't run it.
        if (earningNow.Count > 0)
        {
            _lastRate = earningNow.Sum(s => s.UserUsdPerDay);
            _lastElectricity = earningNow.Sum(s => s.ElectricityUsdPerDay);
        }
        var measured = settings.Measurement?.Devices.Where(d => d.NetUsdPerDay > 0).ToList();
        var (rate, electricity) = _lastRate > 0 ? (_lastRate, _lastElectricity)
            : measured is { Count: > 0 } ? (measured.Sum(d => d.UserUsdPerDay), measured.Sum(d => d.ElectricityUsdPerDay))
            : (_estimate?.GrossUsdPerDay ?? 0, _estimate?.ElectricityUsdPerDay ?? 0);

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
            // Internet sharing pays the same wallet (Proxybase's earnings are converted to the user's coin), so it counts.
            var sharedUsd = _host.Bandwidth.Snapshot.TotalUserUsd;
            // The engines' ledgers, not their snapshots: those are empty until each engine's first tick after a start.
            balanceValue = (decimal)(engines.Sum(e => e.Totals.UserUsd) + sharedUsd);
            BalanceCaption = "Earned so far · paid straight to your wallet";
            var parts = new List<string>();
            if (sharedUsd > 0)
                parts.Add($"{Format.Usd(sharedUsd)} of it from internet sharing");
            if (_host.PoolBalance is { Paid: > 0 } paid)
                parts.Add($"{Format.Coin(paid.Paid, paid.Coin)} paid to you so far");
            PendingText = string.Join(" · ", parts);
        }
        _balance.AnimateTo((double)balanceValue);

        // Projection
        var minimum = account.MinCryptoUsd;
        var mining = Projection.Create(rate, electricity, _host.History.AverageEarningHoursPerDay(DateTimeOffset.Now),
            settings.OnlyWhenIdle, account.AvailableUsd + account.PendingUsd, minimum);
        var projection = WithSharing(mining, SharingPerDay(), account.AvailableUsd + account.PendingUsd, minimum);
        _month.AnimateTo(projection.PerMonth);
        ProjectedMonthCaption = projection.PerMonth > 0 ? "projected per month" : "";
        var today = DateTimeOffset.Now.Date;
        // The tiles are what was really earned; the projection is a separate line, so the two can't be mixed up.
        var now = DateTimeOffset.Now;
        TodayText = Format.Usd(_host.History.EarnedSince(new DateTimeOffset(today, now.Offset)));
        WeekText = Format.Usd(_host.History.EarnedSince(new DateTimeOffset(StartOfWeek(today), now.Offset)));
        MonthText = Format.Usd(_host.History.Month(MonthBucket.KeyFor(now))?.EarnedUsd ?? 0);
        ProjectionText = projection.PerMonth > 0
            ? $"At this pace: about {Format.Usd(projection.PerWeek)} a week, {Format.Usd(projection.PerMonth)} a month."
            : "";
        var away = settings.OnlyWhenIdle ? " away" : "";
        var sharingEstimated = InternetSharing.IsOn && InternetSharing.IsProxybase && _host.Bandwidth.Snapshot.UserUsdPerDay <= 0;
        var miningLoses = rate > 0 && mining.PerDay <= 0;
        ProjectionBasis = miningLoses && projection.PerDay <= 0
            ? "At your electricity price, mining currently costs more than it earns, so no profit is projected."
            : miningLoses
                ? "Mining doesn't pay at your electricity price, so this comes from internet sharing alone."
                : projection.HoursAreMeasured
                    ? $"Based on your PC earning about {projection.HoursPerDay:0.#} hours a day, after electricity."
                    : _lastRate > 0 || measured is { Count: > 0 }
                        ? $"Assuming about {projection.HoursPerDay:0} hours a day{away}. Otium learns your real pattern within a day."
                        : $"Estimated from your hardware, about {projection.HoursPerDay:0} hours a day{away}. Real numbers appear once it runs.";
        if (sharingEstimated && projection.PerDay > 0)
            ProjectionBasis += " Internet sharing is estimated from Proxybase's rate until your own earnings come in.";

        var hours = _host.History.LastHours(DateTimeOffset.Now, 24);
        if (!hours.SequenceEqual(ChartValues))
            ChartValues = hours;
        var last24 = hours.Sum();
        ChartCaption = last24 > 0 ? $"{Format.Usd(last24)} in the last 24 hours" : "Your earnings will show up here hour by hour.";

        // Cash-out goal
        CanCashOut = IsBalanceMode && account.AvailableUsd >= minimum && account.AvailableUsd > 0;
        if (!IsBalanceMode)
            RefreshPoolGoal(settings.EarningEnabled ? rate : 0, engines.Sum(e => e.Totals.UserUsd));
        else if (CanCashOut)
        {
            GoalText = "You can cash out now";
            GoalPercent = 100;
            GoalEta = $"In BTC, ETH, BNB or SOL, from {Format.Money(account.MinCryptoUsd)}";
        }
        else
        {
            var towards = account.AvailableUsd + account.PendingUsd;
            var which = _host.Account?.Balance.RecentCashouts.Count > 0 ? "next" : "first";
            GoalText = $"{Format.Money(towards)} of {Format.Money(minimum)} to your {which} cash-out";
            GoalPercent = minimum > 0 ? (double)Math.Clamp(towards / minimum * 100, 0, 100) : 0;
            GoalEta = settings.EarningEnabled && projection.TimeToCashout is { } eta && eta > TimeSpan.Zero
                ? $"about {Friendly(eta)} at this pace"
                : "";
        }

        RefreshCashouts();

        // Status and activity
        RefreshActivity(engines);
        RefreshInternetRow();
        IsRunning = settings.EarningEnabled;
        ToggleText = settings.EarningEnabled ? "Pause" : "Start";
        var states = snapshots.Select(s => s.State).ToList();
        NeedsAntivirusFix = snapshots.Any(s => s.EngineBlocked);
        if (NeedsAntivirusFix)
            _ = _host.RefreshAntivirusAsync();
        AntivirusDetail = NeedsAntivirusFix ? _host.AntivirusReport?.Describe(_host.Paths.EnginesDir) ?? "" : "";
        OnPropertyChanged(nameof(CanAllowAutomatically));
        IsEarning = states.Contains(EngineState.Earning);
        IsWaiting = !IsEarning && settings.EarningEnabled && states.Contains(EngineState.WaitingForIdle);
        IsProblem = NeedsAntivirusFix || (!IsEarning && states.Contains(EngineState.Error));
        var sharing = _host.Bandwidth.Snapshot.State == BandwidthState.Sharing;
        (StateLabel, StatusText) = Describe(settings, snapshots, earningNow.Sum(s => s.UserUsdPerDay));
        if (!IsEarning && sharing && !IsProblem && settings.EarningEnabled)
        {
            IsEarning = true;
            IsWaiting = false;
            (StateLabel, StatusText) = ("Earning", "Sharing your internet connection. Mining starts when you step away.");
        }
        var sharingNow = sharing ? _host.Bandwidth.Snapshot.UserUsdPerDay : 0;
        var netNow = earningNow.Sum(s => s.NetUsdPerDay) + sharingNow;
        ActivityHeadline = earningNow.Count == 0 && sharingNow <= 0 ? StatusText
            : netNow > 0 ? $"Earning {Format.Usd(netNow)} a day right now, after electricity"
            : $"Earning {Format.Usd(earningNow.Sum(s => s.UserUsdPerDay) + sharingNow)} a day, but electricity costs {Format.Usd(earningNow.Sum(s => s.ElectricityUsdPerDay))}. Otium pauses if that lasts.";

        // Account, invite, update
        NeedsEmail = IsBalanceMode && account.Email is null && account.WorkerId is not null;
        AccountLine = account.Email is null
            ? ""
            : account.DeviceCount > 1 ? $"Signed in as {account.Email} · {account.DeviceCount} computers" : $"Signed in as {account.Email}";
        // Balance accounts get a share of friends' credited earnings; own-address users get half of Otium's fee, mined
        // to their address, which is that share of their friends' mining time.
        var referralPercent = IsBalanceMode
            ? (double)(_host.Account?.Balance.ReferralPercent ?? 0)
            : _host.Fees.ReferrerPercent;
        InviteTitle = referralPercent <= 0 ? "Invite friends"
            : IsBalanceMode ? FormattableString.Invariant($"Get {referralPercent:0.#}% of everything your friends earn")
            : FormattableString.Invariant($"Get {referralPercent:0.#}% of your friends' mining time, paid to your wallet");
        InviteCode = settings.Referral.MyCode ?? "";
        HasInviteCode = InviteCode.Length > 0;
        UpdateReady = _host.Updates.ReadyVersion is not null;
        UpdateText = UpdateReady ? $"Otium {_host.Updates.ReadyVersion} is ready." : "";
    }

    /// <summary>
    /// Own-address payouts: how far the pool balance is from its payout threshold, and anything that would stop the
    /// pool from paying (unMineable's per-address auto-pay switch, failed payments, paused networks).
    /// </summary>
    /// <param name="usdPerDay">The pace for "about 10 hours at this pace"; 0 while paused, which hides it.</param>
    /// <param name="minedUsd">What Otium has counted from mining, to tell when the pool's confirmed amount trails it.</param>
    private void RefreshPoolGoal(double usdPerDay, double minedUsd)
    {
        var pool = _host.PoolBalance;
        PoolWarning = "";
        GoalEta = "";
        GoalNote = "";
        if (_host.OwnPayoutTarget is null)
        {
            GoalText = "Add your payout address in Settings to start earning.";
            GoalPercent = 0;
            return;
        }
        if (pool is null)
        {
            GoalText = _host.PoolBalanceError ?? "Checking your balance at the pool…";
            GoalPercent = 0;
            return;
        }

        GoalPercent = pool.MinimumPayout > 0 ? Math.Clamp(pool.Pending / pool.MinimumPayout * 100, 0, 100) : 0;
        GoalText = $"{Format.Coin(pool.Pending, pool.Coin)} confirmed at {pool.PoolName} · paid out at {Format.Coin(pool.MinimumPayout, pool.Coin)}";
        // USDT is worth about a dollar, so the two can be compared: pools credit mining a few hours late.
        var confirmedUsd = pool.Pending + pool.Paid;
        if (pool.Coin == "USDT" && minedUsd - confirmedUsd > Math.Max(0.01, minedUsd * 0.1))
            GoalNote = $"{pool.PoolName} confirms mining a few hours after it happens, so this trails what Otium has counted.";
        if (pool.Pending >= pool.MinimumPayout && pool.MinimumPayout > 0)
            GoalEta = pool.AutoPay ? "paid out at the next payout" : "ready: request it at the pool";
        else if (pool.Coin == "USDT" && usdPerDay > 0)
            // USDT is worth about a dollar, so the dollar rate tells how long the rest takes.
            GoalEta = $"about {Friendly(TimeSpan.FromDays((pool.MinimumPayout - pool.Pending) / usdPerDay))} at this pace";

        var warnings = new List<string>();
        if (!pool.AutoPay)
            warnings.Add($"Auto pay is off for your address at {pool.PoolName}, so it won't pay you by itself. Turn on \"Auto pay\" on your address page.");
        warnings.AddRange(pool.Problems ?? []);

        // Monero users' graphics cards earn on unMineable, which pays the same address separately.
        if (_host.GpuPoolBalance is { } gpu)
        {
            GoalText += $" · plus {Format.Coin(gpu.Pending, gpu.Coin)} at {gpu.PoolName} from the graphics card, paid out at {Format.Coin(gpu.MinimumPayout, gpu.Coin)}";
            if (!gpu.AutoPay)
                warnings.Add($"Auto pay is off for your address at {gpu.PoolName}, so your graphics card's earnings won't be paid by themselves. Turn on \"Auto pay\" on unMineable's address page.");
            warnings.AddRange(gpu.Problems ?? []);
        }

        if (_host.QuantusBalance is { } quantus && (quantus.Pending > 0 || quantus.Paid > 0))
            GoalText += $" · plus {Format.Coin(quantus.Pending, quantus.Coin)} at {quantus.PoolName} for your Quantus wallet, paid out at {Format.Coin(quantus.MinimumPayout, quantus.Coin)}";
        PoolWarning = string.Join(" ", warnings);
    }

    /// <summary>The first day of this week in the user's culture (Monday in most of Europe, Sunday in the US).</summary>
    private static DateTime StartOfWeek(DateTime today)
    {
        var first = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        return today.AddDays(-(((int)today.DayOfWeek - (int)first + 7) % 7));
    }

    [RelayCommand]
    private void OpenPoolPage()
    {
        // The warning may be about the graphics card's pool (unMineable for Monero users), so open the page it's about.
        var gpu = _host.GpuPoolBalance;
        var pool = gpu is { } g && (!g.AutoPay || g.Problems is { Count: > 0 }) ? gpu : _host.PoolBalance;
        if (pool?.DashboardUrl is { } url)
            Launch.Open(url.ToString());
    }

    private void RefreshCashouts()
    {
        var balance = _host.Account?.Balance;
        var rows = (balance?.RecentCashouts ?? []).Select(CashoutRow.From).ToList();
        if (!rows.SequenceEqual(Cashouts))
        {
            Cashouts.Clear();
            foreach (var row in rows)
                Cashouts.Add(row);
        }
        HasCashouts = IsBalanceMode && rows.Count > 0;
        var open = rows.FirstOrDefault(r => r.IsPending);
        var paidOut = balance?.PaidOutUsd ?? 0;
        CashoutsSummary = open is not null
            ? $"Your {open.Amount} cash-out: {open.Status.ToLowerInvariant()}"
            : paidOut > 0 ? $"{Format.Money(paidOut)} paid out so far" : "";
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
                EngineState.Starting or EngineState.Error when s.Problem is { } problem => problem,
                EngineState.Earning => $"Mining {Coins.ForAlgorithm(s.Detail ?? (i == 0 ? "rx/0" : null)).Name}" +
                                       (s.CurrentRole != PayoutRole.User ? " (short fee slice)" : ""),
                EngineState.Starting when s.Detail?.Contains("Testing", StringComparison.Ordinal) == true => s.Detail,
                EngineState.Starting => "Starting up…",
                EngineState.Installing => "Downloading its engine (one time)…",
                EngineState.WaitingForIdle => WaitingText(s),
                EngineState.PausedUnprofitable => "Paused: electricity would cost more than it earns",
                EngineState.Error => s.EngineBlocked ? "Blocked by antivirus" : "Hit a problem, retrying…",
                _ => settings.EarningEnabled ? "Waiting…" : "Paused",
            };
        }
    }

    private void RefreshInternetRow()
    {
        var settings = _host.Settings;
        var on = settings.BandwidthEnabled && settings.BandwidthConsentAt is not null && _host.BandwidthOffered;
        if (on && !Activity.Contains(_internetRow))
            Activity.Add(_internetRow);
        else if (!on && Activity.Contains(_internetRow))
            Activity.Remove(_internetRow);
        if (!on)
            return;

        var b = _host.Bandwidth.Snapshot;
        _internetRow.IsActive = b.State == BandwidthState.Sharing;
        // Until this computer has its own pace, Proxybase sharing shows the estimate from its rate here (marked as one).
        var directPay = InternetSharing.IsProxybase || b.Earnings is not null;
        var estimate = directPay && b.UserUsdPerDay <= 0 ? InternetSharing.EstimatedUsdPerDay : 0;
        _internetRow.PerDay = b.State != BandwidthState.Sharing ? ""
            : b.UserUsdPerDay > 0 ? $"{Format.Usd(b.UserUsdPerDay)}/day"
            : estimate > 0 ? $"≈ {Format.Usd(estimate)}/day" : "";
        // Partners that pay the user directly (Proxybase) report money, not traffic: show what it has earned so far.
        _internetRow.Speed = directPay
            ? (b.TotalUserUsd > 0 ? $"{Format.Usd(b.TotalUserUsd)} earned so far"
                : estimate > 0 && b.State != BandwidthState.Sharing ? $"≈ {Format.Usd(estimate)}/day estimate · nothing earned yet"
                : estimate > 0 ? "estimate · nothing earned yet"
                : "Nothing earned yet")
            : b.BytesToday > 0 ? $"{InternetSharingViewModel.FormatBytes(b.BytesToday)} today" : "";
        _internetRow.What = b.State switch
        {
            BandwidthState.Sharing => b.Activity ?? "Sharing unused bandwidth",
            BandwidthState.WaitingForIdle => "Waiting for you to step away",
            BandwidthState.LimitReached => "Today's data limit reached",
            BandwidthState.Starting => "Connecting…",
            BandwidthState.Error or BandwidthState.Unavailable => b.StatusText,
            _ => settings.EarningEnabled ? "Starting…" : "Paused",
        };
        if (b.Note is { } note && b.State == BandwidthState.Sharing)
            _internetRow.What = note;
    }

    /// <summary>What internet sharing adds per day: measured while sharing, otherwise the typical estimate.</summary>
    private double SharingPerDay()
    {
        if (!InternetSharing.IsOn)
            return 0;
        var b = _host.Bandwidth.Snapshot;
        // Proxybase reports its pace over whole days already (a per-hour rate covers sharing hours only); until this
        // computer has one, the estimate from Proxybase's rate here counts, as the internet row shows it.
        if (InternetSharing.IsProxybase)
            return b.UserUsdPerDay > 0 ? b.UserUsdPerDay : InternetSharing.EstimatedUsdPerDay;
        var hours = _host.Settings.BandwidthOnlyWhenIdle ? 8 : 16;
        return b.State == BandwidthState.Sharing && b.UserUsdPerDay > 0 ? b.UserUsdPerDay * hours / 24 : InternetSharing.ExpectedUsdPerDay;
    }

    private static Projection WithSharing(Projection mining, double sharingPerDay, decimal towards, decimal minimum)
    {
        if (sharingPerDay <= 0)
            return mining;
        var perDay = mining.PerDay + sharingPerDay;
        var missing = (double)(minimum - towards);
        TimeSpan? eta = missing <= 0 ? TimeSpan.Zero : TimeSpan.FromDays(Math.Min(missing / perDay, 3650));
        return mining with { PerDay = perDay, PerWeek = perDay * 7, PerMonth = perDay * 30, TimeToCashout = eta };
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
            return ("Needs your OK", "Your antivirus blocked the earning engines. Allow them above to start earning.");
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
        if (snapshots.FirstOrDefault(s => s.State == EngineState.Error && s.Problem is not null) is { } explained)
            return ("Needs attention", explained.Problem!);
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
    private async Task AllowAutomatically()
    {
        IsAllowing = true;
        AllowResult = await _host.AllowEnginesInWindowsSecurityAsync() ?? "Allowed. Downloading the engines again…";
        IsAllowing = false;
    }

    [RelayCommand]
    private void StartCashOut()
    {
        CashOut.Reset();
        IsCashingOut = true;
    }

    [RelayCommand]
    private void OpenTransaction(string? url)
    {
        if (!string.IsNullOrEmpty(url))
            Launch.Open(url);
    }

    [RelayCommand]
    private async Task CopyTransaction(string? transactionId)
    {
        if (!string.IsNullOrEmpty(transactionId))
            await ClipboardService.CopyAsync(transactionId);
    }

    [RelayCommand]
    private async Task CopyInvite()
    {
        var link = _host.Settings.Referral.ShareUrl;
        if (string.IsNullOrEmpty(link))
            return;
        await ClipboardService.CopyAsync(InviteMessages.Fill(InviteMessages.Styles[1].Template, InviteCode, link, _host.Fees.RefereeFeeHoliday));
        CopiedText = "Invite copied. Paste it to a friend!";
    }

    [RelayCommand]
    private Task RestartToUpdate() => _host.ApplyUpdateAndRestartAsync();
}

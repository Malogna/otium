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

    /// <summary>For the internet connection: each network it shares through, shown when the row is opened.</summary>
    public System.Collections.ObjectModel.ObservableCollection<ProviderLine> Providers { get; } = [];

    [ObservableProperty] public partial bool HasProviders { get; set; }

    [ObservableProperty] public partial bool IsExpanded { get; set; }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;
}

/// <summary>One network under the internet connection row: its name, what it's doing and what it has earned.</summary>
public partial class ProviderLine : ObservableObject
{
    [ObservableProperty] public partial string Name { get; set; } = "";
    [ObservableProperty] public partial string What { get; set; } = "";
    [ObservableProperty] public partial string Amount { get; set; } = "";
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
    // Each device's last pace while it earned ($/day after the fee, and its electricity), remembered for while it's
    // paused, waiting or testing coins, so one idle device doesn't drop out of the projection.
    private readonly Dictionary<DeviceKind, (double User, double Electricity)> _lastRates = [];
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
            Activity.Add(new ActivityRow { Icon = Icon("IconGpu"), Title = Otium.Core.Gpu.GpuInfo.NamesOf(host.GpuProvider?.Gpus ?? []) ?? "Graphics card" });

        Results = new ResultsViewModel(host);
        host.MeasurementChanged += () => Dispatcher.UIThread.Post(Refresh);

        _internetRow = new ActivityRow { Icon = Icon("IconGlobe"), Title = "Internet connection" };
        InternetSharing = new InternetSharingViewModel(host);
        MysteriumSharing = new MysteriumSharingViewModel(host);
        AiozSharing = new AiozSharingViewModel(host);
        SharingSetup = new SharingSetupViewModel(host, InternetSharing, MysteriumSharing, AiozSharing) { CanDismiss = true };
        SharingSetup.Dismissed += () =>
        {
            ShowSharingSetup = false;
            Refresh();
        };
        MysteriumSharing.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MysteriumSharingViewModel.IsOn))
                Refresh();
        };
        InternetSharing.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(InternetSharingViewModel.IsOn))
                Refresh();
        };
        AiozSharing.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AiozSharingViewModel.IsOn))
                Refresh();
        };

        foreach (var engine in host.Engines)
            engine.SnapshotChanged += _ => Dispatcher.UIThread.Post(Refresh);
        host.Bandwidth.SnapshotChanged += _ => Dispatcher.UIThread.Post(Refresh);
        if (host.Mysterium is { } mysterium)
            mysterium.SnapshotChanged += _ => Dispatcher.UIThread.Post(Refresh);
        if (host.Aioz is { } aioz)
            aioz.SnapshotChanged += _ => Dispatcher.UIThread.Post(Refresh);
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

    /// <summary>Sharing through Mysterium, next to Proxybase: its own suggestion card and overview row.</summary>
    public MysteriumSharingViewModel MysteriumSharing { get; }

    /// <summary>Sharing disk space and upload bandwidth through AIOZ: its line in the internet row and the sharing setup.</summary>
    public AiozSharingViewModel AiozSharing { get; }

    /// <summary>The overview's one card for turning internet sharing on: every network at once.</summary>
    public SharingSetupViewModel SharingSetup { get; }

    /// <summary>Some network is offered and off, and the user hasn't said "Not now" to it.</summary>
    [ObservableProperty] public partial bool ShowSharingSuggestion { get; set; }

    /// <summary>The suggestion is open, showing the networks to tick.</summary>
    [ObservableProperty] public partial bool ShowSharingSetup { get; set; }

    /// <summary>"Earn from your internet too" while nothing is shared, otherwise "Add another network".</summary>
    [ObservableProperty] public partial string SharingSuggestionTitle { get; set; } = "Earn from your internet too";

    [ObservableProperty] public partial string SharingSuggestionText { get; set; } = "";

    [RelayCommand]
    private void OpenSharingSetup() => ShowSharingSetup = true;

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

        // Rates, per device: live while it earns; otherwise its last live rate; otherwise the test's measurement of it.
        // Without either for any device, the hardware estimate. A device the test found unprofitable counts as zero:
        // Otium doesn't run it.
        foreach (var engine in engines)
        {
            if (engine.Snapshot is { State: EngineState.Earning, IsCalibrating: false } s)
                _lastRates[engine.Provider.Device] = (s.UserUsdPerDay, s.ElectricityUsdPerDay);
        }
        var measured = settings.Measurement?.Devices.Where(d => d.NetUsdPerDay > 0).ToList();
        var known = engines.Where(e => e.Enabled).Select(e => e.Provider.Device).Distinct()
            .Select(kind => _lastRates.TryGetValue(kind, out var last) ? last
                : measured?.FirstOrDefault(d => d.Device == kind) is { } m ? (m.UserUsdPerDay, m.ElectricityUsdPerDay)
                : ((double User, double Electricity)?)null)
            .Where(r => r is not null).Select(r => r!.Value).ToList();
        var (rate, electricity) = known.Count > 0 ? (known.Sum(r => r.User), known.Sum(r => r.Electricity))
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
            // Internet sharing pays the user too (Proxybase's earnings converted to their coin, Mysterium's in MYST), so it counts.
            var sharedUsd = _host.Sharers.Sum(s => s.Snapshot.TotalUserUsd);
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
                    : _lastRates.Count > 0 || measured is { Count: > 0 }
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
            // The pool is paid by mining only, at the projected pace (all devices, the hours they earn), not by whatever
            // happens to be running this minute (a graphics card testing coins earned nothing for a few minutes).
            RefreshPoolGoal(settings.EarningEnabled ? mining.EarningRatePerDay * mining.HoursPerDay / 24 : 0, engines.Sum(e => e.Totals.UserUsd));
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
        RefreshSharingSuggestion();
        IsRunning = settings.EarningEnabled;
        ToggleText = settings.EarningEnabled ? "Pause" : "Start";
        var states = snapshots.Select(s => s.State).ToList();
        NeedsAntivirusFix = snapshots.Any(s => s.EngineBlocked) || _host.Sharers.Any(s => s.Snapshot.EngineBlocked);
        if (NeedsAntivirusFix)
            _ = _host.RefreshAntivirusAsync();
        AntivirusDetail = NeedsAntivirusFix ? _host.AntivirusReport?.Describe(_host.Paths.EnginesDir) ?? "" : "";
        OnPropertyChanged(nameof(CanAllowAutomatically));
        IsEarning = states.Contains(EngineState.Earning);
        IsWaiting = !IsEarning && settings.EarningEnabled && states.Contains(EngineState.WaitingForIdle);
        IsProblem = NeedsAntivirusFix || (!IsEarning && states.Contains(EngineState.Error));
        var sharers = _host.Sharers.Select(s => s.Snapshot).Where(s => s.State == BandwidthState.Sharing).ToList();
        var sharing = sharers.Count > 0;
        (StateLabel, StatusText) = Describe(settings, snapshots, earningNow.Sum(s => s.UserUsdPerDay));
        if (!IsEarning && sharing && !IsProblem && settings.EarningEnabled)
        {
            IsEarning = true;
            IsWaiting = false;
            (StateLabel, StatusText) = ("Earning", "Sharing your internet connection. Mining starts when you step away.");
        }
        var sharingNow = sharers.Sum(s => s.UserUsdPerDay);
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

    /// <summary>
    /// The internet connection: one row for every network it shares through, with their pace and earnings added up;
    /// opening it lists each network on its own line.
    /// </summary>
    private void RefreshInternetRow()
    {
        var settings = _host.Settings;
        var lines = new List<(ProviderLine Line, double PerDay, bool Estimate, double Earned, string What)>();
        if (settings.BandwidthEnabled && settings.BandwidthConsentAt is not null && _host.BandwidthOffered)
            lines.Add(ProxybaseLine(settings));
        if (_host.Mysterium is { } mysterium && settings.MysteriumEnabled && settings.MysteriumConsentAt is not null && _host.MysteriumOffered)
            lines.Add(MysteriumLine(settings, mysterium.Snapshot));
        if (_host.Aioz is { } aioz && settings.AiozEnabled && settings.AiozConsentAt is not null && _host.AiozOffered)
            lines.Add(AiozLine(settings, aioz.Snapshot));

        var on = lines.Count > 0;
        if (on && !Activity.Contains(_internetRow))
            Activity.Add(_internetRow);
        else if (!on && Activity.Contains(_internetRow))
            Activity.Remove(_internetRow);
        if (!on)
            return;

        // Keep the same line objects while the set of networks is the same, so an open dropdown doesn't flicker.
        if (_internetRow.Providers.Count != lines.Count || _internetRow.Providers.Select(p => p.Name).Except(lines.Select(l => l.Line.Name)).Any())
        {
            _internetRow.Providers.Clear();
            foreach (var l in lines)
                _internetRow.Providers.Add(l.Line);
        }
        else
        {
            foreach (var (existing, fresh) in _internetRow.Providers.Zip(lines.Select(l => l.Line)))
            {
                existing.What = fresh.What;
                existing.Amount = fresh.Amount;
                existing.IsActive = fresh.IsActive;
            }
        }
        _internetRow.HasProviders = true;

        var active = lines.Count(l => l.Line.IsActive);
        _internetRow.IsActive = active > 0;
        var perDay = lines.Sum(l => l.PerDay);
        _internetRow.PerDay = perDay <= 0 ? "" : $"{(lines.Any(l => l.Estimate && l.PerDay > 0) ? "≈ " : "")}{Format.Usd(perDay)}/day";
        var earned = lines.Sum(l => l.Earned);
        var networks = lines.Count == 1 ? $"via {lines[0].Line.Name}" : $"{lines.Count} networks";
        _internetRow.Speed = earned > 0 ? $"{Format.Usd(earned)} earned · {networks}" : $"Nothing earned yet · {networks}";
        _internetRow.What = lines.Count == 1 ? lines[0].What
            : active == lines.Count ? "Sharing unused bandwidth on every network"
            : active > 0 ? $"Sharing on {string.Join(" and ", lines.Where(l => l.Line.IsActive).Select(l => l.Line.Name))}"
            : lines[0].What;
    }

    /// <summary>Proxybase: its own pace, or until there is one the estimate from its rate here (marked as one).</summary>
    private (ProviderLine, double, bool, double, string) ProxybaseLine(Core.Settings.AppSettings settings)
    {
        var b = _host.Bandwidth.Snapshot;
        var sharing = b.State == BandwidthState.Sharing;
        var directPay = InternetSharing.IsProxybase || b.Earnings is not null;
        var estimate = directPay && b.UserUsdPerDay <= 0 ? InternetSharing.EstimatedUsdPerDay : 0;
        var perDay = !sharing ? 0 : b.UserUsdPerDay > 0 ? b.UserUsdPerDay : estimate;
        var what = SharingWhat(settings, b, b.State == BandwidthState.Sharing && b.Note is { } note ? note : null);
        var amount = (perDay > 0 ? $"{(b.UserUsdPerDay > 0 ? "" : "≈ ")}{Format.Usd(perDay)}/day · " : "")
                     + (b.TotalUserUsd > 0 ? $"{Format.Usd(b.TotalUserUsd)} earned" : "nothing earned yet");
        return (new ProviderLine { Name = "Proxybase", What = what, Amount = amount, IsActive = sharing },
            perDay, b.UserUsdPerDay <= 0, b.TotalUserUsd, what);
    }

    /// <summary>Mysterium: paid in MYST, shown in dollars at today's price once the node has earned something.</summary>
    private static (ProviderLine, double, bool, double, string) MysteriumLine(Core.Settings.AppSettings settings, BandwidthSnapshot b)
    {
        var sharing = b.State == BandwidthState.Sharing;
        var perDay = sharing ? b.UserUsdPerDay : 0;
        var what = SharingWhat(settings, b, null, "Sharing with Mysterium's business customers");
        var amount = (perDay > 0 ? $"{Format.Usd(perDay)}/day · " : "")
                     + (b.TotalUserUsd > 0 ? $"{Format.Usd(b.TotalUserUsd)} earned in MYST" : "nothing earned yet");
        return (new ProviderLine { Name = "Mysterium", What = what, Amount = amount, IsActive = sharing }, perDay, false, b.TotalUserUsd, what);
    }

    /// <summary>AIOZ: paid in AIOZ, shown in dollars at today's price once the node has earned something.</summary>
    private static (ProviderLine, double, bool, double, string) AiozLine(Core.Settings.AppSettings settings, BandwidthSnapshot b)
    {
        var sharing = b.State == BandwidthState.Sharing;
        var perDay = sharing ? b.UserUsdPerDay : 0;
        var what = SharingWhat(settings, b, null, "Storing and delivering files for AIOZ");
        var amount = (perDay > 0 ? $"{Format.Usd(perDay)}/day · " : "")
                     + (b.TotalUserUsd > 0 ? $"{Format.Usd(b.TotalUserUsd)} earned in AIOZ" : "nothing earned yet");
        return (new ProviderLine { Name = "AIOZ", What = what, Amount = amount, IsActive = sharing }, perDay, false, b.TotalUserUsd, what);
    }

    private static string SharingWhat(Core.Settings.AppSettings settings, BandwidthSnapshot b, string? note, string sharing = "Sharing unused bandwidth") =>
        note ?? b.State switch
        {
            BandwidthState.Sharing => b.Activity ?? sharing,
            BandwidthState.WaitingForIdle => "Waiting for you to step away",
            BandwidthState.LimitReached => "Today's data limit reached",
            BandwidthState.Starting => b.StatusText is { Length: > 0 } text ? text : "Connecting…",
            BandwidthState.Error or BandwidthState.Unavailable => b.StatusText,
            _ => settings.EarningEnabled ? "Starting…" : "Paused",
        };

    /// <summary>The one suggestion card: shown while a network is offered, off and not turned down.</summary>
    private void RefreshSharingSuggestion()
    {
        var settings = _host.Settings;
        SharingSetup.Refresh();
        var proxybase = InternetSharing.CanTurnOn && !settings.BandwidthPromptDismissed;
        var mysterium = MysteriumSharing.CanTurnOn && !settings.MysteriumPromptDismissed;
        var aioz = AiozSharing.CanTurnOn && !settings.AiozPromptDismissed;
        ShowSharingSuggestion = settings.OnboardingComplete && (proxybase || mysterium || aioz);
        var sharingAlready = InternetSharing.IsOn || MysteriumSharing.IsOn || AiozSharing.IsOn;
        SharingSuggestionTitle = sharingAlready ? "Add another network" : "Earn from your internet too";
        SharingSuggestionText = sharingAlready
            ? "Share your unused internet through one more network too, for more buyers and more earnings."
            : "Share internet you're not using, even while you're at the computer, through one or more networks. You choose which.";
    }

    /// <summary>What internet sharing adds per day: measured while sharing, otherwise the typical estimate.</summary>
    private double SharingPerDay() => ProxybaseSharingPerDay() + (MysteriumSharing.IsOn ? MysteriumSharing.ExpectedUsdPerDay : 0);

    private double ProxybaseSharingPerDay()
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
    private void OpenDiscord() => Launch.Open(Core.AppIdentity.DiscordUrl);

    [RelayCommand]
    private void EmailUs() => Launch.Open($"mailto:{Core.AppIdentity.ContactEmail}");

    [RelayCommand]
    private void OpenHelp() => Launch.Open(Core.AppIdentity.HelpUrl);

    public string ContactEmail => Core.AppIdentity.ContactEmail;

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

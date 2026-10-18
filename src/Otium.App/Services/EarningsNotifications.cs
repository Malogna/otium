using Otium.Core.Engine;
using Otium.Core.Projections;
using Otium.Providers.Proxybase;

namespace Otium.App.Services;

/// <summary>
/// Which moments are worth a desktop notification, and remembering which were already shown: a stretch of earning
/// that just ended, a payout the pool sent, internet sharing paid out, last month's report, and a pool that something
/// on this computer blocks. Off with one switch in Settings.
/// </summary>
public sealed class EarningsNotifications
{
    /// <summary>A blocked pool is mentioned at most this often, so it doesn't nag.</summary>
    public static readonly TimeSpan ProblemEvery = TimeSpan.FromHours(12);

    private readonly AppHost _host;
    private readonly Action<string, string> _show;
    private AwaySummary? _lastAway;

    public EarningsNotifications(AppHost host, Action<string, string>? show = null)
    {
        _host = host;
        _show = show ?? DesktopNotifier.Show;
        host.AwayChanged += OnAway;
        host.AccountChanged += CheckPayouts;
        host.HistoryChanged += CheckMonthly;
        foreach (var engine in host.Engines)
            engine.SnapshotChanged += CheckProblem;
    }

    private bool Enabled => _host.Settings.NotificationsEnabled && _host.Settings.OnboardingComplete;

    private void OnAway()
    {
        if (_host.LastAway is not { } away || ReferenceEquals(away, _lastAway))
            return;
        _lastAway = away;
        if (Enabled)
            _show("While you were away", $"Your PC earned {Format.Usd(away.EarnedUsd)} in {Format.Duration(away.Earning)} of earning.");
    }

    /// <summary>The pool's "paid" total went up: a payout is on its way to the user's wallet.</summary>
    internal void CheckPayouts()
    {
        var settings = _host.Settings;
        if (_host.PoolBalance is { } pool)
        {
            var key = $"{pool.PoolName}:{pool.Coin}";
            if (settings.NotifiedPaid.TryGetValue(key, out var seen) && pool.Paid > seen + 1e-12 && Enabled)
                _show("Payout on its way", $"{pool.PoolName} sent {Format.Coin(pool.Paid - seen, pool.Coin)} to your wallet.");
            if (!settings.NotifiedPaid.TryGetValue(key, out var before) || Math.Abs(before - pool.Paid) > 1e-12)
            {
                settings.NotifiedPaid[key] = pool.Paid;
                _host.SaveSettings();
            }
        }

        if (_host.Bandwidth.Provider is ProxybaseBandwidthProvider proxybase)
        {
            var payouts = proxybase.Payouts;
            if (payouts.Count > settings.NotifiedSharingPayouts)
            {
                if (Enabled)
                    _show("Internet sharing paid out", payouts[^1].ExpectedOutput is { } what
                        ? $"{what} is on its way to your wallet."
                        : "Your internet sharing earnings are on their way to your wallet.");
                settings.NotifiedSharingPayouts = payouts.Count;
                _host.SaveSettings();
            }
        }
    }

    private void CheckMonthly()
    {
        var settings = _host.Settings;
        if (MonthlyReport.Due(_host.History, DateTimeOffset.Now, settings.MonthlyReportDismissed) is not { } month || settings.MonthlyNotified == month.Month)
            return;
        settings.MonthlyNotified = month.Month;
        _host.SaveSettings();
        if (Enabled)
            _show($"{month.MonthName} in numbers", $"Your PC earned {Format.Usd(month.EarnedUsd)} last month. Open Otium to share it.");
    }

    private void CheckProblem(EngineSnapshot snapshot)
    {
        if (snapshot.Problem is not { } problem || !problem.Contains("blocks the mining pool", StringComparison.Ordinal) || !Enabled)
            return;
        var now = DateTimeOffset.UtcNow;
        if (_host.Settings.ProblemNotifiedAt is { } last && now - last < ProblemEvery)
            return;
        _host.Settings.ProblemNotifiedAt = now;
        _host.SaveSettings();
        _show("Otium can't reach the mining pool", problem);
    }
}

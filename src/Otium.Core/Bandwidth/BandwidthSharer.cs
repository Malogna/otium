using Otium.Core.Accounts;
using Otium.Core.Platform;

namespace Otium.Core.Bandwidth;

public enum BandwidthState
{
    Off,

    /// <summary>The partner SDK isn't available on this computer or in this build.</summary>
    Unavailable,
    Starting,
    Sharing,
    WaitingForIdle,

    /// <summary>The user's daily data limit is reached; sharing resumes the next day.</summary>
    LimitReached,
    Error,
}

/// <summary>Per-tick configuration, re-read every tick so settings apply without a restart.</summary>
/// <param name="Allowed">Switched on, consent given, and the account can be paid for sharing.</param>
/// <param name="DailyLimitBytes">The user's own daily cap (never above the server's).</param>
/// <param name="UserUsdPerGb">What the user receives per GB, after the fee (from the server's terms).</param>
public sealed record BandwidthConfig(
    bool Allowed,
    bool OnlyWhenIdle,
    TimeSpan IdleThreshold,
    long DailyLimitBytes,
    double UserUsdPerGb,
    double UserUsdPerSharingHour,
    string PublisherUserId);

public sealed record BandwidthSnapshot
{
    public BandwidthState State { get; init; }

    public string StatusText { get; init; } = "";

    /// <summary>Recent average speed while sharing.</summary>
    public double BytesPerSecond { get; init; }

    public long BytesToday { get; init; }

    public long TotalBytes { get; init; }

    /// <summary>What a full day of sharing at the recent pace would pay the user.</summary>
    public double UserUsdPerDay { get; init; }

    /// <summary>Credited by the server plus the estimate for usage not reported yet.</summary>
    public double TotalUserUsd { get; init; }

    /// <summary>The server's explanation when it paid less than reported (shared connection, daily limit).</summary>
    public string? Note { get; init; }

    public TimeSpan? IdleTime { get; init; }
}

/// <summary>Persistent counters, so totals and the report sequence survive restarts.</summary>
public sealed class BandwidthLedger
{
    public long NextSequence { get; set; } = 1;

    /// <summary>A report that was built but not yet acknowledged; it is retried unchanged so it is never paid twice.</summary>
    public BandwidthReport? Pending { get; set; }

    public DateTimeOffset? PeriodStart { get; set; }

    public long PeriodBytes { get; set; }

    public double PeriodSeconds { get; set; }

    public long TotalBytes { get; set; }

    public double CreditedUsd { get; set; }

    public DateOnly Day { get; set; }

    public long DayBytes { get; set; }
}

/// <summary>
/// Runs the bandwidth provider when the user has opted in: only while away if they chose so, never past their daily
/// data limit. Measured traffic is reported to the Otium server every few minutes, which credits the balance.
/// </summary>
public sealed class BandwidthSharer : IAsyncDisposable
{
    public static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan ReportInterval = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan SaveInterval = TimeSpan.FromMinutes(1);
    private const double GB = 1024d * 1024 * 1024;

    private readonly IBandwidthProvider _provider;
    private readonly IIdleDetector _idle;
    private readonly Func<BandwidthConfig> _config;
    private readonly Func<BandwidthReport, CancellationToken, Task<BandwidthReportResult>> _report;
    private readonly BandwidthLedger _ledger;
    private readonly Action<BandwidthLedger> _save;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _wake = new(0);
    private readonly CancellationTokenSource _shutdown = new();

    private volatile bool _enabled;
    private bool _running;
    private long _lastProviderBytes;
    private DateTimeOffset _lastTick;
    private DateTimeOffset _lastSave;
    private DateTimeOffset _nextReportAttempt;
    private double _bytesPerSecond;
    private string? _note;
    private Task? _loop;

    public BandwidthSharer(
        IBandwidthProvider provider,
        IIdleDetector idle,
        Func<BandwidthConfig> config,
        Func<BandwidthReport, CancellationToken, Task<BandwidthReportResult>> report,
        BandwidthLedger ledger,
        Action<BandwidthLedger> save,
        TimeProvider? time = null)
    {
        _provider = provider;
        _idle = idle;
        _config = config;
        _report = report;
        _ledger = ledger;
        _save = save;
        _time = time ?? TimeProvider.System;
        _lastTick = _lastSave = _time.GetUtcNow();
        Snapshot = new BandwidthSnapshot { State = BandwidthState.Off, StatusText = "Off" };
    }

    public event Action<BandwidthSnapshot>? SnapshotChanged;

    public BandwidthSnapshot Snapshot { get; private set; }

    public IBandwidthProvider Provider => _provider;

    public bool Enabled => _enabled;

    public void Start() => _loop ??= Task.Run(() => LoopAsync(_shutdown.Token));

    public void Enable()
    {
        _enabled = true;
        _wake.Release();
    }

    public void Disable()
    {
        _enabled = false;
        _wake.Release();
    }

    public void Nudge() => _wake.Release();

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await TickAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                await StopProviderAsync();
                Publish(BandwidthState.Error, $"Something went wrong: {e.Message}");
            }

            try
            {
                await _wake.WaitAsync(TickInterval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    internal async Task TickAsync(CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        var config = _config();
        var elapsed = now - _lastTick;
        _lastTick = now;
        RollDay(now);

        // Measure what was shared since the last tick before deciding anything else.
        if (_running)
            await MeasureAsync(elapsed, ct);
        await ReportIfDueAsync(now, force: false, ct);
        SaveIfDue(now);

        var idle = _idle.GetIdleTime();
        if (!_enabled || !config.Allowed)
        {
            await StopProviderAsync();
            Publish(BandwidthState.Off, "Off", idle);
            return;
        }
        if (_provider.UnavailableReason is { } reason)
        {
            Publish(BandwidthState.Unavailable, reason, idle);
            return;
        }
        if (_ledger.DayBytes >= config.DailyLimitBytes)
        {
            await StopProviderAsync();
            Publish(BandwidthState.LimitReached, "Today's data limit is reached. Sharing continues tomorrow.", idle);
            return;
        }
        if (config.OnlyWhenIdle && idle is { } away && away < config.IdleThreshold)
        {
            await StopProviderAsync();
            Publish(BandwidthState.WaitingForIdle, "Waiting for you to step away", idle);
            return;
        }

        if (!_running)
        {
            if (!_provider.IsInstalled)
            {
                Publish(BandwidthState.Starting, "Downloading the sharing component (one time)…", idle);
                await _provider.InstallAsync(null, ct);
            }
            await _provider.StartAsync(new BandwidthRunOptions(config.PublisherUserId), ct);
            _running = true;
            _lastProviderBytes = 0;
        }

        var status = await _provider.GetStatusAsync(ct);
        switch (status.State)
        {
            case BandwidthProviderState.Online:
                Publish(BandwidthState.Sharing, "Sharing unused bandwidth", idle);
                break;
            case BandwidthProviderState.Error:
                await StopProviderAsync();
                Publish(BandwidthState.Error, status.Message ?? "The sharing component stopped. Retrying…", idle);
                break;
            default:
                Publish(BandwidthState.Starting, "Connecting…", idle);
                break;
        }
    }

    private async Task MeasureAsync(TimeSpan elapsed, CancellationToken ct)
    {
        var status = await _provider.GetStatusAsync(ct);
        // A restarted provider counts from zero again.
        var delta = status.BytesSinceStart >= _lastProviderBytes ? status.BytesSinceStart - _lastProviderBytes : status.BytesSinceStart;
        _lastProviderBytes = status.BytesSinceStart;

        _ledger.PeriodStart ??= _time.GetUtcNow() - elapsed;
        _ledger.PeriodBytes += delta;
        // A long gap means the computer slept; that time wasn't spent online.
        if (status.State == BandwidthProviderState.Online)
            _ledger.PeriodSeconds += Math.Min(elapsed.TotalSeconds, 6 * TickInterval.TotalSeconds);
        _ledger.TotalBytes += delta;
        _ledger.DayBytes += delta;

        // Smoothed over roughly a minute, since partner demand is bursty.
        var seconds = Math.Max(elapsed.TotalSeconds, 0.001);
        var weight = Math.Min(1, seconds / 60);
        _bytesPerSecond += (delta / seconds - _bytesPerSecond) * weight;
    }

    private async Task ReportIfDueAsync(DateTimeOffset now, bool force, CancellationToken ct)
    {
        if (_ledger.Pending is null && _ledger.PeriodStart is { } start && (force || now - start >= ReportInterval))
        {
            _ledger.Pending = new BandwidthReport(_ledger.NextSequence++, start, now, _ledger.PeriodBytes, _ledger.PeriodSeconds, _provider.Id);
            _ledger.PeriodStart = null;
            _ledger.PeriodBytes = 0;
            _ledger.PeriodSeconds = 0;
            _save(_ledger);
        }
        if (_ledger.Pending is not { } pending || now < _nextReportAttempt)
            return;

        try
        {
            var result = await _report(pending, ct);
            _ledger.CreditedUsd += (double)result.CreditedUsd;
            _note = result.Note;
            _ledger.Pending = null;
            _save(_ledger);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or AccountRequestException)
        {
            // Kept and retried unchanged; the server credits each sequence number once.
            _nextReportAttempt = now + RetryInterval;
        }
    }

    private void RollDay(DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        if (_ledger.Day == today)
            return;
        _ledger.Day = today;
        _ledger.DayBytes = 0;
    }

    private void SaveIfDue(DateTimeOffset now)
    {
        if (now - _lastSave < SaveInterval)
            return;
        _lastSave = now;
        _save(_ledger);
    }

    private async Task StopProviderAsync()
    {
        if (!_running)
            return;
        _running = false;
        _bytesPerSecond = 0;
        await _provider.StopAsync(CancellationToken.None);
    }

    private void Publish(BandwidthState state, string text, TimeSpan? idle = null)
    {
        var config = _config();
        var unreported = _ledger.PeriodBytes + (_ledger.Pending?.Bytes ?? 0);
        var perDay = state == BandwidthState.Sharing
            ? _bytesPerSecond * 86_400 / GB * config.UserUsdPerGb + 24 * config.UserUsdPerSharingHour
            : 0;
        Snapshot = new BandwidthSnapshot
        {
            State = state,
            StatusText = text,
            BytesPerSecond = state == BandwidthState.Sharing ? _bytesPerSecond : 0,
            BytesToday = _ledger.DayBytes,
            TotalBytes = _ledger.TotalBytes,
            UserUsdPerDay = perDay,
            TotalUserUsd = _ledger.CreditedUsd + unreported / GB * config.UserUsdPerGb,
            Note = _note,
            IdleTime = idle,
        };
        SnapshotChanged?.Invoke(Snapshot);
    }

    public async ValueTask DisposeAsync()
    {
        await _shutdown.CancelAsync();
        if (_loop is not null)
        {
            try
            {
                await _loop;
            }
            catch (OperationCanceledException)
            {
            }
        }
        await StopProviderAsync();
        // Report what's left so the last minutes before closing are paid too.
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await ReportIfDueAsync(_time.GetUtcNow(), force: true, timeout.Token);
        }
        catch (OperationCanceledException)
        {
        }
        _save(_ledger);
        _shutdown.Dispose();
        _wake.Dispose();
    }
}

using Otium.Core.Platform;
using Otium.Core.Power;
using Otium.Core.Providers;

namespace Otium.Core.Engine;

/// <summary>
/// Owns the hardware: decides whether to run, for whom (user / referrer / app fee slice), and stops when
/// mining would cost more in electricity than it earns. One provider per device; GPU providers slot in later.
/// </summary>
public sealed class EarningsEngine : IAsyncDisposable
{
    public static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan Warmup = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan LossTolerance = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan UnprofitableRecheck = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan ErrorRetry = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan LedgerSaveInterval = TimeSpan.FromMinutes(1);

    private readonly IComputeProvider _provider;
    private readonly IPowerMeter _meter;
    private readonly IIdleDetector _idle;
    private readonly Func<EngineConfig> _config;
    private readonly EarningsLedger _ledger;
    private readonly Action<EarningsLedger> _saveLedger;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _wake = new(0);
    private readonly SemaphoreSlim _tickLock = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();

    private volatile bool _enabled;
    private ProviderRunOptions? _running;
    private DateTimeOffset _runningSince;
    private DateTimeOffset _lastTick;
    private DateTimeOffset? _lossSince;
    private DateTimeOffset? _pausedUntil;
    private DateTimeOffset? _retryAt;
    private DateTimeOffset _lastLedgerSave;
    private string? _lastError;
    private string? _lastProblem;
    private EngineBlockedException? _blocked;
    private Task? _loop;

    public EarningsEngine(
        IComputeProvider provider,
        IPowerMeter meter,
        IIdleDetector idle,
        Func<EngineConfig> config,
        EarningsLedger ledger,
        Action<EarningsLedger> saveLedger,
        TimeProvider? time = null)
    {
        _provider = provider;
        _meter = meter;
        _idle = idle;
        _config = config;
        _ledger = ledger;
        _saveLedger = saveLedger;
        _time = time ?? TimeProvider.System;
        _lastLedgerSave = _time.GetUtcNow();
        Snapshot = new EngineSnapshot { State = EngineState.Off, StatusText = "Off" };
    }

    public event Action<EngineSnapshot>? SnapshotChanged;

    public EngineSnapshot Snapshot { get; private set; }

    public bool Enabled => _enabled;

    public IComputeProvider Provider => _provider;

    public void Start() => _loop ??= Task.Run(() => LoopAsync(_shutdown.Token));

    public void Enable()
    {
        _enabled = true;
        _pausedUntil = null;
        _retryAt = null;
        _blocked = null;
        _wake.Release();
    }

    public void Disable()
    {
        _enabled = false;
        _wake.Release();
    }

    /// <summary>Re-evaluate immediately, e.g. after settings change.</summary>
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
                _lastError = e.Message;
                _lastProblem = null;
                _retryAt = _time.GetUtcNow() + ErrorRetry;
                await StopRunningAsync(CancellationToken.None);
                Publish(EngineState.Error, $"Something went wrong: {e.Message}");
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
        await _tickLock.WaitAsync(ct);
        try
        {
            await TickCoreAsync(ct);
        }
        catch (ProviderProblemException e)
        {
            // Already in plain words (e.g. "Graphics card isn't mining: …"): show it as is and retry shortly.
            _lastError = e.Message;
            _lastProblem = e.Message;
            _retryAt = _time.GetUtcNow() + ErrorRetry;
            await StopRunningAsync(CancellationToken.None);
            Publish(EngineState.Error, e.Message, problem: e.Message);
        }
        finally
        {
            _tickLock.Release();
        }
    }

    private async Task TickCoreAsync(CancellationToken ct)
    {
        var now = _time.GetUtcNow();
        var config = _config();

        if (!_enabled)
        {
            await StopRunningAsync(ct);
            Publish(EngineState.Off, "Off");
            return;
        }

        if (_blocked is not null)
        {
            Publish(EngineState.Error, "Your antivirus blocked the mining engine.", blocked: _blocked);
            return;
        }

        if (_retryAt is { } retry && now < retry)
        {
            Publish(EngineState.Error, _lastError ?? "Retrying shortly…", problem: _lastProblem);
            return;
        }

        if (!_provider.IsInstalled)
        {
            Publish(EngineState.Installing, "Downloading the mining engine…", install: new InstallProgress("Starting download", 0));
            try
            {
                var progress = new InlineProgress<InstallProgress>(p => Publish(EngineState.Installing, p.Stage, install: p));
                await _provider.InstallAsync(progress, ct);
            }
            catch (EngineBlockedException blocked)
            {
                _blocked = blocked;
                Publish(EngineState.Error, "Your antivirus blocked the mining engine.", blocked: blocked);
                return;
            }
        }

        if (config.OnlyWhenIdle)
        {
            var idle = _idle.GetIdleTime();
            if (idle is { } idleTime && idleTime < config.IdleThreshold)
            {
                await StopRunningAsync(ct);
                var wait = config.IdleThreshold - idleTime;
                Publish(EngineState.WaitingForIdle, $"Waiting until you're away ({FormatMinutes(wait)} more)", idleTime: idle);
                return;
            }
        }

        if (_running is null && !await PassesProfitGateAsync(config, now, ct))
            return;

        var desired = new ProviderRunOptions(
            config.FeeSchedule.TargetAt(_ledger.MiningTime, now),
            config.WorkerName,
            config.UsagePercent);

        if (_running is null || _running != desired)
        {
            await StopRunningAsync(ct);
            _meter.CaptureBaseline();
            await _provider.StartAsync(desired, ct);
            _running = desired;
            _runningSince = now;
            _lastTick = now;
            _lossSince = null;
            Publish(EngineState.Starting, "Starting up…");
            return;
        }

        var status = await _provider.GetStatusAsync(ct);
        if (status.State == ProviderState.Error)
        {
            if (status.Problem is { } problem)
                throw new ProviderProblemException(problem);
            throw new InvalidOperationException(status.Message ?? "The mining engine stopped unexpectedly.");
        }

        var elapsed = now - _lastTick;
        _lastTick = now;

        var power = _meter.Read(config.UsagePercent);
        var feeFraction = config.FeeSchedule.EffectiveFeePercent(now) / 100.0;
        var estimate = status.HashrateHps > 0
            ? await _provider.EstimateRevenueAsync(status.HashrateHps, ct)
            : new RevenueEstimate(0, 0, _provider.PayoutCurrency);

        var kwh = Electricity.KilowattHours(power.ExtraWatts, elapsed);
        _ledger.Record(
            _running.Target.Role,
            estimate.UsdPerDay * elapsed.TotalDays,
            kwh,
            kwh * config.PricePerKwh,
            elapsed,
            DateOnly.FromDateTime(_time.GetLocalNow().DateTime));

        // While a provider calibrates (e.g. benchmarking GPU algorithms), its numbers aren't representative.
        var warmedUp = now - _runningSince >= Warmup && !status.IsCalibrating;
        if (status.HashrateHps > 0 && warmedUp)
            _ledger.LastHashrateHps = status.HashrateHps;

        var userPerDay = estimate.UsdPerDay * (1 - feeFraction);
        var costPerDay = Electricity.CostPerDay(power.ExtraWatts, config.PricePerKwh);

        if (config.NeverRunAtLoss && warmedUp && status.HashrateHps > 0 && userPerDay < costPerDay)
        {
            _lossSince ??= now;
            if (now - _lossSince >= LossTolerance)
            {
                await StopRunningAsync(ct);
                _pausedUntil = now + UnprofitableRecheck;
                PublishUnprofitable(userPerDay, costPerDay);
                return;
            }
        }
        else
        {
            _lossSince = null;
        }

        if (now - _lastLedgerSave >= LedgerSaveInterval)
        {
            _saveLedger(_ledger);
            _lastLedgerSave = now;
        }

        var stateText = status.HashrateHps > 0
            ? RoleText(_running.Target.Role)
            : status.Message ?? "Warming up (preparing the mining dataset)…";
        Publish(
            status.HashrateHps > 0 ? EngineState.Earning : EngineState.Starting,
            stateText,
            hashrate: status.HashrateHps,
            gross: estimate.UsdPerDay,
            user: userPerDay,
            power: power,
            costPerDay: costPerDay,
            role: _running.Target.Role,
            detail: status.Detail,
            problem: status.Problem);
    }

    /// <summary>Before starting, use what the provider (or the last run) says to avoid mining at a loss.</summary>
    private async Task<bool> PassesProfitGateAsync(EngineConfig config, DateTimeOffset now, CancellationToken ct)
    {
        if (_pausedUntil is { } until && now < until)
        {
            PublishUnprofitable(Snapshot.UserUsdPerDay, Snapshot.ElectricityUsdPerDay);
            return false;
        }

        _pausedUntil = null;
        if (!config.NeverRunAtLoss)
            return true;

        double grossPerDay;
        double extraWatts;
        var offer = await _provider.GetExpectedOfferAsync(ct);
        if (offer is not null)
        {
            grossPerDay = offer.GrossUsdPerDay;
            extraWatts = offer.ExtraWatts ?? _meter.Read(config.UsagePercent).ExtraWatts;
        }
        else if (_ledger.LastHashrateHps > 0)
        {
            grossPerDay = (await _provider.EstimateRevenueAsync(_ledger.LastHashrateHps, ct)).UsdPerDay;
            extraWatts = _meter.Read(config.UsagePercent).ExtraWatts;
        }
        else
        {
            return true; // nothing measured yet: run to find out
        }

        var userPerDay = grossPerDay * (1 - config.FeeSchedule.EffectiveFeePercent(now) / 100.0);
        var costPerDay = Electricity.CostPerDay(extraWatts, config.PricePerKwh);
        if (userPerDay >= costPerDay)
            return true;

        _pausedUntil = now + UnprofitableRecheck;
        PublishUnprofitable(userPerDay, costPerDay);
        return false;
    }

    private void PublishUnprofitable(double userPerDay, double costPerDay) =>
        Publish(
            EngineState.PausedUnprofitable,
            $"Paused: electricity (${costPerDay:0.00}/day) costs more than mining earns (${userPerDay:0.00}/day). Checking again in 30 min.",
            user: userPerDay,
            costPerDay: costPerDay);

    private async Task StopRunningAsync(CancellationToken ct)
    {
        if (_running is null)
            return;
        _running = null;
        await _provider.StopAsync(ct);
        _saveLedger(_ledger);
        _lastLedgerSave = _time.GetUtcNow();
    }

    private void Publish(
        EngineState state,
        string text,
        double hashrate = 0,
        double gross = 0,
        double user = 0,
        PowerReading? power = null,
        double costPerDay = 0,
        PayoutRole role = PayoutRole.User,
        InstallProgress? install = null,
        EngineBlockedException? blocked = null,
        TimeSpan? idleTime = null,
        string? detail = null,
        string? problem = null)
    {
        var config = _config();
        var now = _time.GetUtcNow();
        Snapshot = new EngineSnapshot
        {
            State = state,
            StatusText = text,
            HashrateHps = hashrate,
            GrossUsdPerDay = gross,
            UserUsdPerDay = user,
            ExtraWatts = power?.ExtraWatts ?? 0,
            PowerMeasured = power?.IsMeasured ?? false,
            PowerSource = power?.Source ?? "",
            ElectricityUsdPerDay = costPerDay,
            CurrentRole = role,
            EffectiveFeePercent = config.FeeSchedule.EffectiveFeePercent(now),
            Install = install,
            EngineBlocked = blocked is not null,
            EngineBlockedPath = blocked?.EnginePath,
            IdleTime = idleTime,
            Detail = detail,
            Problem = problem,
            IdleDetectionAvailable = !config.OnlyWhenIdle || _idle.GetIdleTime() is not null,
            TotalUserUsd = _ledger.EstimatedUserUsd,
            TotalElectricityUsd = _ledger.ElectricityCostUsd,
            TotalEnergyKwh = _ledger.EnergyKwh,
            TodayUserUsd = _ledger.TodayUserUsd,
            TodayElectricityUsd = _ledger.TodayElectricityUsd,
            TotalMiningTime = _ledger.MiningTime,
        };
        SnapshotChanged?.Invoke(Snapshot);
    }

    private static string RoleText(PayoutRole role) => role switch
    {
        PayoutRole.Referrer => "Earning — this short slice thanks the friend who invited you",
        PayoutRole.App => "Earning — this short slice is the Otium fee",
        _ => "Earning for you",
    };

    private static string FormatMinutes(TimeSpan t) =>
        t.TotalMinutes >= 1 ? $"{Math.Ceiling(t.TotalMinutes):0} min" : $"{Math.Ceiling(t.TotalSeconds):0} s";

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

        await _tickLock.WaitAsync();
        try
        {
            await StopRunningAsync(CancellationToken.None);
        }
        finally
        {
            _tickLock.Release();
        }

        _saveLedger(_ledger);
        _shutdown.Dispose();
    }

    /// <summary>Unlike Progress&lt;T&gt;, reports synchronously so the UI sees install progress in order.</summary>
    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}

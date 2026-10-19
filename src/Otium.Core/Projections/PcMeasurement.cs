using Otium.Core.Engine;
using Otium.Core.Providers;

namespace Otium.Core.Projections;

/// <summary>What one device earned per day of mining in the test, on its best coin.</summary>
/// <param name="UserUsdPerDay">The user's share after Otium's fee, at the measured hash rate and today's prices.</param>
/// <param name="ElectricityUsdPerDay">The extra power the device drew, at the user's electricity price.</param>
public sealed record DeviceMeasurement(DeviceKind Device, string Name, string Coin, double HashrateHps, double UserUsdPerDay, double ElectricityUsdPerDay)
{
    public double NetUsdPerDay => UserUsdPerDay - ElectricityUsdPerDay;
}

/// <summary>The result of the "measure my PC" test: this computer's own numbers instead of a hardware table's.</summary>
public sealed record PcMeasurement(DateTimeOffset MeasuredAt, IReadOnlyList<DeviceMeasurement> Devices)
{
    /// <summary>
    /// Per day of continuous earning, after electricity. A device that would lose money counts as zero, since Otium
    /// doesn't run it then.
    /// </summary>
    public double NetUsdPerDay => Devices.Sum(d => Math.Max(0, d.NetUsdPerDay));

    /// <summary>Per 30-day month, earning <paramref name="hoursPerDay"/> hours a day.</summary>
    public double PerMonth(double hoursPerDay) => NetUsdPerDay * Math.Clamp(hoursPerDay, 0, 24) / 24 * 30;

    /// <summary>
    /// How many times what the processor would earn the graphics card must earn before the first test leaves the
    /// processor off: next to such a card it adds a few percent, for a warmer, louder computer (an i7-8700K next to an
    /// RTX 3080 in October 2026: $0.09 against $3.23 a day).
    /// </summary>
    public const double GpuMultipleToLeaveCpuOff = 10;

    /// <summary>
    /// The test measured both, and the graphics card earns at least <see cref="GpuMultipleToLeaveCpuOff"/> times what the
    /// processor would.
    /// </summary>
    public bool ProcessorAddsLittle
    {
        get
        {
            var gpu = Devices.Where(d => d.Device == DeviceKind.Gpu).Sum(d => Math.Max(0, d.NetUsdPerDay));
            var cpu = Devices.Where(d => d.Device == DeviceKind.Cpu).Sum(d => Math.Max(0, d.NetUsdPerDay));
            return gpu > 0 && Devices.Any(d => d.Device == DeviceKind.Cpu) && gpu >= GpuMultipleToLeaveCpuOff * cpu;
        }
    }

    /// <summary>The same result without the processor, for when it's switched off.</summary>
    public PcMeasurement WithoutProcessor() => this with { Devices = Devices.Where(d => d.Device != DeviceKind.Cpu).ToList() };
}

/// <summary>A device's latest state, as the test sees it.</summary>
/// <param name="DefaultAlgorithm">What the device mines when its snapshot doesn't say (the processor: "rx/0").</param>
public sealed record MeasuredDevice(DeviceKind Device, string Name, EngineSnapshot Snapshot, string? DefaultAlgorithm = null);

public enum MeasurementPhase
{
    /// <summary>The engines are downloading or starting.</summary>
    Preparing,
    Measuring,
    Done,
}

/// <summary>
/// The two-minute "measure my PC" test. Fed with the engines' snapshots while they earn on purpose (see
/// <see cref="EarningsEngine.RunNow"/>); it averages each device's measured rate on every coin it tried and keeps the
/// best, since that is what the device mines afterwards.
/// </summary>
public sealed class PcMeasurementRun(DateTimeOffset startedAt)
{
    /// <summary>How long the test measures once the miners are running.</summary>
    public static readonly TimeSpan Target = TimeSpan.FromMinutes(2);

    /// <summary>Each device needs this long of steady hashing on one coin to count.</summary>
    public static readonly TimeSpan MinimumSteady = TimeSpan.FromSeconds(20);

    /// <summary>When a graphics card is still testing coins, the test waits up to this long, then uses what it has.</summary>
    public static readonly TimeSpan Maximum = TimeSpan.FromMinutes(6);

    /// <summary>Downloading the engines can take a while on a slow connection, but not forever.</summary>
    public static readonly TimeSpan MaximumPreparing = TimeSpan.FromMinutes(10);

    /// <summary>How long an engine may still say "Off" after the test starts before it counts as not running.</summary>
    public static readonly TimeSpan OffGrace = TimeSpan.FromSeconds(30);

    /// <summary>How long the engines are told to keep earning while the user is at the computer.</summary>
    public static TimeSpan RunNowWindow => MaximumPreparing + Maximum + TimeSpan.FromMinutes(1);

    private readonly Dictionary<(DeviceKind, string), Device> _devices = [];
    private DateTimeOffset? _measuringSince;
    private DateTimeOffset _now = startedAt;

    public DateTimeOffset StartedAt { get; } = startedAt;

    public MeasurementPhase Phase { get; private set; } = MeasurementPhase.Preparing;

    /// <summary>0–1, for a progress bar. Holds just short of 1 while waiting for a device to settle.</summary>
    public double Progress => Phase switch
    {
        MeasurementPhase.Done => 1,
        MeasurementPhase.Measuring when _measuringSince is { } since => Math.Min(0.97, (_now - since) / Target),
        _ => 0,
    };

    /// <summary>Time left in the measuring phase, for "about a minute left".</summary>
    public TimeSpan Remaining => _measuringSince is { } since ? Target - (_now - since) : Target;

    /// <summary>Why the test couldn't measure anything, when it ended without a result.</summary>
    public string? Failure { get; private set; }

    public void Observe(DateTimeOffset now, IReadOnlyList<MeasuredDevice> devices)
    {
        if (Phase == MeasurementPhase.Done)
            return;
        _now = now;

        foreach (var d in devices)
        {
            var s = d.Snapshot;
            if (!_devices.TryGetValue((d.Device, d.Name), out var device))
                _devices[(d.Device, d.Name)] = device = new Device(d.Device, d.Name);
            device.State = s.State;
            device.LastSnapshot = s;
            if (s.State is EngineState.Starting or EngineState.Earning)
                _measuringSince ??= now;
            if (s.State == EngineState.Earning && s.HashrateHps > 0)
                device.Add(s.Detail ?? d.DefaultAlgorithm ?? "", now, s.HashrateHps, s.UserUsdPerDay, s.ElectricityUsdPerDay);
        }

        if (_measuringSince is not { } measuringSince)
        {
            if (now - StartedAt >= MaximumPreparing)
                Finish("The miners didn't start in time, so nothing was measured.");
            // Nothing will run: report what the engines said (e.g. "would cost more than it earns"). Engines still say
            // "Off" for a moment after Start, until their first update, so that only counts after a short grace period.
            else if (_devices.Count > 0 && _devices.Values.All(x => x.State is EngineState.Error or EngineState.PausedUnprofitable
                         || (x.State == EngineState.Off && now - StartedAt >= OffGrace)))
                Finish(null);
            return;
        }

        Phase = MeasurementPhase.Measuring;
        var measuring = now - measuringSince;
        // A device that won't run (electricity costs more, an error) doesn't hold the test up.
        var waitingFor = _devices.Values.Where(x => x.State is EngineState.Starting or EngineState.Earning or EngineState.Installing);
        if ((measuring >= Target && waitingFor.All(x => x.SteadyFor >= MinimumSteady)) || measuring >= Maximum)
            Finish(null);
    }

    /// <summary>Ends the test early (the user stopped it): the result is whatever was measured so far.</summary>
    public void Stop() => Finish(null);

    private void Finish(string? failure)
    {
        Phase = MeasurementPhase.Done;
        Failure = failure;
    }

    /// <summary>The measured numbers, or null when no device measured anything.</summary>
    public PcMeasurement? Result()
    {
        var devices = new List<DeviceMeasurement>();
        foreach (var device in _devices.Values)
        {
            if (device.Best() is { } best)
                devices.Add(new DeviceMeasurement(device.Kind, device.Name, Coins.ForAlgorithm(best.Algorithm).Name, best.Hashrate, best.User, best.Electricity));
            else if (device.LastSnapshot is { State: EngineState.PausedUnprofitable } paused)
                // It wouldn't pay at this electricity price: what the engine estimated, so the result says so.
                devices.Add(new DeviceMeasurement(device.Kind, device.Name, "", 0, paused.UserUsdPerDay, paused.ElectricityUsdPerDay));
        }
        return devices.Count == 0 ? null : new PcMeasurement(_now, devices);
    }

    private sealed class Device(DeviceKind kind, string name)
    {
        private readonly Dictionary<string, List<(DateTimeOffset At, double Hashrate, double User, double Electricity)>> _samples = [];

        public DeviceKind Kind { get; } = kind;

        public string Name { get; } = name;

        public EngineState State { get; set; }

        public EngineSnapshot? LastSnapshot { get; set; }

        public void Add(string algorithm, DateTimeOffset at, double hashrate, double user, double electricity)
        {
            if (!_samples.TryGetValue(algorithm, out var list))
                _samples[algorithm] = list = [];
            list.Add((at, hashrate, user, electricity));
        }

        /// <summary>The longest steady stretch on any one coin.</summary>
        public TimeSpan SteadyFor => _samples.Values.Select(l => l[^1].At - l[0].At).DefaultIfEmpty(TimeSpan.Zero).Max();

        /// <summary>The coin with the best average after electricity, from at least two readings.</summary>
        public (string Algorithm, double Hashrate, double User, double Electricity)? Best()
        {
            var options = _samples
                .Where(kv => kv.Value.Count >= 2)
                .Select(kv => (Algorithm: kv.Key, Hashrate: kv.Value.Average(x => x.Hashrate), User: kv.Value.Average(x => x.User), Electricity: kv.Value.Average(x => x.Electricity)))
                .ToList();
            return options.Count == 0 ? null : options.MaxBy(o => o.User - o.Electricity);
        }
    }
}

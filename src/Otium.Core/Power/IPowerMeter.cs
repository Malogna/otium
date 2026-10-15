namespace Otium.Core.Power;

public sealed record PowerReading(double ExtraWatts, bool IsMeasured, string Source);

/// <summary>Reports how many watts mining adds on top of what the machine would draw anyway.</summary>
public interface IPowerMeter
{
    /// <summary>Called right before mining starts, so measured meters can record an idle baseline.</summary>
    void CaptureBaseline();

    PowerReading Read(int cpuUsagePercent);
}

/// <summary>Model-only meter: estimates from CPU usage. The model is re-read each time so settings changes apply.</summary>
public sealed class EstimatedCpuPowerMeter(Func<CpuPowerModel> model) : IPowerMeter
{
    public EstimatedCpuPowerMeter(CpuPowerModel model)
        : this(() => model)
    {
    }

    public void CaptureBaseline()
    {
    }

    public PowerReading Read(int cpuUsagePercent) =>
        new(model().ExtraWatts(cpuUsagePercent), IsMeasured: false, "Estimated from CPU usage");
}

/// <summary>
/// Measures CPU package power on Linux through the RAPL energy counter, when the user has granted read access
/// (it has been root-only by default since kernel 5.10). Falls back to the model when unreadable.
/// </summary>
public sealed class RaplPowerMeter : IPowerMeter
{
    private const string EnergyPath = "/sys/class/powercap/intel-rapl:0/energy_uj";
    private const string MaxRangePath = "/sys/class/powercap/intel-rapl:0/max_energy_range_uj";

    private readonly EstimatedCpuPowerMeter _fallback;
    private readonly TimeProvider _time;
    private long? _lastEnergyUj;
    private long _lastTimestamp;
    private double? _baselineWatts;
    private double? _lastWatts;

    public RaplPowerMeter(Func<CpuPowerModel> fallbackModel, TimeProvider? time = null)
    {
        _fallback = new EstimatedCpuPowerMeter(fallbackModel);
        _time = time ?? TimeProvider.System;
    }

    public static bool IsAvailable()
    {
        if (!OperatingSystem.IsLinux())
            return false;
        try
        {
            return long.TryParse(File.ReadAllText(EnergyPath).Trim(), out _);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void CaptureBaseline()
    {
        // Two samples ~1s apart give the idle package power.
        SampleWatts();
        Thread.Sleep(1000);
        _baselineWatts = SampleWatts();
    }

    public PowerReading Read(int cpuUsagePercent)
    {
        var watts = SampleWatts() ?? _lastWatts;
        if (watts is null || _baselineWatts is null)
            return _fallback.Read(cpuUsagePercent);

        return new PowerReading(Math.Max(0, watts.Value - _baselineWatts.Value), IsMeasured: true, "Measured (RAPL)");
    }

    private double? SampleWatts()
    {
        long energy;
        try
        {
            energy = long.Parse(File.ReadAllText(EnergyPath).Trim());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException)
        {
            return null;
        }

        var now = _time.GetTimestamp();
        double? watts = null;
        if (_lastEnergyUj is { } previous)
        {
            var delta = energy - previous;
            if (delta < 0 && long.TryParse(SafeRead(MaxRangePath), out var range))
                delta += range; // counter wrapped
            var seconds = _time.GetElapsedTime(_lastTimestamp, now).TotalSeconds;
            if (seconds > 0.2 && delta >= 0)
                watts = delta / 1_000_000.0 / seconds;
        }

        _lastEnergyUj = energy;
        _lastTimestamp = now;
        if (watts is not null)
            _lastWatts = watts;
        return watts;
    }

    private static string SafeRead(string path)
    {
        try
        {
            return File.ReadAllText(path).Trim();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }
}

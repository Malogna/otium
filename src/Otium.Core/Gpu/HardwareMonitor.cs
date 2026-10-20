using System.Globalization;
using System.Runtime.InteropServices;

namespace Otium.Core.Gpu;

/// <summary>The processor right now: how busy it is overall (all apps, miner included) and how warm, where it can be read.</summary>
public sealed record CpuStats(double? LoadPercent, double? TemperatureC)
{
    public string? Describe() => Join(
        LoadPercent is { } load ? FormattableString.Invariant($"{load:0}% load") : null,
        TemperatureC is { } temp ? FormattableString.Invariant($"{temp:0} °C") : null);

    internal static string? Join(params string?[] parts) =>
        parts.Where(p => p is not null).ToList() is { Count: > 0 } kept ? string.Join(" · ", kept) : null;
}

/// <summary>One graphics card right now, as far as its driver reports it.</summary>
public sealed record GpuStats(
    string Name,
    double? TemperatureC,
    double? LoadPercent,
    double? FanPercent,
    double? Watts,
    double? ClockMhz,
    double? MemoryUsedGb,
    double? MemoryTotalGb)
{
    public string? Describe() => CpuStats.Join(
        TemperatureC is { } temp ? FormattableString.Invariant($"{temp:0} °C") : null,
        LoadPercent is { } load ? FormattableString.Invariant($"{load:0}% load") : null,
        FanPercent is { } fan ? FormattableString.Invariant($"fan {fan:0}%") : null,
        Watts is { } watts ? FormattableString.Invariant($"{watts:0} W") : null,
        ClockMhz is { } mhz ? string.Create(CultureInfo.InvariantCulture, $"{mhz:N0} MHz") : null,
        MemoryUsedGb is { } used && MemoryTotalGb is { } total ? FormattableString.Invariant($"{used:0.0} of {total:0} GB memory") : null);
}

/// <summary>
/// Reads processor and graphics card stats for Details: load and temperature, and for graphics cards fan, power, clock
/// and memory. NVIDIA cards through NVML; AMD cards through their Linux sensors (Windows has no reader without AMD's
/// own library); processor load from the system's CPU times. The processor's temperature is read only on Linux:
/// Windows exposes it only to a kernel driver, which antivirus tools flag and Windows 11 may block.
/// </summary>
public sealed class HardwareMonitor(Nvml? nvml)
{
    private (ulong Idle, ulong Total)? _lastCpu;

    public CpuStats ReadCpu() => new(ReadCpuLoad(), OperatingSystem.IsLinux() ? ReadLinuxCpuTemperature() : null);

    public IReadOnlyList<GpuStats> ReadGpus(IReadOnlyList<GpuInfo> gpus) =>
        gpus.Select(g => g.Vendor == GpuVendor.Nvidia
                ? nvml?.ReadStats(g.Index) is { } stats ? stats with { Name = g.Name } : new GpuStats(g.Name, null, null, null, null, null, null, null)
                : ReadAmd(g))
            .ToList();

    // ---------- processor load ----------

    /// <summary>Busy share of all cores since the previous reading (null on the first one).</summary>
    private double? ReadCpuLoad()
    {
        var now = OperatingSystem.IsWindows() ? WindowsCpuTimes() : OperatingSystem.IsLinux() ? LinuxCpuTimes() : null;
        if (now is not { } current)
            return null;
        var previous = _lastCpu;
        _lastCpu = current;
        if (previous is not { } before || current.Total <= before.Total)
            return null;
        var idle = (double)(current.Idle - before.Idle);
        var total = (double)(current.Total - before.Total);
        return Math.Clamp(100 * (1 - idle / total), 0, 100);
    }

    private static (ulong Idle, ulong Total)? WindowsCpuTimes()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
            return null;
        // Kernel time includes idle time.
        return (idle, kernel + user);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out ulong idleTime, out ulong kernelTime, out ulong userTime);

    private static (ulong Idle, ulong Total)? LinuxCpuTimes()
    {
        try
        {
            var line = File.ReadLines("/proc/stat").FirstOrDefault(l => l.StartsWith("cpu ", StringComparison.Ordinal));
            var values = line?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(v => ulong.TryParse(v, out var n) ? n : 0).ToList();
            if (values is not { Count: >= 5 })
                return null;
            var idle = values[3] + values[4]; // idle + iowait
            return (idle, values.Aggregate(0UL, (a, b) => a + b));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // ---------- Linux sensors ----------

    /// <summary>The processor package temperature from k10temp (AMD), coretemp (Intel) or zenpower.</summary>
    private static double? ReadLinuxCpuTemperature()
    {
        try
        {
            foreach (var hwmon in Directory.EnumerateDirectories("/sys/class/hwmon"))
            {
                var name = ReadText(Path.Combine(hwmon, "name"));
                if (name is not ("k10temp" or "coretemp" or "zenpower"))
                    continue;
                // The first sensor is Tctl (k10temp) or "Package id 0" (coretemp): the package as a whole.
                if (ReadNumber(Path.Combine(hwmon, "temp1_input")) is { } milli)
                    return milli / 1000;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
        return null;
    }

    /// <summary>An AMD card's load, temperature and fan from its sysfs folder (Linux), found by PCI address.</summary>
    private static GpuStats ReadAmd(GpuInfo gpu)
    {
        var empty = new GpuStats(gpu.Name, null, null, null, null, null, null, gpu.MemoryGb > 0 ? gpu.MemoryGb : null);
        if (!OperatingSystem.IsLinux() || gpu.PciBus is not { } bus)
            return empty;
        try
        {
            var device = Directory.EnumerateDirectories("/sys/bus/pci/devices")
                .FirstOrDefault(d => GpuInfo.NormalizePciBus(Path.GetFileName(d)) == bus);
            if (device is null)
                return empty;
            var hwmon = Directory.Exists(Path.Combine(device, "hwmon"))
                ? Directory.EnumerateDirectories(Path.Combine(device, "hwmon")).FirstOrDefault()
                : null;
            double? fan = null;
            if (hwmon is not null && ReadNumber(Path.Combine(hwmon, "pwm1")) is { } pwm && ReadNumber(Path.Combine(hwmon, "pwm1_max")) is > 0 and var max)
                fan = 100 * pwm / max;
            var used = ReadNumber(Path.Combine(device, "mem_info_vram_used"));
            return empty with
            {
                LoadPercent = ReadNumber(Path.Combine(device, "gpu_busy_percent")),
                TemperatureC = hwmon is null ? null : ReadNumber(Path.Combine(hwmon, "temp1_input")) / 1000,
                FanPercent = fan,
                Watts = hwmon is null ? null : (ReadNumber(Path.Combine(hwmon, "power1_average")) ?? ReadNumber(Path.Combine(hwmon, "power1_input"))) / 1_000_000,
                MemoryUsedGb = used / 1024 / 1024 / 1024,
            };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return empty;
        }
    }

    private static string? ReadText(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static double? ReadNumber(string path) =>
        double.TryParse(ReadText(path), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : null;
}

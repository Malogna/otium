using Otium.Core.Power;

namespace Otium.Core.Gpu;

/// <summary>
/// Extra GPU watts while mining. Measured through NVML for NVIDIA, or the amdgpu hwmon sensor on Linux;
/// otherwise estimated from the user's figure (or a conservative default).
/// </summary>
public sealed class GpuPowerMeter : IPowerMeter
{
    public const double DefaultEstimatedWatts = 180;

    private readonly Nvml? _nvml;
    private readonly Func<double?> _userWatts;
    private readonly int _gpuCount;
    private double? _baseline;

    public GpuPowerMeter(Nvml? nvml, Func<double?> userWatts, int gpuCount)
    {
        _nvml = nvml;
        _userWatts = userWatts;
        _gpuCount = Math.Max(1, gpuCount);
    }

    public bool CanMeasure => ReadTotalWatts() is not null;

    public void CaptureBaseline()
    {
        // Keep the lowest reading ever seen: right after the miner stops the card is still spinning down,
        // and a too-high baseline would make mining look cheaper than it is.
        if (ReadTotalWatts() is { } watts)
            _baseline = _baseline is { } previous ? Math.Min(previous, watts) : watts;
    }

    public PowerReading Read(int usagePercent)
    {
        if (ReadTotalWatts() is { } watts)
        {
            _baseline ??= watts;
            return new PowerReading(Math.Max(0, watts - _baseline.Value), IsMeasured: true, _nvml is not null ? "Measured (NVML)" : "Measured (hwmon)");
        }

        var userWatts = _userWatts();
        var estimate = userWatts is > 0 ? userWatts.Value : DefaultEstimatedWatts * _gpuCount;
        return new PowerReading(estimate, IsMeasured: false, "Estimated");
    }

    private double? ReadTotalWatts() => _nvml?.ReadTotalWatts() ?? ReadAmdHwmonWatts();

    private static double? ReadAmdHwmonWatts()
    {
        if (!OperatingSystem.IsLinux() || !Directory.Exists("/sys/class/drm"))
            return null;

        double total = 0;
        var any = false;
        foreach (var hwmon in Directory.EnumerateDirectories("/sys/class/drm", "card*")
                     .Select(card => Path.Combine(card, "device", "hwmon"))
                     .Where(Directory.Exists)
                     .SelectMany(Directory.EnumerateDirectories))
        {
            foreach (var file in new[] { "power1_average", "power1_input" })
            {
                try
                {
                    var path = Path.Combine(hwmon, file);
                    if (File.Exists(path) && long.TryParse(File.ReadAllText(path).Trim(), out var microwatts))
                    {
                        total += microwatts / 1_000_000.0;
                        any = true;
                        break;
                    }
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
        return any ? total : null;
    }
}

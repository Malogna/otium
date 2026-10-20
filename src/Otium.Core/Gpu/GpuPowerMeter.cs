using Otium.Core.Power;

namespace Otium.Core.Gpu;

/// <summary>
/// Extra GPU watts while mining, for every card together. NVIDIA cards are measured through NVML and AMD cards through
/// their own hwmon sensor on Linux; an AMD card Windows can't measure adds an estimate, so a computer with both kinds
/// never counts the AMD card's electricity as free. With nothing measurable, the user's figure for all their cards (or
/// a conservative default per card) stands.
/// </summary>
public sealed class GpuPowerMeter : IPowerMeter
{
    public const double DefaultEstimatedWatts = 180;

    private readonly Nvml? _nvml;
    private readonly Func<double?> _userWatts;
    private readonly int _gpuCount;
    private readonly IReadOnlyList<GpuInfo> _gpus;
    private double? _baseline;

    public GpuPowerMeter(Nvml? nvml, Func<double?> userWatts, IReadOnlyList<GpuInfo> gpus)
    {
        _nvml = nvml;
        _userWatts = userWatts;
        _gpus = gpus;
        _gpuCount = Math.Max(1, gpus.Count);
    }

    /// <summary>AMD cards no sensor here reads (Windows), each counted at the default estimate on top of what's measured.</summary>
    private int UnmeasuredAmdCards =>
        _gpus.Count(g => g.Vendor == GpuVendor.Amd) - (OperatingSystem.IsLinux() ? AmdHwmonPaths().Count() : 0);

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
            var extra = Math.Max(0, watts - _baseline.Value);
            var unmeasured = Math.Max(0, UnmeasuredAmdCards);
            return unmeasured == 0
                ? new PowerReading(extra, IsMeasured: true, _nvml is not null ? "Measured (NVML)" : "Measured (hwmon)")
                : new PowerReading(extra + unmeasured * DefaultEstimatedWatts, IsMeasured: false, "Measured, plus an estimate for the AMD card");
        }

        var userWatts = _userWatts();
        var estimate = userWatts is > 0 ? userWatts.Value : DefaultEstimatedWatts * _gpuCount;
        return new PowerReading(estimate, IsMeasured: false, "Estimated");
    }

    /// <summary>NVIDIA's measured watts and AMD's (Linux), added together; null when neither can be read.</summary>
    private double? ReadTotalWatts()
    {
        var nvidia = _gpus.Any(g => g.Vendor == GpuVendor.Nvidia) || _gpus.Count == 0 ? _nvml?.ReadTotalWatts() : null;
        var amd = ReadAmdHwmonWatts();
        return nvidia is null && amd is null ? null : (nvidia ?? 0) + (amd ?? 0);
    }

    /// <summary>
    /// The hwmon folders of the AMD cards being counted (matched by PCI address, so the processor's built-in graphics,
    /// whose sensor can include the processor's own power, isn't read).
    /// </summary>
    private IEnumerable<string> AmdHwmonPaths()
    {
        if (!OperatingSystem.IsLinux() || !Directory.Exists("/sys/class/drm"))
            return [];
        var buses = _gpus.Where(g => g.Vendor == GpuVendor.Amd && g.PciBus is not null).Select(g => g.PciBus!).ToHashSet();
        return Directory.EnumerateDirectories("/sys/class/drm", "card*")
            .Where(card => !Path.GetFileName(card).Contains('-'))
            .Where(card =>
            {
                try
                {
                    var target = Directory.ResolveLinkTarget(Path.Combine(card, "device"), returnFinalTarget: true)?.FullName;
                    return GpuInfo.NormalizePciBus(Path.GetFileName(target)) is { } bus && buses.Contains(bus);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    return false;
                }
            })
            .Select(card => Path.Combine(card, "device", "hwmon"))
            .Where(Directory.Exists)
            .SelectMany(Directory.EnumerateDirectories)
            .ToList();
    }

    private double? ReadAmdHwmonWatts()
    {
        double total = 0;
        var any = false;
        foreach (var hwmon in AmdHwmonPaths())
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

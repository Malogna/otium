using System.Runtime.InteropServices;

namespace Otium.Core.Power;

/// <summary>
/// Estimates the extra watts the CPU draws while mining. Used where no unprivileged power sensor exists
/// (Windows, macOS, and Linux without RAPL access). Users can override the full-load figure in settings.
/// </summary>
public sealed record CpuPowerModel(double FullLoadWatts, double IdleWatts)
{
    public static CpuPowerModel ForThisMachine(double? fullLoadOverride = null)
    {
        var threads = Environment.ProcessorCount;
        var appleSilicon = OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

        // Rough defaults: Apple Silicon is far more efficient than x86 desktop/laptop parts.
        var (perThread, baseline, idle) = appleSilicon ? (1.5, 5.0, 2.0) : (4.5, 25.0, 10.0);
        var fullLoad = fullLoadOverride is > 0 ? fullLoadOverride.Value : Math.Min(250, baseline + perThread * threads);
        return new CpuPowerModel(fullLoad, Math.Min(idle, fullLoad));
    }

    /// <summary>Extra watts above idle when mining at the given share of CPU capacity.</summary>
    public double ExtraWatts(int cpuUsagePercent) =>
        Math.Max(0, FullLoadWatts - IdleWatts) * Math.Clamp(cpuUsagePercent, 0, 100) / 100.0;
}

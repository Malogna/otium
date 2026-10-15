using System.Globalization;
using Otium.Core.Platform;

namespace Otium.Core.Gpu;

public enum GpuVendor
{
    Nvidia,
    Amd,
    Intel,
}

public sealed record GpuInfo(GpuVendor Vendor, int Index, string Name, double MemoryGb)
{
    public string Describe() => MemoryGb > 0 ? $"{Name} ({MemoryGb:0} GB)" : Name;
}

public interface IGpuDetector
{
    IReadOnlyList<GpuInfo> Detect();
}

/// <summary>
/// Finds mining-capable GPUs: NVIDIA through NVML (Windows and Linux), AMD through sysfs on Linux and
/// WMI on Windows. Intel GPUs are listed only so the UI can say they aren't supported for mining yet.
/// </summary>
public sealed class GpuDetector(Nvml? nvml) : IGpuDetector
{
    private const string AmdPciVendor = "0x1002";

    public IReadOnlyList<GpuInfo> Detect()
    {
        var gpus = new List<GpuInfo>();
        if (nvml is not null)
            gpus.AddRange(nvml.ListDevices());

        if (OperatingSystem.IsLinux())
            gpus.AddRange(DetectAmdLinux());
        else if (OperatingSystem.IsWindows())
            gpus.AddRange(DetectAmdWindows());

        return gpus;
    }

    private static IEnumerable<GpuInfo> DetectAmdLinux()
    {
        const string drm = "/sys/class/drm";
        if (!Directory.Exists(drm))
            yield break;

        var index = 0;
        foreach (var card in Directory.EnumerateDirectories(drm, "card*").Where(d => !Path.GetFileName(d).Contains('-')).Order())
        {
            var device = Path.Combine(card, "device");
            if (ReadTrimmed(Path.Combine(device, "vendor")) != AmdPciVendor)
                continue;
            var vramBytes = long.TryParse(ReadTrimmed(Path.Combine(device, "mem_info_vram_total")), out var v) ? v : 0;
            yield return new GpuInfo(GpuVendor.Amd, index++, $"AMD Radeon ({Path.GetFileName(card)})", vramBytes / 1024.0 / 1024 / 1024);
        }
    }

    /// <summary>
    /// Reads the display adapter class key in the registry: instant (unlike WMI) and, unlike WMI's AdapterRAM,
    /// reports VRAM above 4 GB via HardwareInformation.qwMemorySize.
    /// </summary>
    private static IEnumerable<GpuInfo> DetectAmdWindows()
    {
        if (!OperatingSystem.IsWindows())
            return [];

        const string displayClass = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
        var result = new List<GpuInfo>();
        try
        {
            using var root = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(displayClass);
            if (root is null)
                return result;

            foreach (var name in root.GetSubKeyNames().Where(n => n.All(char.IsAsciiDigit)).Order())
            {
                try
                {
                    using var adapter = root.OpenSubKey(name);
                    var provider = adapter?.GetValue("ProviderName") as string ?? "";
                    if (!provider.Contains("Advanced Micro Devices", StringComparison.OrdinalIgnoreCase) && !provider.Equals("AMD", StringComparison.OrdinalIgnoreCase))
                        continue;
                    var description = adapter?.GetValue("DriverDesc") as string ?? "AMD Radeon";
                    var memory = adapter?.GetValue("HardwareInformation.qwMemorySize") switch
                    {
                        long l => l,
                        byte[] b when b.Length >= 8 => BitConverter.ToInt64(b, 0),
                        _ => 0L,
                    };
                    result.Add(new GpuInfo(GpuVendor.Amd, result.Count, description, memory / 1024.0 / 1024 / 1024));
                }
                catch (System.Security.SecurityException)
                {
                    // Some adapter subkeys are locked down; skip them.
                }
            }
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
        }
        return result;
    }

    private static string? ReadTrimmed(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim().ToLower(CultureInfo.InvariantCulture) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

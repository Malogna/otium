using System.Globalization;
using Otium.Core.Platform;

namespace Otium.Core.Gpu;

public enum GpuVendor
{
    Nvidia,
    Amd,
    Intel,
}

/// <param name="PciBus">The card's PCI address as "bus:device.function" in hex ("01:00.0"), or null when unknown. The
/// miners number cards their own way, so a card is matched to SRBMiner's numbering by this, never by list position.</param>
public sealed record GpuInfo(GpuVendor Vendor, int Index, string Name, double MemoryGb, string? PciBus = null)
{
    public string Describe() => MemoryGb > 0 ? $"{Name} ({MemoryGb:0} GB)" : Name;

    /// <summary>
    /// The cards in a few words: "NVIDIA GeForce RTX 3080", "2× NVIDIA GeForce RTX 3080",
    /// "NVIDIA GeForce RTX 3080 + AMD Radeon RX 6700 XT", or "3 graphics cards"; null without any.
    /// </summary>
    public static string? NamesOf(IReadOnlyList<GpuInfo> gpus)
    {
        if (gpus.Count == 0)
            return null;
        var groups = gpus.GroupBy(g => g.Name).ToList();
        if (groups.Count == 1)
            return gpus.Count == 1 ? gpus[0].Name : $"{gpus.Count}× {gpus[0].Name}";
        return groups.Count == 2 && gpus.Count == 2 ? $"{gpus[0].Name} + {gpus[1].Name}" : $"{gpus.Count} graphics cards";
    }

    /// <summary>The model reported in opt-in earnings statistics: one card's, or "multi:N" for several (kept out of per-card figures).</summary>
    public static string? ReportedModel(IReadOnlyList<GpuInfo> gpus) =>
        gpus.Count == 1 ? gpus[0].Name : gpus.Count > 1 ? $"multi:{gpus.Count}" : null;

    /// <summary>"01:00.0" from any of "00000000:01:00.0" (NVML), "0000:01:00.0" (SRBMiner, Linux) or "PCI bus 1, device 0, function 0" (Windows).</summary>
    public static string? NormalizePciBus(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var t = text.Trim().TrimEnd('\0').ToLowerInvariant();
        var words = System.Text.RegularExpressions.Regex.Match(t, @"pci bus (\d+), device (\d+), function (\d+)");
        if (words.Success)
            return $"{int.Parse(words.Groups[1].Value):x2}:{int.Parse(words.Groups[2].Value):x2}.{int.Parse(words.Groups[3].Value):x}";
        var hex = System.Text.RegularExpressions.Regex.Match(t, @"([0-9a-f]{1,2}):([0-9a-f]{1,2})\.([0-7])$");
        return hex.Success
            ? $"{Convert.ToInt32(hex.Groups[1].Value, 16):x2}:{Convert.ToInt32(hex.Groups[2].Value, 16):x2}.{hex.Groups[3].Value}"
            : null;
    }
}

public interface IGpuDetector
{
    IReadOnlyList<GpuInfo> Detect();
}

/// <summary>
/// Finds mining-capable GPUs: NVIDIA through NVML (Windows and Linux), AMD through sysfs on Linux and the registry
/// on Windows. Graphics built into the processor (most Ryzen processors have it) and AMD cards no longer in the computer
/// are left out: they can't mine, and counting them would block coins for the real card and inflate estimates.
/// </summary>
public sealed class GpuDetector(Nvml? nvml) : IGpuDetector
{
    private const string AmdPciVendor = "0x1002";

    /// <summary>
    /// Below this much graphics memory an AMD adapter is the processor's built-in graphics (a slice of system memory,
    /// typically 0.5–2 GB) rather than a graphics card; no coin Otium mines fits in it anyway.
    /// </summary>
    public const double MinDiscreteMemoryGb = 3;

    /// <summary>An AMD adapter worth mining on: a card with its own memory (unknown memory counts, to be safe).</summary>
    public static bool IsMiningCard(GpuInfo gpu) => gpu.Vendor != GpuVendor.Amd || gpu.MemoryGb <= 0 || gpu.MemoryGb >= MinDiscreteMemoryGb;

    public IReadOnlyList<GpuInfo> Detect()
    {
        var gpus = new List<GpuInfo>();
        if (nvml is not null)
            gpus.AddRange(nvml.ListDevices());

        if (OperatingSystem.IsLinux())
            gpus.AddRange(DetectAmdLinux().Where(IsMiningCard));
        else if (OperatingSystem.IsWindows())
            gpus.AddRange(DetectAmdWindows().Where(IsMiningCard));

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
            string? bus = null;
            try
            {
                bus = GpuInfo.NormalizePciBus(Path.GetFileName(Directory.ResolveLinkTarget(device, returnFinalTarget: true)?.FullName));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
            yield return new GpuInfo(GpuVendor.Amd, index++, $"AMD Radeon ({Path.GetFileName(card)})", vramBytes / 1024.0 / 1024 / 1024, bus);
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

        const string displayClassGuid = "{4d36e968-e325-11ce-bfc1-08002be10318}";
        const string displayClass = @"SYSTEM\CurrentControlSet\Control\Class\" + displayClassGuid;
        var result = new List<GpuInfo>();
        try
        {
            using var root = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(displayClass);
            if (root is null)
                return result;
            // Windows keeps a driver entry for every AMD card that was ever installed: only the ones present count.
            var present = PresentAmdAdapters(displayClassGuid);

            foreach (var name in root.GetSubKeyNames().Where(n => n.All(char.IsAsciiDigit)).Order())
            {
                try
                {
                    using var adapter = root.OpenSubKey(name);
                    var provider = adapter?.GetValue("ProviderName") as string ?? "";
                    if (!provider.Contains("Advanced Micro Devices", StringComparison.OrdinalIgnoreCase) && !provider.Equals("AMD", StringComparison.OrdinalIgnoreCase))
                        continue;
                    string? bus = null;
                    if (present is not null)
                    {
                        if (!present.TryGetValue(name, out bus))
                            continue;
                    }
                    var description = adapter?.GetValue("DriverDesc") as string ?? "AMD Radeon";
                    var memory = adapter?.GetValue("HardwareInformation.qwMemorySize") switch
                    {
                        long l => l,
                        byte[] b when b.Length >= 8 => BitConverter.ToInt64(b, 0),
                        _ => 0L,
                    };
                    result.Add(new GpuInfo(GpuVendor.Amd, result.Count, description, memory / 1024.0 / 1024 / 1024, bus));
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

    /// <summary>
    /// The display-class entries ("0001") of AMD adapters plugged in right now, with their PCI address; null when that
    /// can't be read (then every entry counts, as before). Device instances live under Enum\PCI\VEN_1002&amp;…, each naming
    /// its driver entry; Windows keeps instances of removed cards too, so each is checked with the device manager.
    /// </summary>
    private static Dictionary<string, string?>? PresentAmdAdapters(string displayClassGuid)
    {
        if (!OperatingSystem.IsWindows())
            return null;
        try
        {
            using var pci = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\PCI");
            if (pci is null)
                return null;
            var present = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var hardware in pci.GetSubKeyNames().Where(n => n.StartsWith("VEN_1002", StringComparison.OrdinalIgnoreCase)))
            {
                using var hardwareKey = pci.OpenSubKey(hardware);
                if (hardwareKey is null)
                    continue;
                foreach (var instance in hardwareKey.GetSubKeyNames())
                {
                    using var instanceKey = hardwareKey.OpenSubKey(instance);
                    if (instanceKey?.GetValue("Driver") is not string driver
                        || !driver.StartsWith(displayClassGuid + "\\", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (CM_Locate_DevNodeW(out _, $@"PCI\{hardware}\{instance}", 0) != 0)
                        continue; // not plugged in now
                    present[driver[(displayClassGuid.Length + 1)..]] = GpuInfo.NormalizePciBus(instanceKey.GetValue("LocationInformation") as string);
                }
            }
            return present;
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException or DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    /// <summary>CR_SUCCESS (0) when the device instance is present (CM_LOCATE_DEVNODE_NORMAL only finds present devices).</summary>
    [System.Runtime.InteropServices.DllImport("cfgmgr32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int CM_Locate_DevNodeW(out uint devInst, string deviceId, uint flags);

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

namespace Otium.Core.Platform;

/// <summary>The processor's marketing name ("AMD Ryzen 7 5800X 8-Core Processor"), for friendly display.</summary>
public static class CpuInfo
{
    private static readonly Lazy<string> Cached = new(Read);

    public static string Name => Cached.Value;

    private static string Read()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                if (key?.GetValue("ProcessorNameString") is string name && name.Trim().Length > 0)
                    return Clean(name);
            }
            else if (OperatingSystem.IsLinux() && File.Exists("/proc/cpuinfo"))
            {
                var line = File.ReadLines("/proc/cpuinfo").FirstOrDefault(l => l.StartsWith("model name", StringComparison.Ordinal));
                if (line?.Split(':', 2) is [_, var value])
                    return Clean(value);
            }
            else if (OperatingSystem.IsMacOS())
            {
                var brand = ProcessRunner.TryRun("/usr/sbin/sysctl", "-n machdep.cpu.brand_string");
                if (!string.IsNullOrWhiteSpace(brand))
                    return Clean(brand);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
        }
        return $"{Environment.ProcessorCount}-thread processor";
    }

    private static string Clean(string name) =>
        string.Join(' ', name.Replace("(R)", "").Replace("(TM)", "").Replace(" CPU", "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
}

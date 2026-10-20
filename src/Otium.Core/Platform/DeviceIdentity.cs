using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace Otium.Core.Platform;

/// <summary>
/// A stable, anonymised per-machine identifier. The referral server uses it to stop one machine from
/// redeeming several referral codes or referring itself. The raw OS machine ID never leaves the device.
/// </summary>
public static class DeviceIdentity
{
    /// <summary>A device id derived only from a seed (dev mode uses this to run two "computers" on one PC).</summary>
    public static string FromSeed(string seed) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("Otium.device.v1:seed:" + seed)));

    public static string Compute(string fallbackSeed)
    {
        var machineId = ReadMachineId();
        var source = string.IsNullOrWhiteSpace(machineId) ? fallbackSeed : machineId;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("Otium.device.v1:" + source));
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// This computer's hardware id for invites: a hash of the OS machine id, also when this install uses a device id of
    /// its own, so reinstalling (or deleting the account) doesn't make an invite code usable again on the same computer.
    /// Null when the OS has no machine id.
    /// </summary>
    public static string? HardwareId() =>
        ReadMachineId() is { Length: > 0 } machineId ? HardwareIdFromSeed(machineId) : null;

    /// <summary>A hardware id from a seed (dev mode, where one PC runs several "computers").</summary>
    public static string HardwareIdFromSeed(string seed) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("otium-hw:" + seed.Trim())));

    private static string? ReadMachineId()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
                return key?.GetValue("MachineGuid") as string;
            }

            if (OperatingSystem.IsMacOS())
            {
                var output = ProcessRunner.TryRun("/usr/sbin/ioreg", "-rd1 -c IOPlatformExpertDevice");
                var line = output?.Split('\n').FirstOrDefault(l => l.Contains("IOPlatformUUID", StringComparison.Ordinal));
                return line?.Split('"', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim();
            }

            foreach (var path in new[] { "/etc/machine-id", "/var/lib/dbus/machine-id" })
            {
                if (File.Exists(path))
                    return File.ReadAllText(path).Trim();
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
        }
        return null;
    }
}

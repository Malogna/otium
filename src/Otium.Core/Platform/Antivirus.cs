using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Otium.Core.Platform;

/// <param name="Enabled">Real-time protection on, as Windows Security Center reports it.</param>
public sealed record AntivirusProduct(string Name, bool Enabled)
{
    public bool IsMicrosoftDefender => Name.Contains("Defender", StringComparison.OrdinalIgnoreCase);
}

/// <summary>A file Microsoft Defender removed or blocked, and when.</summary>
public sealed record AntivirusRemoval(string File, DateTimeOffset At);

/// <summary>
/// What's guarding this computer and what it took from Otium's engines folder, so the app can name the culprit and
/// give the right fix: one click for Microsoft Defender, step-by-step for other antivirus products, whose settings
/// Otium can't change.
/// </summary>
public sealed record AntivirusReport(IReadOnlyList<AntivirusProduct> Products, IReadOnlyList<AntivirusRemoval> Removals)
{
    /// <summary>The antivirus doing the scanning: a third-party product when one is on (Defender then steps back).</summary>
    public AntivirusProduct? Active =>
        Products.FirstOrDefault(p => p.Enabled && !p.IsMicrosoftDefender) ?? Products.FirstOrDefault(p => p.Enabled);

    /// <summary>Whether Otium's one-click Defender exclusion can fix it (true when unknown, as on most PCs).</summary>
    public bool DefenderIsActive => Active?.IsMicrosoftDefender ?? true;

    /// <summary>
    /// One or two plain sentences: what was removed, by whom and when, and what to do when it isn't Defender.
    /// </summary>
    public string Describe(string enginesDir, TimeZoneInfo? zone = null)
    {
        zone ??= TimeZoneInfo.Local;
        var parts = new List<string>();
        var removed = Removals
            .GroupBy(r => Path.GetFileName(r.File), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Max(r => r.At))
            .Select(g =>
            {
                var last = TimeZoneInfo.ConvertTime(g.Max(r => r.At), zone);
                var times = g.Count() == 1 ? "once" : $"{g.Count()} times";
                return $"{g.Key} {times}, most recently on {last.ToString("MMM d 'at' HH:mm", CultureInfo.InvariantCulture)}";
            })
            .ToList();
        if (removed.Count > 0)
            parts.Add($"Microsoft Defender removed {string.Join("; ", removed)}.");

        if (Active is { IsMicrosoftDefender: false } other)
            parts.Add($"Your antivirus is {other.Name}, and Otium can't change its settings for you. Open {other.Name}, find its " +
                      "exceptions (also called exclusions or the allow list), add this folder, then press Try again: " + enginesDir);
        return string.Join(" ", parts);
    }
}

public static class Antivirus
{
    /// <summary>
    /// Asks Windows (without administrator rights) for the installed antivirus products and Microsoft Defender's
    /// recent detections in <paramref name="enginesDir"/>. Null off Windows or if Windows doesn't answer.
    /// </summary>
    public static async Task<AntivirusReport?> InspectAsync(string enginesDir, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows())
            return null;
        const string script =
            "$p = @(Get-CimInstance -Namespace root/SecurityCenter2 -ClassName AntiVirusProduct -ErrorAction SilentlyContinue | " +
            "Select-Object displayName, productState); " +
            "$d = @(Get-MpThreatDetection -ErrorAction SilentlyContinue | Select-Object " +
            "@{n='at';e={$_.InitialDetectionTime.ToUniversalTime().ToString('o')}}, @{n='files';e={@($_.Resources)}}); " +
            "@{ products = $p; detections = $d } | ConvertTo-Json -Depth 4 -Compress";
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"))
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
        };
        foreach (var arg in new[] { "-NoProfile", "-NonInteractive", "-Command", script })
            start.ArgumentList.Add(arg);
        try
        {
            using var process = Process.Start(start);
            if (process is null)
                return null;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            var output = await process.StandardOutput.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            return Parse(output, enginesDir);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or OperationCanceledException or JsonException && !ct.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>Reads the script's JSON; detections outside the engines folder are left out.</summary>
    internal static AntivirusReport Parse(string json, string enginesDir)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        var root = doc.RootElement;
        var products = Items(root, "products")
            .Select(p => new AntivirusProduct(
                p.TryGetProperty("displayName", out var n) ? n.GetString() ?? "" : "",
                p.TryGetProperty("productState", out var s) && s.TryGetInt32(out var state) && IsEnabled(state)))
            .Where(p => p.Name.Length > 0)
            .ToList();

        var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(enginesDir)) + Path.DirectorySeparatorChar;
        var removals = new List<AntivirusRemoval>();
        foreach (var d in Items(root, "detections"))
        {
            if (!d.TryGetProperty("at", out var at) || !DateTimeOffset.TryParse(at.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var when))
                continue;
            foreach (var resource in Items(d, "files").Where(f => f.ValueKind == JsonValueKind.String).Select(f => f.GetString()!))
            {
                // Defender writes files as "file:_C:\path".
                var path = resource.StartsWith("file:_", StringComparison.OrdinalIgnoreCase) ? resource[6..] : resource;
                if (path.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                    removals.Add(new AntivirusRemoval(path, when));
            }
        }
        return new AntivirusReport(products, removals);
    }

    /// <summary>Security Center's productState: bits 12–15 are 1 while real-time protection is on (e.g. 0x61100).</summary>
    internal static bool IsEnabled(int productState) => ((productState >> 12) & 0xF) == 1;

    /// <summary>ConvertTo-Json writes a one-item array as a bare object, so accept both.</summary>
    private static IEnumerable<JsonElement> Items(JsonElement e, string name) =>
        !e.TryGetProperty(name, out var v) ? []
        : v.ValueKind == JsonValueKind.Array ? v.EnumerateArray()
        : v.ValueKind == JsonValueKind.Object || v.ValueKind == JsonValueKind.String ? [v]
        : [];
}

namespace Otium.Core.Platform;

public sealed class AppPaths(string root)
{
    public static AppPaths Default { get; } = new(DefaultRoot());

    public string Root { get; } = root;

    public string SettingsFile => Path.Combine(Root, "settings.json");

    public string LedgerFile => Path.Combine(Root, "ledger.json");

    public string GpuLedgerFile => Path.Combine(Root, "ledger-gpu.json");

    public string GpuBenchmarksFile => Path.Combine(Root, "gpu-benchmarks.json");

    public string HistoryFile => Path.Combine(Root, "history.json");

    /// <summary>Downloaded engines live in one folder, so an antivirus exclusion can be kept this narrow.</summary>
    public string EnginesDir => Path.Combine(Root, "engines");

    public string LogsDir => Path.Combine(Root, "logs");

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(EnginesDir);
        Directory.CreateDirectory(LogsDir);
    }

    private static string DefaultRoot()
    {
        var overridden = Environment.GetEnvironmentVariable("OTIUM_HOME");
        if (!string.IsNullOrWhiteSpace(overridden))
            return overridden;

        if (OperatingSystem.IsMacOS())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Otium");

        // %LOCALAPPDATA% on Windows, ~/.local/share on Linux.
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Otium");
    }
}

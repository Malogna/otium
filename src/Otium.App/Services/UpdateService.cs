using System.Reflection;
using Otium.Core;
using Velopack;
using Velopack.Exceptions;
using Velopack.Sources;

namespace Otium.App.Services;

/// <summary>
/// Background updates through Velopack. Updates download silently; the user chooses when to restart, because
/// restarting stops mining for a few seconds. Only active in installed copies (not when run from bin/Debug).
/// </summary>
public sealed class UpdateService
{
    private readonly UpdateManager? _manager;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private UpdateInfo? _ready;

    private UpdateService(UpdateManager? manager, string status)
    {
        _manager = manager;
        Status = status;
    }

    public static UpdateService Create()
    {
        var url = AppIdentity.ResolveUpdateUrl();
        if (url is null)
            return new UpdateService(null, "Automatic updates aren't configured in this build.");

        var manager = Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            ? new UpdateManager(new GithubSource(url, null, false))
            : new UpdateManager(url);

        return manager.IsInstalled
            ? new UpdateService(manager, "")
            : new UpdateService(null, "Updates work in installed copies only (this one is running from a build folder).");
    }

    public event Action? Changed;

    public bool IsEnabled => _manager is not null;

    public string Status { get; private set; }

    public string? ReadyVersion => _ready?.TargetFullRelease.Version.ToString();

    public string CurrentVersion =>
        _manager?.CurrentVersion?.ToString()
        ?? Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "dev";

    /// <summary>Checks for and downloads a newer version. Safe to call repeatedly.</summary>
    public async Task CheckAsync()
    {
        if (_manager is null || !await _lock.WaitAsync(0))
            return;
        try
        {
            SetStatus("Checking for updates…");
            var update = await _manager.CheckForUpdatesAsync();
            if (update is null)
            {
                SetStatus($"You're on the latest version ({CurrentVersion}).");
                return;
            }

            SetStatus($"Downloading version {update.TargetFullRelease.Version}…");
            await _manager.DownloadUpdatesAsync(update);
            _ready = update;
            SetStatus($"Version {update.TargetFullRelease.Version} is ready. Restart Otium to finish updating.");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or ChecksumFailedException or AcquireLockFailedException or NotInstalledException or IOException)
        {
            SetStatus($"Couldn't check for updates: {e.Message}");
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Exits, installs the downloaded version and restarts. Callers must stop the miners first.</summary>
    public void ApplyAndRestart()
    {
        if (_manager is not null && _ready is not null)
            _manager.ApplyUpdatesAndRestart(_ready.TargetFullRelease, ["--minimized"]);
    }

    private void SetStatus(string status)
    {
        Status = status;
        Changed?.Invoke();
    }
}

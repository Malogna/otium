using System.Reflection;
using Otium.Core;
using Otium.Core.Updates;
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

        // A release candidate ("0.1.0-rc.1") follows the pre-releases, so testers get rc.2 and then the release; stable
        // installs only ever see stable releases.
        var version = typeof(UpdateService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "";
        var followPrereleases = version.Split('+')[0].Contains('-');
        // Every update must be signed with a key built into Otium (see UpdateSignature), wherever it comes from: GitHub
        // releases are read through Otium's server (its /updates feed), with GitHub itself as the fallback; any other
        // update address is read as a feed folder.
        var feeds = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        feeds.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppIdentity.Name}-Updater");
        var packages = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        packages.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppIdentity.Name}-Updater");
        var updates = new SignedUpdates(packages, AppIdentity.UpdateSigningKeys);
        IUpdateSource source = Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            ? new FallbackUpdateSource(
                new SignedUpdateSource(updates, new ServerUpdateOrigin(feeds, ServerFeedUrl(followPrereleases))),
                new SignedUpdateSource(updates, new GitHubUpdateOrigin(feeds, uri.AbsolutePath.Trim('/'), followPrereleases)))
            : new SignedUpdateSource(updates, new ServerUpdateOrigin(feeds, url));
        var manager = new UpdateManager(source);

        return manager.IsInstalled
            ? new UpdateService(manager, "")
            : new UpdateService(null, "Updates work in installed copies only (this one is running from a build folder).");
    }

    /// <summary>Otium's server's copy of the release feed: release candidates too, or releases only.</summary>
    private static string ServerFeedUrl(bool prereleases) =>
        $"{AppIdentity.ResolveServerUrl().TrimEnd('/')}/updates/{(prereleases ? "pre" : "stable")}/";

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
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or ChecksumFailedException or AcquireLockFailedException or NotInstalledException
                                       or IOException or UpdateVerificationException)
        {
            SetStatus(e is HttpRequestException or TaskCanceledException
                ? "Couldn't reach the update server just now. Otium tries again in a few hours."
                : $"Couldn't check for updates: {e.Message}");
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Exits, installs the downloaded version and restarts. Callers must stop the miners first.</summary>
    /// <param name="showWindow">The window was open: the new version opens it again instead of starting in the tray.</param>
    public void ApplyAndRestart(bool showWindow)
    {
        if (_manager is not null && _ready is not null)
            _manager.ApplyUpdatesAndRestart(_ready.TargetFullRelease, RestartArguments(showWindow));
    }

    /// <summary>What the new version starts with: in the tray ("--minimized") unless the window was open.</summary>
    private static string[] RestartArguments(bool showWindow) => showWindow ? [] : ["--minimized"];

    private void SetStatus(string status)
    {
        Status = status;
        Changed?.Invoke();
    }
}

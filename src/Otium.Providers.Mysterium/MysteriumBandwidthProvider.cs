using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using System.Security.Cryptography;
using Otium.Core.Accounts;
using Otium.Core.Bandwidth;
using Otium.Core.Providers;

namespace Otium.Providers.Mysterium;

/// <summary>
/// Internet sharing as a Mysterium Network node (the open-source <c>myst</c> node, run headless). The node is paid in
/// MYST by Mysterium's B2B customers and settles straight to the user's own Polygon address (its "beneficiary"), so
/// nothing passes through Otium. Only B2B services run: data scraping, data transfer and business VPN.
///
/// Every new node needs a one-time on-chain registration (about 0.13 MYST, a cent or so in October 2026). Otium pays it:
/// the server sends the fee to the node's channel address, then the node registers with the user's address. The node
/// also needs Mysterium's network service installed once, with an administrator prompt.
/// </summary>
public sealed class MysteriumBandwidthProvider : IBandwidthProvider
{
    /// <summary>The services Mysterium recommends for home nodes: its business customers only, no anonymous VPN.</summary>
    public static readonly IReadOnlyList<string> B2BServices = ["scraping", "data_transfer", "dvpn"];

    /// <summary>How long a just-funded channel gets before registering is retried.</summary>
    private static readonly TimeSpan RegisterRetry = TimeSpan.FromSeconds(30);

    private readonly EngineInstaller _installer;
    private readonly string _dataDir;
    private readonly Func<string?> _beneficiary;
    private readonly Func<MystSponsorRequest, CancellationToken, Task<MystSponsorResult>> _sponsor;
    private readonly IMystSupervisor? _supervisor;
    private readonly MystAsset? _asset;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private Process? _process;
    private TequilaClient? _api;
    private string? _identity;
    private long _baselineBytes;
    private bool _funded;
    private DateTimeOffset _nextRegisterAttempt;
    private string? _registrationProblem;
    private bool _supervisorDeclined;

    /// <param name="beneficiary">The user's own Polygon (0x…) address that earnings settle to; null until they set one.</param>
    /// <param name="sponsor">Asks the Otium server to pay the registration fee for a node.</param>
    public MysteriumBandwidthProvider(
        HttpClient http,
        string enginesRoot,
        Func<string?> beneficiary,
        Func<MystSponsorRequest, CancellationToken, Task<MystSponsorResult>> sponsor,
        IMystSupervisor? supervisor = null,
        MystAsset? asset = null,
        TimeProvider? time = null)
    {
        var engineDir = Path.Combine(enginesRoot, $"myst-{MystRelease.Version}");
        _installer = new EngineInstaller(http, engineDir, MystRelease.BinaryName);
        // Outside the versioned folder, so the node's identity (its keystore) survives an upgrade.
        _dataDir = Path.Combine(enginesRoot, "myst-data");
        _beneficiary = beneficiary;
        _sponsor = sponsor;
        _supervisor = supervisor ?? (OperatingSystem.IsWindows() ? new WindowsMystSupervisor() : null);
        _asset = asset ?? MystRelease.ForCurrentPlatform();
        _time = time ?? TimeProvider.System;
    }

    public string Id => "mysterium";

    public string DisplayName => "Mysterium Network";

    public string? UnavailableReason =>
        _asset is null || _supervisor is null ? "Internet sharing through Mysterium isn't available on this computer yet (Windows only for now)."
        : _beneficiary() is null ? "Add a Polygon address (0x…) for internet sharing payouts."
        : null;

    public bool IsInstalled => _installer.IsInstalled;

    public Task InstallAsync(IProgress<InstallProgress>? progress, CancellationToken ct) =>
        _asset is null
            ? throw new PlatformNotSupportedException("Mysterium has no node for this computer.")
            : _installer.InstallAsync($"Mysterium node {MystRelease.Version}", _asset.DownloadUrl, _asset.FileName, _asset.Sha256, progress, ct);

    public async Task StartAsync(BandwidthRunOptions options, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_process is { HasExited: false })
                return;
            var binary = _installer.FindBinary() ?? throw new InvalidOperationException("The Mysterium node isn't installed.");
            var supervisor = Path.Combine(Path.GetDirectoryName(binary)!, MystRelease.SupervisorName);
            if (_supervisor is { IsInstalled: false })
            {
                // Ask for administrator rights once per opt-in, not on every retry.
                if (_supervisorDeclined)
                    throw new ProviderProblemException(
                        "Internet sharing needs a one-time administrator permission to set up Mysterium's network service. Turn sharing off and on again to be asked once more.");
                try
                {
                    await _supervisor.InstallAsync(supervisor, ct);
                }
                catch (ProviderProblemException)
                {
                    _supervisorDeclined = true;
                    throw;
                }
            }

            Directory.CreateDirectory(_dataDir);
            var port = FreeLoopbackPort();
            var password = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
            var start = new ProcessStartInfo(binary)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(binary)!,
            };
            foreach (var arg in DaemonArguments(_dataDir, port, password))
                start.ArgumentList.Add(arg);
            _process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the Mysterium node.");
            MinerLifetime.Adopt(_process); // dies with Otium, even if Otium crashes
            // Drain its log so a full pipe never blocks it.
            _process.OutputDataReceived += (_, _) => { };
            _process.ErrorDataReceived += (_, _) => { };
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            _api = new TequilaClient(new SocketsHttpHandler(), port, password);
            var deadline = _time.GetUtcNow() + TimeSpan.FromSeconds(60);
            while (!await _api.IsHealthyAsync(ct))
            {
                if (_process.HasExited || _time.GetUtcNow() > deadline)
                    throw new ProviderProblemException("The Mysterium node didn't start. Retrying in a while.");
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }

            _identity = await _api.UnlockCurrentIdentityAsync(ct);
            // Sharing only starts after the user agreed in Otium's consent screen, which shows Mysterium's terms.
            await _api.AgreeToProviderTermsAsync(ct);
            _baselineBytes = await _api.GetRelayedBytesAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>The node's command line: no web UI, the local API on 127.0.0.1 only, network changes via the supervisor.</summary>
    internal static IReadOnlyList<string> DaemonArguments(string dataDir, int port, string password) =>
    [
        "--ui.enable=false",
        $"--data-dir={dataDir}",
        $"--config-dir={dataDir}",
        $"--log-dir={Path.Combine(dataDir, "logs")}",
        "--tequilapi.address=127.0.0.1",
        $"--tequilapi.port={port.ToString(CultureInfo.InvariantCulture)}",
        $"--tequilapi.auth.password={password}",
        "--usermode",
        "daemon",
    ];

    public async Task<BandwidthStatus> GetStatusAsync(CancellationToken ct)
    {
        if (_process is null || _api is null || _identity is null)
            return new BandwidthStatus(BandwidthProviderState.Stopped, 0);
        if (_process.HasExited)
            return new BandwidthStatus(BandwidthProviderState.Error, 0, "The Mysterium node stopped. Restarting it…");

        var identity = await _api.GetIdentityAsync(_identity, ct);
        var bytes = Math.Max(0, await _api.GetRelayedBytesAsync(ct) - _baselineBytes);
        switch (identity.Status)
        {
            case "Registered":
                await EnsureServicesAsync(identity.Id, ct);
                return new BandwidthStatus(BandwidthProviderState.Online, bytes, null, EarningsText(identity));
            case "InProgress":
                return new BandwidthStatus(BandwidthProviderState.Starting, bytes, "Registering this computer with Mysterium (one time, a few minutes)…");
            case "RegistrationError":
                return new BandwidthStatus(BandwidthProviderState.Error, bytes, "Mysterium couldn't register this computer. Retrying later.");
            default:
                await RegisterAsync(identity, ct);
                return new BandwidthStatus(BandwidthProviderState.Starting, bytes,
                    _registrationProblem ?? "Registering this computer with Mysterium (one time, a few minutes)…");
        }
    }

    /// <summary>Gets the fee paid (once per identity) and registers with the user's address as beneficiary.</summary>
    private async Task RegisterAsync(MystIdentity identity, CancellationToken ct)
    {
        if (_time.GetUtcNow() < _nextRegisterAttempt || _api is null)
            return;
        _nextRegisterAttempt = _time.GetUtcNow() + RegisterRetry;
        var beneficiary = _beneficiary() ?? throw new ProviderProblemException("Add a Polygon address (0x…) for internet sharing payouts.");

        if (!_funded)
        {
            var result = await _sponsor(new MystSponsorRequest(identity.Id, identity.ChannelAddress), ct);
            if (result.Status == MystSponsorStatuses.Unavailable)
            {
                _registrationProblem = result.Message ?? "Otium can't cover Mysterium's registration right now. Retrying later.";
                _nextRegisterAttempt = _time.GetUtcNow() + TimeSpan.FromMinutes(30);
                return;
            }
            _funded = true;
            _registrationProblem = null;
            return; // give the transfer a block or two before registering
        }

        try
        {
            await _api.RegisterAsync(identity.Id, beneficiary, ct);
            _registrationProblem = null;
        }
        catch (MystNodeException e)
        {
            // Usually the fee hasn't arrived yet; try again shortly.
            _registrationProblem = $"Waiting for Mysterium to confirm the registration… ({e.Message})";
        }
    }

    private async Task EnsureServicesAsync(string identity, CancellationToken ct)
    {
        var running = (await _api!.GetServicesAsync(ct)).Select(s => s.Type).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var type in B2BServices.Where(t => !running.Contains(t)))
            await _api.StartServiceAsync(identity, type, ct);
    }

    internal static string EarningsText(MystIdentity identity) =>
        $"{identity.EarningsTotalMyst.ToString("0.####", CultureInfo.InvariantCulture)} MYST earned in total; " +
        $"{identity.EarningsMyst.ToString("0.####", CultureInfo.InvariantCulture)} MYST waiting, paid to your Polygon address automatically at 5 MYST (Mysterium keeps 20%).";

    public async Task StopAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_process is { HasExited: false } process)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(ct);
            }
            _process?.Dispose();
            _process = null;
            _api = null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Consent is recorded with the node when it starts (terms agreed). Opting in again also allows one more
    /// administrator prompt if the user declined the last one.
    /// </summary>
    public Task OptInAsync(CancellationToken ct)
    {
        _supervisorDeclined = false;
        return Task.CompletedTask;
    }

    public async Task OptOutAsync(CancellationToken ct)
    {
        if (_api is not null)
        {
            try
            {
                await _api.WithdrawProviderTermsAsync(ct);
            }
            catch (Exception e) when (e is HttpRequestException or MystNodeException or TaskCanceledException)
            {
                // The node is going away anyway.
            }
        }
        await StopAsync(ct);
    }

    public async ValueTask DisposeAsync() => await StopAsync(CancellationToken.None);

    private static int FreeLoopbackPort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        return ((System.Net.IPEndPoint)socket.LocalEndPoint!).Port;
    }
}

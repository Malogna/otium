using System.Collections.Concurrent;
using System.ComponentModel;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using Otium.Core.Pools;
using Otium.Core.Providers;
using Otium.Core.Wallets;

namespace Otium.Providers.XMRig;

/// <summary>CPU mining with XMRig (RandomX), paid by the pool directly to the user's own XMR address.</summary>
public sealed class XmrigProvider : IComputeProvider, IPausableProvider
{
    /// <summary>Conservative pool fee used in estimates; public sources disagree on MoneroOcean's fee (0–1%).</summary>
    public const double PoolFeePercent = 1.0;

    // Windows error raised when Defender blocks launching a file it considers malicious/unwanted.
    private const int ErrorVirusInfected = 225;
    private const int ErrorVirusDeleted = 226;

    private readonly HttpClient _http;
    private readonly IMoneroPoolApi _pool;
    private readonly string _engineDir;
    private readonly PoolEndpoint _endpoint;
    private readonly IUnmineableApi _unmineable;
    private readonly string? _referralCode;
    private readonly EngineInstaller _installer;
    private readonly ConcurrentQueue<string> _log = new();
    private IXmrigProcess? _process;
    private int _httpPort;
    private string _accessToken = "";
    private DateTimeOffset _startedAt;
    private (string Host, int Port)? _poolAddress;
    private ProviderRunOptions? _options;
    private (string Url, string User) _poolLogin;
    private DateTimeOffset _lastConfigCheck;
    private readonly Func<string, int, CancellationToken, Task<string?>> _poolBlocked;

    /// <summary>How long the miner may stay unconnected before Otium checks whether something blocks the pool.</summary>
    public static readonly TimeSpan ConnectGrace = TimeSpan.FromMinutes(2);

    /// <summary>How often Otium asks the running miner which pool and login it uses (see <see cref="GetStatusAsync"/>).</summary>
    public static readonly TimeSpan ConfigCheckInterval = TimeSpan.FromSeconds(60);

    /// <param name="referralCode">Otium's unMineable referral code, added to unMineable logins.</param>
    public XmrigProvider(
        HttpClient http,
        IMoneroPoolApi pool,
        string enginesRoot,
        PoolEndpoint? endpoint = null,
        IUnmineableApi? unmineable = null,
        string? referralCode = null,
        bool unitedStates = false,
        Func<string, int, CancellationToken, Task<string?>>? poolBlocked = null,
        Func<bool>? isUnitedStates = null)
    {
        _poolBlocked = poolBlocked ?? ((_, _, _) => Task.FromResult<string?>(null));
        _unitedStates = isUnitedStates ?? (() => unitedStates);
        _http = http;
        _pool = pool;
        _unmineable = unmineable ?? new UnmineableClient(http);
        _referralCode = referralCode;
        _engineDir = Path.Combine(enginesRoot, $"xmrig-{XmrigRelease.Version}");
        _endpoint = endpoint ?? PoolEndpoint.MoneroOcean;
        _installer = new EngineInstaller(http, _engineDir, XmrigRelease.BinaryName, keepArchive: true);
    }

    /// <summary>The XMRig download for this computer (tests use their own).</summary>
    internal XmrigAsset? Asset { get; init; } = XmrigRelease.ForCurrentPlatform();

    /// <summary>Starts XMRig with the given arguments, sending its output to the log (tests use a stand-in).</summary>
    internal Func<string, IReadOnlyList<string>, Action<string?>, IXmrigProcess> Launch { get; init; } = XmrigProcess.Start;

    internal TimeProvider Time { get; init; } = TimeProvider.System;

    internal EngineInstaller Installer => _installer;

    /// <summary>The user is a US person, whose Monero mines on MoneroOcean instead of unMineable (asked each time: it can change).</summary>
    private readonly Func<bool> _unitedStates;

    public string Id => "xmrig-cpu";

    public string DisplayName => "CPU mining (Monero via XMRig)";

    public DeviceKind Device => DeviceKind.Cpu;

    public string PayoutCurrency => "XMR";

    public bool IsSupportedOnThisPlatform => Asset is not null;

    public string EngineDirectory => _engineDir;

    private string ConfigPath => Path.Combine(_engineDir, "config.json");

    public bool IsInstalled => _installer.IsInstalled;

    /// <summary>Recent engine output, for the activity log and bug reports.</summary>
    public IReadOnlyList<string> RecentLog => _log.ToArray();

    public string? ValidatePayoutAddress(string address) =>
        PayoutTarget.TryParse(address, out _, out var error) ? null : error;

    public Task InstallAsync(IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        if (Asset is not { } asset)
            throw new PlatformNotSupportedException("XMRig has no official build for this operating system and processor.");
        return _installer.InstallAsync($"XMRig {XmrigRelease.Version}", asset.DownloadUrl, asset.FileName, asset.Sha256, progress, ct);
    }

    public async Task StartAsync(ProviderRunOptions options, CancellationToken ct)
    {
        if (_process is { HasExited: false })
            await StopAsync(ct);

        // Checked against the download (pinned in code) before every launch, not only at install: a binary swapped for
        // one that ignores the pool settings would skip Otium's fee slice.
        var asset = Asset ?? throw new PlatformNotSupportedException("XMRig has no official build for this operating system and processor.");
        var binary = await _installer.VerifiedBinaryAsync("The mining engine", asset.FileName, asset.Sha256, ct)
                     ?? throw new EngineBlockedException("The mining engine is missing — antivirus software may have removed it.", _engineDir);

        _httpPort = FreeLoopbackPort();
        _accessToken = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        var config = XmrigConfig.Build(options, _endpoint, _httpPort, _accessToken, _referralCode, unitedStates: _unitedStates());
        _poolAddress = XmrigConfig.PoolAddress(config);
        _poolLogin = XmrigConfig.PoolLogin(config);
        await File.WriteAllTextAsync(ConfigPath, config, ct);

        try
        {
            _process = Launch(binary, [$"--config={ConfigPath}", "--no-color"], AppendLog);
        }
        catch (Win32Exception e) when (e.NativeErrorCode is ErrorVirusInfected or ErrorVirusDeleted)
        {
            throw new EngineBlockedException("Windows Security blocked the mining engine from starting.", _engineDir);
        }

        _options = options;
        _startedAt = Time.GetUtcNow();
        _lastConfigCheck = _startedAt;
    }

    public async Task StopAsync(CancellationToken ct)
    {
        var process = _process;
        _process = null;
        if (process is null)
            return;

        await process.DisposeAsync();
    }

    public async Task<ProviderStatus> GetStatusAsync(CancellationToken ct)
    {
        var process = _process;
        if (process is null)
            return ProviderStatus.Stopped(IsInstalled);

        if (process.HasExited)
        {
            var lastLines = string.Join(" ", RecentLog.TakeLast(3));
            return new ProviderStatus(ProviderState.Error, 0, $"The mining engine exited (code {process.ExitCode}). {lastLines}".Trim());
        }

        if (Time.GetUtcNow() - _lastConfigCheck >= ConfigCheckInterval && _options is { } options)
        {
            _lastConfigCheck = Time.GetUtcNow();
            if (await PoolSettingsChangedAsync(ct))
            {
                AppendLog("[Otium] Warning: the mining engine's pool settings were changed outside Otium. Restarting it with Otium's settings.");
                await StartAsync(options, ct); // stops it and writes Otium's config afresh
                return new ProviderStatus(ProviderState.Starting, 0, "Restarting the mining engine…");
            }
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{_httpPort}/2/summary");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            using var response = await _http.SendAsync(request, timeout.Token);
            response.EnsureSuccessStatusCode();
            var summary = XmrigSummary.Parse(await response.Content.ReadAsStringAsync(ct));
            var message = summary.PoolConnected ? null : "Connecting to the pool…";
            if (!summary.PoolConnected && Time.GetUtcNow() - _startedAt >= ConnectGrace && _poolAddress is { } pool
                && await _poolBlocked(pool.Host, pool.Port, ct) is { } blocked)
            {
                var problem = $"Processor isn't mining: {blocked}.";
                return new ProviderStatus(ProviderState.Starting, 0, problem, Problem: problem);
            }
            return new ProviderStatus(summary.HashrateHps > 0 ? ProviderState.Running : ProviderState.Starting, summary.HashrateHps, message);
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException && !ct.IsCancellationRequested)
        {
            // The API isn't up yet during the first seconds after launch. No hashrate is reported, so none of this counts
            // as mining time (a suspended miner doesn't answer either).
            var state = Time.GetUtcNow() - _startedAt < TimeSpan.FromMinutes(1) ? ProviderState.Starting : ProviderState.Running;
            return new ProviderStatus(state, 0, "Waiting for the engine to report…");
        }
    }

    /// <summary>
    /// Whether the running miner no longer mines only to the pool and login Otium configured. XMRig's API is unrestricted
    /// (pause and resume need it) and its token is in config.json, so anyone on this computer could change the pool
    /// through it (PUT /1/config); "watch": false stops edits to the file itself from being picked up. An API answering
    /// with an error (e.g. restricted, or a different token) counts as changed; one that doesn't answer doesn't.
    /// </summary>
    private async Task<bool> PoolSettingsChangedAsync(CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{_httpPort}/1/config");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            using var response = await _http.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
                return true;
            return !XmrigConfig.MinesOnlyTo(await response.Content.ReadAsStringAsync(ct), _poolLogin.Url, _poolLogin.User);
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            return false;
        }
    }

    public Task<bool> PauseAsync(CancellationToken ct) => CallAsync("pause", ct);

    public Task<bool> ResumeAsync(CancellationToken ct) => CallAsync("resume", ct);

    /// <summary>XMRig's JSON-RPC "pause" and "resume", which keep the dataset and the pool connection.</summary>
    private async Task<bool> CallAsync(string method, CancellationToken ct)
    {
        if (_process is not { HasExited: false })
            return false;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{_httpPort}/json_rpc")
            {
                Content = RpcContent(method),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            using var response = await _http.SendAsync(request, timeout.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception e) when (e is HttpRequestException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>
    /// The JSON-RPC body. XMRig answers 415 to "application/json; charset=utf-8" (StringContent's default), so the
    /// content type carries no charset.
    /// </summary>
    internal static HttpContent RpcContent(string method)
    {
        var content = new StringContent($$"""{"method":"{{method}}","id":1,"jsonrpc":"2.0"}""", System.Text.Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    public async Task<RevenueEstimate> EstimateRevenueAsync(double hashrateHps, CancellationToken ct)
    {
        var network = await _pool.GetNetworkStatsAsync(ct);
        // The XMRig donation slice reduces the hashes credited to the user.
        var effective = hashrateHps * (1 - XmrigConfig.DonateLevelPercent / 100.0);
        var xmr = MoneroProfit.XmrPerDay(effective, network, PoolFeePercent);
        return new RevenueEstimate(xmr, xmr * network.PriceUsd, PayoutCurrency);
    }

    /// <summary>XMRig has a single algorithm, so the engine's last observed hashrate is the best guide.</summary>
    public Task<ProfitOffer?> GetExpectedOfferAsync(CancellationToken ct) => Task.FromResult<ProfitOffer?>(null);

    public async Task<PayoutBalance?> GetPayoutBalanceAsync(string walletAddress, CancellationToken ct)
    {
        if (PayoutRouting.PoolFor(walletAddress, _unitedStates()) == PayoutPool.Unmineable)
            return await UnmineableClient.GetPayoutBalanceAsync(_unmineable, PayoutTarget.ParseOrNull(walletAddress)!, ct);

        walletAddress = PayoutRouting.MoneroOceanWallet(walletAddress);
        var stats = await _pool.GetMinerStatsAsync(walletAddress, ct);
        return stats is null
            ? null
            : new PayoutBalance(stats.PendingXmr, stats.PaidXmr, PayoutCurrency, MoneroOceanClient.MinimumPayoutXmr, MoneroOceanClient.DashboardUrl(walletAddress));
    }

    public async ValueTask DisposeAsync() => await StopAsync(CancellationToken.None);

    private void AppendLog(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;
        _log.Enqueue(line);
        while (_log.Count > 200 && _log.TryDequeue(out _))
        {
        }
    }

    private static int FreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

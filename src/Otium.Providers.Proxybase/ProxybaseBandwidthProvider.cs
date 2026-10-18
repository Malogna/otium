using System.Globalization;
using System.Text.Json;
using Otium.Core.Bandwidth;
using Otium.Core.Providers;

namespace Otium.Providers.Proxybase;

/// <summary>A payout Otium asked Proxybase for, kept so the overview can say what was paid.</summary>
public sealed record ProxybasePayout(DateTimeOffset At, long Microcredits, string PayoutId);

/// <summary>
/// Internet sharing through Proxybase (proxybase.xyz): its open-source client relays SOCKS5 traffic for Proxybase's
/// buyers (AI agents, scraping and ad-verification jobs) and earns per GB. It needs no account: on first start the
/// client makes a wallet key on this computer, signs in with it, and earnings build up as credits for that key.
///
/// Otium never holds the money: whenever $1 has built up, it asks Proxybase to pay it to the user's own 0x address,
/// in USDC on the Tempo network. Only direct mode runs (this computer's own connection, no resold proxies). The wallet
/// key stays in Otium's data folder on this computer; only unpaid credits (under $1) depend on it.
/// </summary>
public sealed class ProxybaseBandwidthProvider : IBandwidthProvider
{
    /// <summary>Proxybase's minimum payout: 1,000,000 microcredits = $1.</summary>
    public const long MinimumPayoutMicrocredits = 1_000_000;

    public const double MicrocreditsPerUsd = 1_000_000;

    /// <summary>How often the balance is read (and paid out when it's enough).</summary>
    public static readonly TimeSpan BalanceInterval = TimeSpan.FromMinutes(10);

    private readonly EngineInstaller _installer;
    private readonly string _dataDir;
    private readonly Func<string?> _beneficiary;
    private readonly IProxybaseCli _cli;
    private readonly ProxybaseAsset? _asset;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private ISellerProcess? _seller;
    private DateTimeOffset _startedAt;
    private DateTimeOffset _nextBalanceCheck;
    private long? _available;
    private string? _payoutProblem;

    /// <param name="beneficiary">The user's own 0x address that payouts go to; null until they set one.</param>
    public ProxybaseBandwidthProvider(
        HttpClient http,
        string enginesRoot,
        Func<string?> beneficiary,
        IProxybaseCli? cli = null,
        ProxybaseAsset? asset = null,
        TimeProvider? time = null)
    {
        var engineDir = Path.Combine(enginesRoot, $"proxybase-{ProxybaseRelease.Version}");
        _installer = new EngineInstaller(http, engineDir, ProxybaseRelease.BinaryName);
        // Outside the versioned folder, so the wallet key and its unpaid credits survive an upgrade.
        _dataDir = Path.Combine(enginesRoot, "proxybase-data");
        _beneficiary = beneficiary;
        _cli = cli ?? new ProxybaseCli(_installer.FindBinary, _dataDir);
        _asset = asset ?? ProxybaseRelease.ForCurrentPlatform();
        _time = time ?? TimeProvider.System;
    }

    public string Id => "proxybase";

    public string DisplayName => "Proxybase";

    public string? UnavailableReason =>
        _asset is null ? "Internet sharing through Proxybase isn't available on this computer (it needs Windows, Linux, or a Mac with Apple silicon)."
        : _beneficiary() is null ? "Add a 0x address for internet sharing payouts."
        : null;

    public bool IsInstalled => _installer.IsInstalled;

    /// <summary>The address this computer's earnings build up under at Proxybase, once the wallet exists.</summary>
    public string? NodeAddress { get; private set; }

    /// <summary>Payouts Otium requested to the user's address, oldest first.</summary>
    public IReadOnlyList<ProxybasePayout> Payouts => LoadPayouts();

    private string PayoutsFile => Path.Combine(_dataDir, "otium-payouts.json");

    public Task InstallAsync(IProgress<InstallProgress>? progress, CancellationToken ct) =>
        _asset is null
            ? throw new PlatformNotSupportedException("Proxybase has no client for this computer.")
            : _installer.InstallAsync($"Proxybase client {ProxybaseRelease.Version}", _asset.DownloadUrl, _asset.FileName, _asset.Sha256, progress, ct);

    public async Task StartAsync(BandwidthRunOptions options, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_seller is { HasExited: false })
                return;
            _seller?.Dispose();
            _seller = null;
            Directory.CreateDirectory(_dataDir);

            // The wallet key that earnings build up under: made once on this computer, never shown or sent anywhere.
            var info = await _cli.RunAsync(["wallet", "info"], ct);
            NodeAddress = ProxybaseOutput.Address(info.Output);
            if (NodeAddress is null)
            {
                var created = await _cli.RunAsync(["wallet", "create"], ct);
                NodeAddress = ProxybaseOutput.Address(created.Output)
                              ?? throw new ProviderProblemException("Proxybase couldn't set up this computer. Retrying in a while.");
            }

            // A fresh session each start; the relay signs in again by itself if it expires while running.
            var login = await _cli.RunAsync(["login"], ct);
            if (!login.Succeeded)
                throw new ProviderProblemException("Couldn't sign in to Proxybase. Retrying in a while.");

            // Direct mode only: this computer's own connection, no resold proxies.
            var config = Path.Combine(_dataDir, "seller_config.json");
            if (!File.Exists(config))
                await File.WriteAllTextAsync(config, """{"upstream_proxies":[],"no_direct":false,"volunteer":false}""", ct);

            _seller = _cli.StartSeller();
            _startedAt = _time.GetUtcNow();
            _nextBalanceCheck = _startedAt + TimeSpan.FromMinutes(1);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<BandwidthStatus> GetStatusAsync(CancellationToken ct)
    {
        var seller = _seller;
        if (seller is null)
            return new BandwidthStatus(BandwidthProviderState.Stopped, 0);
        if (seller.HasExited)
            return new BandwidthStatus(BandwidthProviderState.Error, 0, "Proxybase's client stopped. Restarting it…");

        if (_time.GetUtcNow() >= _nextBalanceCheck)
        {
            _nextBalanceCheck = _time.GetUtcNow() + BalanceInterval;
            await CheckBalanceAndPayAsync(ct);
        }
        return new BandwidthStatus(BandwidthProviderState.Online, 0, _payoutProblem, EarningsText());
    }

    /// <summary>Reads what's waiting at Proxybase and, from $1, has it paid to the user's own address.</summary>
    internal async Task CheckBalanceAndPayAsync(CancellationToken ct)
    {
        var balance = await _cli.RunAsync(["buyer", "balance"], ct);
        if (ProxybaseOutput.SellerAvailable(balance.Output) is not { } available)
            return; // the backend didn't answer; try again next time
        _available = available;

        if (available < MinimumPayoutMicrocredits || _beneficiary() is not { } address)
            return;
        var payout = await _cli.RunAsync(["seller", "payout", "create", "--amount", available.ToString(CultureInfo.InvariantCulture), "--tempo-address", address], ct);
        if (ProxybaseOutput.PayoutId(payout.Output) is { } id)
        {
            var payouts = LoadPayouts().ToList();
            payouts.Add(new ProxybasePayout(_time.GetUtcNow(), available, id));
            await File.WriteAllTextAsync(PayoutsFile, JsonSerializer.Serialize(payouts), ct);
            _available = 0;
            _payoutProblem = null;
        }
        else
        {
            _payoutProblem = "Proxybase didn't accept the payout request yet. Otium tries again in a few minutes.";
        }
    }

    /// <summary>"$0.42 waiting · $3.00 paid to 0x12…ab so far (USDC on Tempo, from $1)".</summary>
    internal string? EarningsText()
    {
        var paid = LoadPayouts().Sum(p => p.Microcredits) / MicrocreditsPerUsd;
        if (_available is not { } available && paid == 0)
            return null;
        var address = _beneficiary() is { Length: > 10 } a ? $"{a[..6]}…{a[^4..]}" : "your address";
        var waiting = (_available ?? 0) / MicrocreditsPerUsd;
        return FormattableString.Invariant($"${waiting:0.00} waiting at Proxybase; ${paid:0.00} paid to {address} so far, in USDC on Tempo, automatically from $1");
    }

    private IReadOnlyList<ProxybasePayout> LoadPayouts()
    {
        try
        {
            return File.Exists(PayoutsFile)
                ? JsonSerializer.Deserialize<List<ProxybasePayout>>(File.ReadAllText(PayoutsFile)) ?? []
                : [];
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_seller is { } seller)
            {
                await seller.StopAsync(ct);
                seller.Dispose();
            }
            _seller = null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Consent is Otium's own screen; the client needs no separate opt-in.</summary>
    public Task OptInAsync(CancellationToken ct) => Task.CompletedTask;

    public Task OptOutAsync(CancellationToken ct) => StopAsync(ct);

    public async ValueTask DisposeAsync() => await StopAsync(CancellationToken.None);
}

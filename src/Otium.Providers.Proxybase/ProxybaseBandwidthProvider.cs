using System.Globalization;
using System.Text.Json;
using Otium.Core.Accounts;
using Otium.Core.Bandwidth;
using Otium.Core.Providers;

namespace Otium.Providers.Proxybase;

/// <summary>A payout Otium asked Proxybase for, kept so the overview can say what was paid.</summary>
/// <param name="ExpectedOutput">What the conversion said the user receives, e.g. "4.23 USDT (TRON)".</param>
public sealed record ProxybasePayout(DateTimeOffset At, long Microcredits, string PayoutId, string? ExpectedOutput = null);

/// <summary>
/// Otium's fee on sharing payouts: kept back at Proxybase (<paramref name="Reserved"/>) until it reaches Proxybase's $1
/// minimum, then paid to Otium's own address. <paramref name="Paid"/> is what has gone to Otium so far.
/// </summary>
public sealed record ProxybaseFee(long Reserved = 0, string? Address = null, long Paid = 0);

/// <summary>Asks the Otium server where to send a payout so it reaches the user's own address in their coin.</summary>
public delegate Task<SharingRoute> SharingRouter(SharingRouteRequest request, CancellationToken ct);

/// <summary>
/// Internet sharing through Proxybase (proxybase.xyz): its open-source client relays SOCKS5 traffic for Proxybase's
/// buyers (AI agents, scraping and ad-verification jobs) and earns per GB. It needs no account: on first start the
/// client makes a wallet key on this computer, signs in with it, and earnings build up as credits for that key.
///
/// Proxybase pays only in USDC on the Tempo network, which users shouldn't have to deal with. So when enough has built
/// up, the Otium server opens a one-time conversion (Relay) from Tempo USDC to the coin and address the user chose, and
/// Proxybase pays straight into it: the user receives their own coin, and Otium never holds the money. Otium's fee isn't
/// part of the conversion: its share of each payout stays at Proxybase and, from $1, is paid to Otium's own address.
/// A failed conversion refunds to this computer's own Proxybase wallet. Only direct mode runs (this
/// computer's own connection, no resold proxies). The wallet key stays in Otium's data folder on this computer; only
/// unpaid credits depend on it.
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
    private readonly SharingRouter _route;
    private readonly IProxybaseCli _cli;
    private readonly ProxybaseAsset? _asset;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private ISellerProcess? _seller;
    private DateTimeOffset _startedAt;
    private DateTimeOffset _nextBalanceCheck;
    private long? _available;
    private string? _payoutProblem;

    /// <param name="route">Asks the Otium server where to send a payout (see <see cref="SharingRouter"/>).</param>
    public ProxybaseBandwidthProvider(
        HttpClient http,
        string enginesRoot,
        SharingRouter route,
        IProxybaseCli? cli = null,
        ProxybaseAsset? asset = null,
        TimeProvider? time = null)
    {
        var engineDir = Path.Combine(enginesRoot, $"proxybase-{ProxybaseRelease.Version}");
        _installer = new EngineInstaller(http, engineDir, ProxybaseRelease.BinaryName);
        // Outside the versioned folder, so the wallet key and its unpaid credits survive an upgrade.
        _dataDir = Path.Combine(enginesRoot, "proxybase-data");
        _route = route;
        _cli = cli ?? new ProxybaseCli(_installer.FindBinary, _dataDir);
        _asset = asset ?? ProxybaseRelease.ForCurrentPlatform();
        _time = time ?? TimeProvider.System;
    }

    public string Id => "proxybase";

    public string DisplayName => "Proxybase";

    public string? UnavailableReason =>
        _asset is null ? "Internet sharing through Proxybase isn't available on this computer (it needs Windows, Linux, or a Mac with Apple silicon)."
        : null;

    public bool IsInstalled => _installer.IsInstalled;

    /// <summary>The address this computer's earnings build up under at Proxybase, once the wallet exists.</summary>
    public string? NodeAddress { get; private set; }

    /// <summary>Payouts Otium requested for the user, oldest first.</summary>
    public IReadOnlyList<ProxybasePayout> Payouts => LoadPayouts();

    private string PayoutsFile => Path.Combine(_dataDir, "otium-payouts.json");

    private string FeeFile => Path.Combine(_dataDir, "otium-fee.json");

    /// <summary>Otium's fee: what's kept back for it and what it has been paid.</summary>
    public ProxybaseFee Fee => LoadFee();

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

    /// <summary>
    /// Reads what's waiting at Proxybase and, from $1, asks the server where to send it; pays it there when the server
    /// says the conversion's costs are reasonable for the amount, otherwise lets it build up. Otium's fee on it is kept
    /// back and paid to Otium's address once it reaches Proxybase's $1 minimum.
    /// </summary>
    internal async Task CheckBalanceAndPayAsync(CancellationToken ct)
    {
        var balance = await _cli.RunAsync(["buyer", "balance"], ct);
        if (ProxybaseOutput.SellerAvailable(balance.Output) is not { } available)
            return; // the backend didn't answer; try again next time
        available = await PayFeeIfDueAsync(available, ct);
        _available = available;
        var free = available - LoadFee().Reserved; // the user's part; Otium's kept-back fee isn't theirs to convert
        if (free < MinimumPayoutMicrocredits)
        {
            _payoutProblem = null;
            return;
        }

        SharingRoute route;
        try
        {
            route = await _route(new SharingRouteRequest(free, NodeAddress), ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or AccountRequestException or InvalidOperationException && !ct.IsCancellationRequested)
        {
            _payoutProblem = "Couldn't reach Otium to arrange the payout. Trying again in a few minutes.";
            return;
        }

        if (route.Decision != SharingRouteDecisions.Pay || route.DepositAddress is not { } depositAddress || route.AmountMicrocredits <= 0)
        {
            _payoutProblem = route.Message;
            return;
        }

        var fee = route.FeeAddress is { } feeAddress && ProxybaseOutput.IsEvmAddress(feeAddress) ? Math.Clamp(route.FeeMicrocredits, 0, free / 10) : 0;
        var amount = Math.Min(route.AmountMicrocredits, free - fee);
        var payout = await _cli.RunAsync(["seller", "payout", "create", "--amount", amount.ToString(CultureInfo.InvariantCulture), "--tempo-address", depositAddress], ct);
        if (ProxybaseOutput.PayoutId(payout.Output) is { } id)
        {
            var payouts = LoadPayouts().ToList();
            payouts.Add(new ProxybasePayout(_time.GetUtcNow(), amount, id, route.ExpectedOutput));
            await File.WriteAllTextAsync(PayoutsFile, JsonSerializer.Serialize(payouts), ct);
            if (fee > 0)
            {
                var kept = LoadFee();
                await SaveFeeAsync(kept with { Reserved = kept.Reserved + fee, Address = route.FeeAddress!.ToLowerInvariant() }, ct);
            }
            _available = await PayFeeIfDueAsync(available - amount, ct);
            _payoutProblem = null;
        }
        else
        {
            _payoutProblem = "Proxybase didn't accept the payout request yet. Otium tries again in a few minutes.";
        }
    }

    /// <summary>Pays Otium's kept-back fee to Otium's address once it reaches Proxybase's minimum; returns what's left waiting.</summary>
    private async Task<long> PayFeeIfDueAsync(long available, CancellationToken ct)
    {
        var fee = LoadFee();
        if (fee.Reserved < MinimumPayoutMicrocredits || fee.Address is not { } address || available < fee.Reserved)
            return available;
        var payout = await _cli.RunAsync(["seller", "payout", "create", "--amount", fee.Reserved.ToString(CultureInfo.InvariantCulture), "--tempo-address", address], ct);
        if (ProxybaseOutput.PayoutId(payout.Output) is null)
            return available; // tried again next time
        await SaveFeeAsync(fee with { Reserved = 0, Paid = fee.Paid + fee.Reserved }, ct);
        return available - fee.Reserved;
    }

    private ProxybaseFee LoadFee()
    {
        try
        {
            return File.Exists(FeeFile) ? JsonSerializer.Deserialize<ProxybaseFee>(File.ReadAllText(FeeFile)) ?? new() : new();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return new();
        }
    }

    private Task SaveFeeAsync(ProxybaseFee fee, CancellationToken ct) => File.WriteAllTextAsync(FeeFile, JsonSerializer.Serialize(fee), ct);

    /// <summary>"$0.42 waiting at Proxybase; $3.00 paid out so far, last: 4.23 USDT (TRON) on its way to your wallet".</summary>
    internal string? EarningsText()
    {
        var payouts = LoadPayouts();
        var paid = payouts.Sum(p => p.Microcredits) / MicrocreditsPerUsd;
        if (_available is null && paid == 0)
            return null;
        var waiting = (_available ?? 0) / MicrocreditsPerUsd;
        var text = FormattableString.Invariant($"${waiting:0.00} waiting at Proxybase; ${paid:0.00} paid out to your wallet so far");
        return payouts.LastOrDefault()?.ExpectedOutput is { } last ? $"{text} (last: {last})" : text;
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

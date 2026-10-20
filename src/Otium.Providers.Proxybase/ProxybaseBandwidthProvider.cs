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
/// minimum, then paid to Otium's own address, the exact amount the server asks for (which is how the server recognises
/// the payment as this account's). <paramref name="Paid"/> is what has gone to Otium so far.
/// </summary>
public sealed record ProxybaseFee(long Reserved = 0, string? Address = null, long Paid = 0);

/// <summary>
/// What this computer has earned at Proxybase in total (waiting, paid to the user and paid to Otium), sampled at each
/// balance check, so the app can show the user's share and its recent pace. <paramref name="FeeShare"/> is Otium's
/// share of payouts as the server last set it (0.02 until the first payout).
/// </summary>
public sealed record ProxybaseEarned(IReadOnlyList<ProxybaseEarnedSample> Samples, double FeeShare = 0.02)
{
    public static readonly ProxybaseEarned Empty = new([]);

    /// <summary>Everything earned so far, in microcredits ($1 = 1,000,000).</summary>
    public long GrossMicrocredits => Samples.Count == 0 ? 0 : Samples[^1].GrossMicrocredits;

    /// <summary>The user's share of everything earned so far, after Otium's fee.</summary>
    public double UserUsd => GrossMicrocredits / ProxybaseBandwidthProvider.MicrocreditsPerUsd * (1 - FeeShare);

    /// <summary>
    /// The user's share per day at the pace of the last day (or of what's been seen, from an hour on); null before an
    /// hour of history.
    /// </summary>
    public double? UserUsdPerDay(DateTimeOffset now)
    {
        if (Samples.Count < 2)
            return null;
        var latest = Samples[^1];
        var from = Samples.LastOrDefault(s => s.At <= now - TimeSpan.FromDays(1)) ?? Samples[0];
        var hours = (latest.At - from.At).TotalHours;
        if (hours < 1)
            return null;
        return (latest.GrossMicrocredits - from.GrossMicrocredits) / ProxybaseBandwidthProvider.MicrocreditsPerUsd * (1 - FeeShare) / hours * 24;
    }

    /// <summary>Adds a reading when the total changed or an hour has passed; keeps a week.</summary>
    public ProxybaseEarned With(DateTimeOffset at, long grossMicrocredits)
    {
        if (Samples.Count > 0 && Samples[^1].GrossMicrocredits == grossMicrocredits && at - Samples[^1].At < TimeSpan.FromHours(1))
            return this;
        var kept = Samples.Where(s => s.At >= at - TimeSpan.FromDays(8)).Append(new ProxybaseEarnedSample(at, grossMicrocredits)).ToList();
        return this with { Samples = kept };
    }
}

public sealed record ProxybaseEarnedSample(DateTimeOffset At, long GrossMicrocredits);

/// <summary>This computer as Proxybase sees it (<c>seller status</c>).</summary>
/// <param name="ActiveStreams">Buyer connections passing through it right now.</param>
/// <param name="LatencyMs">How long Proxybase's checks take to get an answer through it.</param>
/// <param name="Uptime">The share of Proxybase's checks it answered (0–1).</param>
/// <param name="PendingMicrocredits">Earned but not yet cleared for payout.</param>
public sealed record ProxybaseNode(int ActiveStreams, int MaxStreams, string? Country, string? Category, double? LatencyMs,
    double? Uptime, long PendingMicrocredits, string? Pool = null)
{
    /// <summary>
    /// Proxybase took this computer out of the pool buyers are sent to ("pool": "suspended"), which its servers decide from
    /// their own test requests through it (a node whose tests took ~14 s was suspended; ~1.5 s was in "production").
    /// </summary>
    public bool Suspended => string.Equals(Pool, "suspended", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A few words for the overview's internet row. Open streams aren't all buyers: Proxybase's own test requests use them
    /// too, so they're called connections.
    /// </summary>
    public string Activity => Suspended ? "Paused by Proxybase: too slow in its tests" : ActiveStreams switch
    {
        0 => "Online, waiting for a buyer",
        1 => "1 connection open now",
        var n => $"{n} connections open now",
    };

    /// <summary>The fuller account for Details and Settings.</summary>
    public string Describe(bool earnedAnything)
    {
        var where = Country is { Length: 2 } code ? CountryName(code) : "your country";
        var kind = Category is { Length: > 0 } c ? $"a {c} connection" : "a connection";
        var parts = new List<string>
        {
            Suspended
                ? "Proxybase has paused this computer, so buyers aren't sent to it: its test requests through it were too slow. " +
                  "Otium keeps it online so Proxybase can test it again and list it once it answers quicker."
            : ActiveStreams > 0
                ? $"Open now: {ActiveStreams} connection{(ActiveStreams == 1 ? "" : "s")} through this computer, from buyers or Proxybase's own tests."
                : earnedAnything
                    ? $"Listed for Proxybase's buyers as {kind} in {where}; nobody is using it right now."
                    : $"Listed for Proxybase's buyers as {kind} in {where}, and waiting for the first one. Nothing sold yet.",
        };
        var quality = new List<string>();
        if (Uptime is { } up)
            quality.Add(FormattableString.Invariant($"reachable {Math.Floor(up * 10000) / 100:0.##}% of the time"));
        if (LatencyMs is { } ms)
            quality.Add(FormattableString.Invariant($"answers in {ms / 1000:0.0} s"));
        if (quality.Count > 0)
            parts.Add($"Proxybase's checks: {string.Join(", ", quality)}.");
        if (PendingMicrocredits > 0)
            parts.Add(FormattableString.Invariant($"${PendingMicrocredits / ProxybaseBandwidthProvider.MicrocreditsPerUsd:0.000} is still clearing at Proxybase."));
        return string.Join(" ", parts);
    }

    private static string CountryName(string code)
    {
        try
        {
            return new System.Globalization.RegionInfo(code).EnglishName;
        }
        catch (ArgumentException)
        {
            return code;
        }
    }
}

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
/// part of the conversion: its share of each payout stays at Proxybase and, from $1, is paid to Otium's own address
/// (the exact amount the server names, so it can tell the payment arrived; it opens no new routes while one is overdue).
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

    /// <summary>How often Otium asks Proxybase how this connection is doing (buyers connected, reachability).</summary>
    public static readonly TimeSpan NodeInterval = TimeSpan.FromMinutes(2);

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
    private DateTimeOffset _nextNodeCheck;
    private ProxybaseNode? _node;
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

    private string EarnedFile => Path.Combine(_dataDir, "otium-earned.json");

    /// <summary>What this computer has earned at Proxybase so far, sampled at each balance check.</summary>
    public ProxybaseEarned Earned => Load(EarnedFile, ProxybaseEarned.Empty);

    public double? LastKnownUserUsd => Earned.UserUsd;

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
            // The client says "No wallet found" for any wallet it can't load (a damaged or locked file too), and
            // "wallet create" replaces the file without asking: earnings not yet paid out would stay with the lost key.
            if (NodeAddress is null && File.Exists(Path.Combine(_dataDir, "wallet", "keyfile.enc")))
                throw new ProviderProblemException(
                    "Proxybase couldn't open this computer's wallet, so internet sharing is paused rather than starting a new one, which would lose " +
                    "anything not yet paid out. Send a problem report (Settings → About) so it can be looked at.");
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
        if (_time.GetUtcNow() >= _nextNodeCheck)
        {
            _nextNodeCheck = _time.GetUtcNow() + NodeInterval;
            _node = ProxybaseOutput.Node((await _cli.RunAsync(["seller", "status"], ct)).Output) ?? _node;
        }
        var earned = Earned;
        return new BandwidthStatus(BandwidthProviderState.Online, 0, _payoutProblem, EarningsText(), earned.UserUsd, earned.UserUsdPerDay(_time.GetUtcNow()),
            _node?.Activity, _node?.Describe(earned.GrossMicrocredits > 0));
    }

    /// <summary>
    /// Reads what's waiting at Proxybase and, from $1, asks the server where to send it; pays it there when the server
    /// says the conversion's costs are reasonable for the amount, otherwise lets it build up. Otium's fee on it is kept
    /// back; the app tells the server how much is kept back, and from $1 the server names the exact amount to pay
    /// Otium (a few microcredits off the fee, unique to this payment) so it can recognise the payment when it arrives.
    /// </summary>
    internal async Task CheckBalanceAndPayAsync(CancellationToken ct)
    {
        var balance = await _cli.RunAsync(["buyer", "balance"], ct);
        if (ProxybaseOutput.SellerAvailable(balance.Output) is not { } available)
            return; // the backend didn't answer; try again next time
        // Everything earned so far: what's waiting plus what has been paid out, to the user and to Otium.
        var gross = available + LoadPayouts().Sum(p => p.Microcredits) + LoadFee().Paid;
        await Save(EarnedFile, Earned.With(_time.GetUtcNow(), gross), ct);
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
            route = await _route(new SharingRouteRequest(free, NodeAddress, LoadFee().Reserved), ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or AccountRequestException or InvalidOperationException && !ct.IsCancellationRequested)
        {
            _payoutProblem = "Couldn't reach Otium to arrange the payout. Trying again in a few minutes.";
            return;
        }

        if (await PayRequestedFeeAsync(route, available, ct) is { } paid)
        {
            available -= paid;
            free = available - LoadFee().Reserved;
            _available = available;
        }

        if (route.Decision != SharingRouteDecisions.Pay || route.DepositAddress is not { } depositAddress || route.AmountMicrocredits <= 0)
        {
            _payoutProblem = route.Message;
            return;
        }
        // Relay's deposit addresses are on Tempo, an EVM chain: anything else isn't a route Otium's server makes.
        // (Who a deposit address pays out to can only be looked up with Relay's API key, so that isn't checked here.)
        if (!ProxybaseOutput.IsEvmAddress(depositAddress))
        {
            _payoutProblem = "Otium's server sent a payout address that doesn't look right, so nothing was paid. Trying again later.";
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
            // Otium's share as the server set it this time, for showing the user's share of what's still waiting.
            await Save(EarnedFile, Earned with { FeeShare = (double)fee / (amount + fee) }, ct);
            _available = available - amount;
            _payoutProblem = null;
        }
        else
        {
            _payoutProblem = "Proxybase didn't accept the payout request yet. Otium tries again in a few minutes.";
        }
    }

    /// <summary>
    /// Pays Otium's fee when the server asks for it: exactly <see cref="SharingRoute.FeePaymentMicrocredits"/> to Otium's
    /// address, out of what's kept back for it (or, for a fee the server is still owed beyond that, out of what's waiting).
    /// The exact amount is what lets the server tell this payment apart. Returns what was paid, or null.
    /// </summary>
    private async Task<long?> PayRequestedFeeAsync(SharingRoute route, long available, CancellationToken ct)
    {
        var amount = route.FeePaymentMicrocredits;
        if (amount < MinimumPayoutMicrocredits || route.FeeAddress is not { } address || !ProxybaseOutput.IsEvmAddress(address) || available < amount)
            return null;
        var payout = await _cli.RunAsync(["seller", "payout", "create", "--amount", amount.ToString(CultureInfo.InvariantCulture), "--tempo-address", address], ct);
        if (ProxybaseOutput.PayoutId(payout.Output) is null)
            return null; // the server asks again next time
        var fee = LoadFee();
        await SaveFeeAsync(fee with { Reserved = Math.Max(0, fee.Reserved - amount), Paid = fee.Paid + amount, Address = address.ToLowerInvariant() }, ct);
        return amount;
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
        var earned = Earned;
        if (earned.Samples.Count == 0 && payouts.Count == 0)
            return null;
        var paid = payouts.Sum(p => p.Microcredits) / MicrocreditsPerUsd;
        var yours = Math.Max(earned.UserUsd, paid);
        var text = FormattableString.Invariant(
            $"${yours:0.00} earned so far: ${yours - paid:0.00} waiting at Proxybase, ${paid:0.00} paid out to your wallet");
        return payouts.LastOrDefault()?.ExpectedOutput is { } last ? $"{text} (last: {last})" : text;
    }

    private T Load<T>(string path, T fallback)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? fallback : fallback;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or NotSupportedException)
        {
            return fallback;
        }
    }

    private async Task Save<T>(string path, T value, CancellationToken ct)
    {
        Directory.CreateDirectory(_dataDir);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value), ct);
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

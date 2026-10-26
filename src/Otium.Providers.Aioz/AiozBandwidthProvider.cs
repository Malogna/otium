using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using System.Text.Json;
using Otium.Core.Bandwidth;
using Otium.Core.Providers;

namespace Otium.Providers.Aioz;

/// <summary>
/// What this node has earned in total (in AIOZ: storage, delivery and transcoding together, paid out or not), sampled as
/// its status is read, and the last AIOZ price the server gave, so the app can show it in dollars and its recent pace.
/// </summary>
public sealed record AiozEarned(IReadOnlyList<AiozEarnedSample> Samples, double? AiozUsd = null)
{
    public static readonly AiozEarned Empty = new([]);

    public decimal TotalAioz => Samples.Count == 0 ? 0 : Samples[^1].TotalAioz;

    /// <summary>Everything earned so far, in dollars at the last known price (AIOZ pays it all; Otium takes nothing).</summary>
    public double? UserUsd => AiozUsd is { } price ? (double)TotalAioz * price : null;

    /// <summary>Earnings per day at the pace of the last day (from an hour of history on); null before that or without a price.</summary>
    public double? UserUsdPerDay(DateTimeOffset now)
    {
        if (Samples.Count < 2 || AiozUsd is not { } price)
            return null;
        var latest = Samples[^1];
        var from = Samples.LastOrDefault(x => x.At <= now - TimeSpan.FromDays(1)) ?? Samples[0];
        var hours = (latest.At - from.At).TotalHours;
        return hours < 1 ? null : (double)(latest.TotalAioz - from.TotalAioz) * price / hours * 24;
    }

    /// <summary>Adds a reading when the total changed or an hour has passed (keeping a week), and the newest price if any.</summary>
    public AiozEarned With(DateTimeOffset at, decimal totalAioz, double? aiozUsd)
    {
        var price = aiozUsd is > 0 ? aiozUsd : AiozUsd;
        if (Samples.Count > 0 && Samples[^1].TotalAioz == totalAioz && at - Samples[^1].At < TimeSpan.FromHours(1))
            return this with { AiozUsd = price };
        var kept = Samples.Where(x => x.At >= at - TimeSpan.FromDays(8)).Append(new AiozEarnedSample(at, totalAioz)).ToList();
        return new AiozEarned(kept, price);
    }
}

public sealed record AiozEarnedSample(DateTimeOffset At, decimal TotalAioz);

/// <summary>The node's <c>/stats</c>: what it stores for AIOZ's network and whether it's delivering.</summary>
internal sealed record AiozStats(long StoredFiles, long StoredBytes, double UpstreamSpeed);

/// <summary>The node's <c>/worker/balance</c>, in AIOZ: what's waiting, what each kind of work earned, what was paid out.</summary>
internal sealed record AiozBalance(decimal Waiting, decimal Storage, decimal Delivery, decimal Transcoding, decimal Withdrawn)
{
    public decimal Earned => Storage + Delivery + Transcoding;
}

/// <summary>
/// Sharing storage and upload bandwidth as an AIOZ DePIN node (AIOZ's official command-line node, run headless). AIOZ's
/// network stores and delivers files (its decentralized storage and CDN) through it and pays AIOZ tokens on the AIOZ
/// Network chain. The node holds what it earns until it's withdrawn; Otium withdraws it to the user's own 0x address
/// once there's enough, so nothing passes through Otium, which takes no fee.
///
/// The node runs as the user (no administrator rights) and listens only on 127.0.0.1, on a port of its own (AIOZ's
/// default 1317 is often reserved on Windows). Everything it keeps, its key and AIOZ's helper programs included, stays in
/// <c>engines/aioz-data</c>: every command gets <c>--home</c>, or the node would use <c>~/.aiozworker</c>.
///
/// No service converts AIOZ on its own network into other coins (October 2026: Relay and LI.FI don't support the AIOZ
/// chain; ChangeNOW and SideShift only take its Ethereum and BNB Chain copies), so AIOZ can't be routed to the user's
/// payout coin the way Proxybase's earnings are. It needs no address to run: when the payout address is an 0x address
/// it's withdrawn there, otherwise it waits in the node's own key on this computer until the user adds one.
///
/// AIOZ's network has many more nodes than paying files (October 2026: a test node got two storage tasks in ten minutes,
/// both aborted with "client credit is not enough"), so earnings are uncertain; the consent screen says so.
/// </summary>
public sealed class AiozBandwidthProvider : IBandwidthProvider
{
    /// <summary>Otium withdraws once the node holds this much, to keep the number of transactions low.</summary>
    public const decimal WithdrawAtAioz = 5;

    /// <summary>The node allows one withdrawal an hour; Otium tries at most once a day.</summary>
    private static readonly TimeSpan WithdrawEvery = TimeSpan.FromDays(1);

    /// <summary>How often the files the node keeps are measured against the user's storage limit.</summary>
    private static readonly TimeSpan MeasureEvery = TimeSpan.FromMinutes(10);

    private readonly EngineInstaller _installer;
    private readonly string _dataDir;
    private readonly Func<string?> _payoutAddress;
    private readonly Func<double> _storageLimitGb;
    private readonly AiozAsset? _asset;
    private readonly TimeProvider _time;
    private readonly Func<double?> _aiozUsd;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private Process? _process;
    private HttpClient? _api;
    private int _port;
    private long? _storedBytes;
    private DateTimeOffset _measuredAt = DateTimeOffset.MinValue;
    private string? _withdrawProblem;

    /// <param name="payoutAddress">The user's own 0x address AIOZ is withdrawn to; null until there is one (it then waits in the node).</param>
    /// <param name="storageLimitGb">Most disk space the node may use, in GB (the user's choice).</param>
    /// <param name="aiozUsd">Today's AIOZ price in dollars from the Otium server, or null when it has none.</param>
    public AiozBandwidthProvider(
        HttpClient http,
        string enginesRoot,
        Func<string?> payoutAddress,
        Func<double> storageLimitGb,
        AiozAsset? asset = null,
        TimeProvider? time = null,
        Func<double?>? aiozUsd = null)
    {
        _asset = asset ?? AiozRelease.ForCurrentPlatform();
        _installer = new EngineInstaller(http, Path.Combine(enginesRoot, $"aioz-{AiozRelease.Version}"),
            _asset?.BinaryName ?? "aioz-depin-cli");
        // Outside the versioned folder, so the node's key and files survive an upgrade.
        _dataDir = Path.Combine(enginesRoot, "aioz-data");
        _payoutAddress = payoutAddress;
        _storageLimitGb = storageLimitGb;
        _time = time ?? TimeProvider.System;
        _aiozUsd = aiozUsd ?? (() => null);
    }

    private string HomeDir => Path.Combine(_dataDir, "home");

    /// <summary>The node's key. It holds only what hasn't been withdrawn yet; payouts go to the user's own address.</summary>
    private string KeyFile => Path.Combine(_dataDir, "privkey.json");

    private string EarnedFile => Path.Combine(_dataDir, "otium-earned.json");

    private string WithdrawnFile => Path.Combine(_dataDir, "otium-withdrawn.txt");

    public AiozEarned Earned
    {
        get
        {
            try
            {
                return File.Exists(EarnedFile) ? JsonSerializer.Deserialize<AiozEarned>(File.ReadAllText(EarnedFile)) ?? AiozEarned.Empty : AiozEarned.Empty;
            }
            catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
            {
                return AiozEarned.Empty;
            }
        }
    }

    public double? LastKnownUserUsd => Earned.UserUsd;

    private void SaveEarned(AiozEarned earned)
    {
        try
        {
            Directory.CreateDirectory(_dataDir);
            File.WriteAllText(EarnedFile, JsonSerializer.Serialize(earned));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Shown again from the node next time; only the pace history is lost.
        }
    }

    public string Id => "aioz";

    public string DisplayName => "AIOZ Network";

    public string? UnavailableReason =>
        _asset is null
            ? "Sharing through AIOZ isn't available on this computer (it needs Windows or Linux on a 64-bit Intel or AMD processor)."
            : null;

    public bool IsInstalled => _installer.IsInstalled;

    public Task InstallAsync(IProgress<InstallProgress>? progress, CancellationToken ct) =>
        _asset is null
            ? throw new PlatformNotSupportedException("AIOZ has no node for this computer.")
            : _installer.InstallAsync($"AIOZ DePIN node {AiozRelease.Version}", _asset.DownloadUrl, _asset.FileName, _asset.Sha256, progress, ct);

    public async Task StartAsync(BandwidthRunOptions options, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_process is { HasExited: false } || OverStorageLimit())
                return;
            var binary = _installer.FindBinary() ?? throw new InvalidOperationException("The AIOZ node isn't installed.");
            Directory.CreateDirectory(HomeDir);
            if (!File.Exists(KeyFile))
                await CreateKeyAsync(binary, ct);

            _port = FreeLoopbackPort();
            var start = NodeProcess(binary);
            foreach (var arg in StartArguments(HomeDir, KeyFile, _port))
                start.ArgumentList.Add(arg);
            _process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the AIOZ node.");
            MinerLifetime.Adopt(_process); // dies with Otium, even if Otium crashes
            _process.StandardInput.Close(); // no password on the key: it never waits for one
            // Drain its log so a full pipe never blocks it.
            _process.OutputDataReceived += (_, _) => { };
            _process.ErrorDataReceived += (_, _) => { };
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            _api = new HttpClient(new SocketsHttpHandler()) { BaseAddress = new Uri($"http://127.0.0.1:{_port}"), Timeout = TimeSpan.FromSeconds(10) };
            // The first start unpacks AIOZ's helper programs, which takes a while.
            var deadline = _time.GetUtcNow() + TimeSpan.FromMinutes(2);
            while (await TryGetAsync("/stats", ct) is null)
            {
                if (_process.HasExited || _time.GetUtcNow() > deadline)
                    throw new ProviderProblemException("The AIOZ node didn't start. Retrying in a while.");
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>The node's command line: its files in Otium's folder, its API on 127.0.0.1 only, no self-updates (Otium pins it).</summary>
    internal static IReadOnlyList<string> StartArguments(string homeDir, string keyFile, int port) =>
    [
        "start",
        $"--home={homeDir}",
        $"--priv-key-file={keyFile}",
        $"--laddr=tcp://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}",
        "--no-auto-update",
        "--log_format=plain",
    ];

    /// <summary>
    /// A new key for the node, without a password. The node also prints the key's recovery words; Otium reads and drops
    /// them so they're never shown or logged (the key file is enough, and payouts go to the user's own address anyway).
    /// </summary>
    private async Task CreateKeyAsync(string binary, CancellationToken ct)
    {
        var start = NodeProcess(binary);
        foreach (var arg in new[] { "keytool", "new", $"--save-priv-key={KeyFile}", $"--home={HomeDir}" })
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the AIOZ node.");
        process.StandardInput.Close(); // skips the optional password
        var output = process.StandardOutput.ReadToEndAsync(ct);
        var errors = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        await Task.WhenAll(output, errors);
        if (process.ExitCode != 0 || !File.Exists(KeyFile))
            throw new ProviderProblemException("The AIOZ node couldn't create its key. Retrying in a while.");
    }

    private ProcessStartInfo NodeProcess(string binary)
    {
        var start = new ProcessStartInfo(binary)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _dataDir,
        };
        // The node reads the disk's model through Windows PowerShell; some PATHs leave it out.
        if (OperatingSystem.IsWindows())
        {
            var powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0");
            var path = start.Environment.TryGetValue("PATH", out var p) ? p ?? "" : "";
            if (Directory.Exists(powershell) && !path.Contains(powershell, StringComparison.OrdinalIgnoreCase))
                start.Environment["PATH"] = path.Length == 0 ? powershell : $"{path};{powershell}";
        }
        return start;
    }

    public async Task<BandwidthStatus> GetStatusAsync(CancellationToken ct)
    {
        if (OverStorageLimit())
        {
            await StopAsync(ct);
            return new BandwidthStatus(BandwidthProviderState.Error, 0, StorageLimitText());
        }
        if (_process is null || _api is null)
            return new BandwidthStatus(BandwidthProviderState.Stopped, 0);
        if (_process.HasExited)
            return new BandwidthStatus(BandwidthProviderState.Error, 0, "The AIOZ node stopped. Restarting it…");

        var statsJson = await TryGetAsync("/stats", ct);
        var balanceJson = await TryGetAsync("/worker/balance", ct);
        if (statsJson is null || balanceJson is null)
            return new BandwidthStatus(BandwidthProviderState.Starting, 0, "Connecting to AIOZ…");

        var stats = ParseStats(statsJson);
        var balance = ParseBalance(balanceJson);
        var earned = Earned.With(_time.GetUtcNow(), balance.Earned, _aiozUsd());
        SaveEarned(earned);
        await WithdrawIfDueAsync(balance, ct);
        return new BandwidthStatus(BandwidthProviderState.Online, 0, null, EarningsText(balance, _payoutAddress() is not null, _withdrawProblem),
            earned.UserUsd, earned.UserUsdPerDay(_time.GetUtcNow()), ActivityText(stats));
    }

    /// <summary>Withdraws everything waiting (whole AIOZ) to the user's address, once there's enough and at most once a day.</summary>
    private async Task WithdrawIfDueAsync(AiozBalance balance, CancellationToken ct)
    {
        var amount = decimal.Floor(balance.Waiting);
        if (amount < WithdrawAtAioz || _payoutAddress() is not { } address || _process is null || LastWithdrawal() is { } last && _time.GetUtcNow() - last < WithdrawEvery)
            return;
        var binary = _installer.FindBinary();
        if (binary is null)
            return;
        RecordWithdrawal(); // also after a failure: tried again tomorrow, not every few seconds

        var start = NodeProcess(binary);
        foreach (var arg in WithdrawArguments(HomeDir, KeyFile, _port, address, amount))
            start.ArgumentList.Add(arg);
        using var process = Process.Start(start);
        if (process is null)
            return;
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync(ct);
        var errors = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        await Task.WhenAll(output, errors);
        _withdrawProblem = process.ExitCode == 0 ? null : "AIOZ didn't accept the last withdrawal; Otium tries again tomorrow.";
    }

    internal static IReadOnlyList<string> WithdrawArguments(string homeDir, string keyFile, int port, string address, decimal amount) =>
    [
        "reward",
        "withdraw",
        $"--address={address}",
        $"--amount={amount.ToString("0", CultureInfo.InvariantCulture)}aioz",
        $"--priv-key-file={keyFile}",
        $"--endpoint=http://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}",
        $"--home={homeDir}",
    ];

    private DateTimeOffset? LastWithdrawal()
    {
        try
        {
            return File.Exists(WithdrawnFile) && DateTimeOffset.TryParse(File.ReadAllText(WithdrawnFile).Trim(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var at) ? at : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void RecordWithdrawal()
    {
        try
        {
            File.WriteAllText(WithdrawnFile, _time.GetUtcNow().ToString("O", CultureInfo.InvariantCulture));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // At worst it's tried again sooner; the node allows one an hour.
        }
    }

    /// <summary>Whether the node's files have reached the user's limit, measured every ten minutes (the node itself has no limit).</summary>
    private bool OverStorageLimit()
    {
        var now = _time.GetUtcNow();
        if (_storedBytes is null || now - _measuredAt >= MeasureEvery)
        {
            _storedBytes = FolderBytes(Path.Combine(HomeDir, "data"));
            _measuredAt = now;
        }
        return _storedBytes >= LimitBytes(_storageLimitGb());
    }

    internal static long LimitBytes(double gb) => (long)(Math.Clamp(gb, 1, 1000) * 1024 * 1024 * 1024);

    private string StorageLimitText() =>
        $"Paused: AIOZ's files reached your {_storageLimitGb().ToString("0.#", CultureInfo.InvariantCulture)} GB storage limit. Raise it in Settings to continue.";

    internal static long FolderBytes(string folder)
    {
        if (!Directory.Exists(folder))
            return 0;
        long total = 0;
        foreach (var file in Directory.EnumerateFiles(folder, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }))
        {
            try
            {
                total += new FileInfo(file).Length;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Being written or removed right now.
            }
        }
        return total;
    }

    private async Task<string?> TryGetAsync(string path, CancellationToken ct)
    {
        if (_api is null)
            return null;
        try
        {
            using var response = await _api.GetAsync(path, ct);
            return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(ct) : null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return null;
        }
    }

    internal static AiozStats ParseStats(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var storage = root.TryGetProperty("storage", out var s) ? s : default;
        var delivery = root.TryGetProperty("delivery", out var d) ? d : default;
        return new AiozStats(Number(storage, "total_count"), Number(storage, "total_size"),
            delivery.ValueKind == JsonValueKind.Object && delivery.TryGetProperty("upstream_speed", out var u) && u.TryGetDouble(out var speed) ? speed : 0);

        static long Number(JsonElement parent, string name) =>
            parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var v) && v.TryGetInt64(out var n) ? n : 0;
    }

    internal static AiozBalance ParseBalance(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        return new AiozBalance(Coins(root, "balance"), Coins(root, "storage_earned"), Coins(root, "delivery_earned"),
            Coins(root, "transcoding_earned"), Coins(root, "withdraw"));
    }

    /// <summary>
    /// A list of coins (<c>{"denom":"attoaioz","amount":"…"}</c>, Cosmos style) in whole AIOZ. AIOZ has 18 decimals;
    /// other tokens are ignored.
    /// </summary>
    internal static decimal Coins(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var list) || list.ValueKind != JsonValueKind.Array)
            return 0;
        decimal total = 0;
        foreach (var coin in list.EnumerateArray())
        {
            if (coin.ValueKind != JsonValueKind.Object || !coin.TryGetProperty("denom", out var denom) || !coin.TryGetProperty("amount", out var amount))
                continue;
            var text = amount.ValueKind == JsonValueKind.String ? amount.GetString() : amount.GetRawText();
            if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                continue;
            total += denom.GetString()?.ToLowerInvariant() switch
            {
                "attoaioz" => value / 1_000_000_000_000_000_000m,
                "nanoaioz" => value / 1_000_000_000m,
                "aioz" => value,
                _ => 0,
            };
        }
        return total;
    }

    /// <param name="hasAddress">There's an 0x address to withdraw to; without one the AIOZ waits on this computer.</param>
    internal static string EarningsText(AiozBalance balance, bool hasAddress, string? withdrawProblem)
    {
        var earned = $"{balance.Earned.ToString("0.####", CultureInfo.InvariantCulture)} AIOZ earned in total; ";
        var waiting = balance.Waiting.ToString("0.####", CultureInfo.InvariantCulture);
        var text = hasAddress
            ? earned + $"{waiting} AIOZ waiting, sent to your 0x address on the AIOZ network once it reaches {WithdrawAtAioz.ToString(CultureInfo.InvariantCulture)} AIOZ."
            : balance.Waiting >= WithdrawAtAioz
                ? earned + $"{waiting} AIOZ waiting on this computer. Add an 0x address in Advanced settings to receive it."
                : earned + $"{waiting} AIOZ waiting on this computer, kept safe until there's enough to send.";
        return withdrawProblem is null ? text : $"{text} {withdrawProblem}";
    }

    internal static string ActivityText(AiozStats stats) => stats.StoredFiles switch
    {
        0 => "Waiting for AIOZ to send files",
        1 => $"1 file stored for AIOZ ({SizeText(stats.StoredBytes)})",
        _ => $"{stats.StoredFiles.ToString(CultureInfo.InvariantCulture)} files stored for AIOZ ({SizeText(stats.StoredBytes)})",
    };

    private static string SizeText(long bytes) => bytes switch
    {
        < 1024 * 1024 => $"{(bytes / 1024.0).ToString("0", CultureInfo.InvariantCulture)} KB",
        < 1024L * 1024 * 1024 => $"{(bytes / 1024.0 / 1024).ToString("0.#", CultureInfo.InvariantCulture)} MB",
        _ => $"{(bytes / 1024.0 / 1024 / 1024).ToString("0.##", CultureInfo.InvariantCulture)} GB",
    };

    public async Task StopAsync(CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_process is { HasExited: false } process)
            {
                process.Kill(entireProcessTree: true); // its helper programs too
                await process.WaitForExitAsync(ct);
            }
            _process?.Dispose();
            _process = null;
            _api?.Dispose();
            _api = null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>AIOZ's node has no consent setting of its own; the user agreed in Otium's consent screen.</summary>
    public Task OptInAsync(CancellationToken ct) => Task.CompletedTask;

    public Task OptOutAsync(CancellationToken ct) => StopAsync(ct);

    public async ValueTask DisposeAsync() => await StopAsync(CancellationToken.None);

    private static int FreeLoopbackPort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        return ((System.Net.IPEndPoint)socket.LocalEndPoint!).Port;
    }
}

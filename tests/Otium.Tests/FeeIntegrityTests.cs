using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Otium.Core.Engine;
using Otium.Core.Pools;
using Otium.Core.Providers;
using Otium.Core.Settings;
using Otium.Providers.XMRig;
using Otium.Server.Data;

namespace Otium.Tests;

/// <summary>
/// An unmodified copy of Otium keeps its fee slice: editing or deleting the mining record, changing the miner's pool
/// login or swapping the miner's binary doesn't skip it.
/// </summary>
public sealed class FeeIntegrityTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"otium-integrity-{Guid.NewGuid():N}");

    public FeeIntegrityTests() => Directory.CreateDirectory(_dir);

    // ---------- the mining record ----------

    [Fact]
    public void A_negative_mining_time_in_the_ledger_file_loads_as_zero()
    {
        var path = Path.Combine(_dir, "ledger.json");
        File.WriteAllText(path, """{ "MiningTime": "-3.00:00:00", "EstimatedUserUsd": 1.5 }""");

        var ledger = new JsonFileStore<EarningsLedger>(path).Load();

        Assert.Equal(TimeSpan.Zero, ledger.MiningTime);
        Assert.Equal(1.5, ledger.EstimatedUserUsd);
    }

    [Fact]
    public void The_mining_time_only_grows()
    {
        var ledger = new EarningsLedger { MiningTime = TimeSpan.FromHours(20) };
        ledger.Record(PayoutRole.User, 0, 0, 0, TimeSpan.FromHours(-25), DateOnly.FromDateTime(T0.Date));
        Assert.Equal(TimeSpan.FromHours(20), ledger.MiningTime);

        Assert.False(ledger.RaiseMiningTimeTo(TimeSpan.FromHours(1)));
        Assert.Equal(TimeSpan.FromHours(20), ledger.MiningTime);
        Assert.True(ledger.RaiseMiningTimeTo(TimeSpan.FromHours(23.6)));
        Assert.Equal(TimeSpan.FromHours(23.6), ledger.MiningTime);
    }

    [Fact]
    public void The_server_keeps_the_most_mining_time_reported_and_no_faster_than_the_clock()
    {
        var store = new OtiumStore($"Data Source={Path.Combine(_dir, "otium.db")};Pooling=False");
        store.Upsert(new DeviceRecord("dev", "hash", "acc", T0, T0));
        Assert.Equal((0d, 0d), store.MiningTime("dev"));

        Assert.Equal((3600d, 60d), store.RecordMiningTime("dev", 3600, 60, T0)); // the first report is taken as it is
        Assert.Equal((3600d, 60d), store.RecordMiningTime("dev", 10, 0, T0.AddMinutes(1))); // never lowered
        // Two minutes after the last report a made-up day of mining adds two minutes.
        Assert.Equal((3720d, 60d), store.RecordMiningTime("dev", 3600 + 86_400, null, T0.AddMinutes(3)));
        Assert.Equal((3720d, 60d), store.RecordMiningTime("dev", null, null, T0.AddHours(1))); // older apps report nothing
        Assert.Equal((3720d, 60d), store.MiningTime("dev"));
        Assert.Equal((0d, 0d), store.RecordMiningTime("unknown", 50, 50, T0));
    }

    [Fact]
    public void Version_ten_databases_gain_the_mining_time_columns()
    {
        var db = Path.Combine(_dir, "v10.db");
        using (var connection = new SqliteConnection($"Data Source={db};Pooling=False"))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE devices (device_id TEXT PRIMARY KEY, secret_hash TEXT NOT NULL, account_id TEXT NOT NULL,
                    created_at TEXT NOT NULL, last_seen_at TEXT NOT NULL);
                INSERT INTO devices VALUES ('dev', 'hash', 'acc', '2026-10-01T00:00:00.0000000Z', '2026-10-01T00:00:00.0000000Z');
                PRAGMA user_version = 10;
                """;
            cmd.ExecuteNonQuery();
        }

        var store = new OtiumStore($"Data Source={db};Pooling=False");
        Assert.Equal((0d, 0d), store.MiningTime("dev"));
        Assert.Equal((100d, 0d), store.RecordMiningTime("dev", 100, null, T0));
        Assert.Equal((100d, 0d), new OtiumStore($"Data Source={db};Pooling=False").MiningTime("dev")); // opening again changes nothing
    }

    // ---------- the miner's binary ----------

    [Fact]
    public async Task The_binary_is_checked_against_the_pinned_download_before_each_launch()
    {
        var engine = Path.Combine(_dir, "miner");
        var sha = FakeEngine.Install(engine, "miner-1.0.zip", "miner-1.0/miner.exe");
        var installer = new EngineInstaller(new HttpClient(), engine, "miner.exe", keepArchive: true);
        Assert.True(installer.IsInstalled);

        var binary = await installer.VerifiedBinaryAsync("The miner", "miner-1.0.zip", sha, CancellationToken.None);
        Assert.Equal(Path.GetFullPath(Path.Combine(engine, "miner-1.0", "miner.exe")), binary);
        var hashed = installer.FilesHashed;
        Assert.Equal(binary, await installer.VerifiedBinaryAsync("The miner", "miner-1.0.zip", sha, CancellationToken.None));
        Assert.Equal(hashed, installer.FilesHashed); // unchanged files aren't hashed again

        File.WriteAllText(binary!, "a patched miner that ignores the pool settings");
        var e = await Assert.ThrowsAsync<ProviderProblemException>(() => installer.VerifiedBinaryAsync("The miner", "miner-1.0.zip", sha, CancellationToken.None));
        Assert.Contains("downloading a fresh copy", e.Message);
        Assert.False(installer.IsInstalled); // so the engine installs it again

        // The reinstall puts the real one back.
        var download = File.ReadAllBytes(Path.Combine(engine, "miner-1.0.zip"));
        var reinstaller = new EngineInstaller(new HttpClient(new Files(download)), engine, "miner.exe", keepArchive: true) { InstallSettle = TimeSpan.Zero };
        await reinstaller.InstallAsync("The miner", new Uri("https://example.test/miner-1.0.zip"), "miner-1.0.zip", sha, null, CancellationToken.None);
        Assert.True(reinstaller.IsInstalled);
        Assert.Equal(binary, await reinstaller.VerifiedBinaryAsync("The miner", "miner-1.0.zip", sha, CancellationToken.None));
        Assert.Equal("the real miner", File.ReadAllText(binary!));
    }

    [Fact]
    public async Task A_swapped_download_or_an_install_that_didnt_keep_it_is_not_trusted()
    {
        var engine = Path.Combine(_dir, "miner");
        var sha = FakeEngine.Install(engine, "miner-1.0.zip", "miner-1.0/miner.exe");
        var installer = new EngineInstaller(new HttpClient(), engine, "miner.exe", keepArchive: true);

        // The pinned hash doesn't match (e.g. the download and the binary both swapped).
        await Assert.ThrowsAsync<ProviderProblemException>(() =>
            installer.VerifiedBinaryAsync("The miner", "miner-1.0.zip", new string('0', 64), CancellationToken.None));

        // An install from before downloads were kept installs again.
        File.WriteAllText(Path.Combine(engine, ".verified"), sha);
        Assert.False(installer.IsInstalled);
        Assert.True(new EngineInstaller(new HttpClient(), engine, "miner.exe").IsInstalled); // other engines don't keep it
    }

    // ---------- XMRig's pool settings ----------

    [Fact]
    public async Task A_pool_login_changed_through_xmrigs_api_restarts_it_with_otiums_settings()
    {
        var (provider, api, launches) = Xmrig(out var time);
        var wallet = TestAddresses.Generate();
        await provider.StartAsync(new ProviderRunOptions(new MiningTarget(wallet, PayoutRole.App), "w", 50), CancellationToken.None);
        var launch = Assert.Single(launches);
        Assert.Contains(launch.Arguments, a => a.StartsWith("--config=", StringComparison.Ordinal));

        // Within the first minute the pool isn't asked about.
        time.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(5000, (await provider.GetStatusAsync(CancellationToken.None)).HashrateHps);
        Assert.Equal(0, api.ConfigRequests);

        // Someone points the fee slice at their own address through the API (PUT /1/config).
        api.PoolUser = TestAddresses.Generate();
        time.Advance(TimeSpan.FromSeconds(31));
        var status = await provider.GetStatusAsync(CancellationToken.None);
        Assert.Equal(ProviderState.Starting, status.State);
        Assert.Equal(0, status.HashrateHps);
        Assert.Equal(2, launches.Count);
        Assert.True(launches[0].Process.Disposed);
        Assert.Contains(provider.RecentLog, l => l.Contains("pool settings were changed outside Otium"));
        var pool = JsonNode.Parse(File.ReadAllText(api.ConfigPath))!["pools"]![0]!;
        Assert.Equal(wallet, pool["user"]!.GetValue<string>()); // the file says Otium's login again

        // The restarted miner runs Otium's settings: no more restarts.
        api.PoolUser = null;
        time.Advance(TimeSpan.FromSeconds(61));
        Assert.Equal(5000, (await provider.GetStatusAsync(CancellationToken.None)).HashrateHps);
        Assert.Equal(2, launches.Count);
    }

    [Fact]
    public async Task An_api_that_refuses_otium_or_lists_a_second_pool_also_restarts_the_miner()
    {
        var (provider, api, launches) = Xmrig(out var time);
        await provider.StartAsync(new ProviderRunOptions(new MiningTarget(TestAddresses.Generate(), PayoutRole.App), "w", 50), CancellationToken.None);

        api.ConfigStatus = HttpStatusCode.Forbidden; // restricted, or another token set through the API
        time.Advance(XmrigProvider.ConfigCheckInterval);
        await provider.GetStatusAsync(CancellationToken.None);
        Assert.Equal(2, launches.Count);

        api.ConfigStatus = HttpStatusCode.OK;
        api.ExtraPool = true; // a pool of their own to fail over to
        time.Advance(XmrigProvider.ConfigCheckInterval);
        await provider.GetStatusAsync(CancellationToken.None);
        Assert.Equal(3, launches.Count);

        // An API that doesn't answer (busy, or the miner suspended) doesn't restart it: it just reports no hashrate.
        api.ExtraPool = false;
        api.Unreachable = true;
        time.Advance(XmrigProvider.ConfigCheckInterval);
        var status = await provider.GetStatusAsync(CancellationToken.None);
        Assert.Equal(3, launches.Count);
        Assert.Equal(0, status.HashrateHps);
    }

    [Theory]
    [InlineData("gulf.moneroocean.stream:20128", "4abc", true)]
    [InlineData("stratum+ssl://gulf.moneroocean.stream:20128", "4abc", true)]
    [InlineData("gulf.moneroocean.stream:20128", "4someone-else", false)]
    [InlineData("pool.example:3333", "4abc", false)]
    public void Only_otiums_pool_and_login_count_as_unchanged(string url, string user, bool unchanged)
    {
        var config = new JsonObject { ["pools"] = new JsonArray { new JsonObject { ["url"] = url, ["user"] = user, ["enabled"] = true } } };
        Assert.Equal(unchanged, XmrigConfig.MinesOnlyTo(config.ToJsonString(), "gulf.moneroocean.stream:20128", "4abc"));
        Assert.False(XmrigConfig.MinesOnlyTo("""{"pools":[]}""", "gulf.moneroocean.stream:20128", "4abc"));
        Assert.False(XmrigConfig.MinesOnlyTo("not json", "gulf.moneroocean.stream:20128", "4abc"));
    }

    private (XmrigProvider Provider, XmrigApi Api, List<(IReadOnlyList<string> Arguments, FakeXmrig Process)> Launches) Xmrig(out ManualTime time)
    {
        var enginesRoot = Path.Combine(_dir, "engines");
        var engineDir = Path.Combine(enginesRoot, $"xmrig-{XmrigRelease.Version}");
        var sha = FakeEngine.Install(engineDir, "xmrig-test.zip", $"xmrig-{XmrigRelease.Version}/{XmrigRelease.BinaryName}");
        var api = new XmrigApi(Path.Combine(engineDir, "config.json"));
        var launches = new List<(IReadOnlyList<string>, FakeXmrig)>();
        var http = new HttpClient(api);
        var clock = new ManualTime(T0);
        time = clock;
        var provider = new XmrigProvider(http, new MoneroOceanClient(http), enginesRoot, unitedStates: true)
        {
            Asset = new XmrigAsset("xmrig-test.zip", sha),
            Time = clock,
            Launch = (_, arguments, _) =>
            {
                var process = new FakeXmrig();
                launches.Add((arguments, process));
                return process;
            },
        };
        return (provider, api, launches);
    }

    private sealed class FakeXmrig : IXmrigProcess
    {
        public bool Disposed { get; private set; }

        public bool HasExited => Disposed;

        public int ExitCode => 0;

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>XMRig's HTTP API: its config is what Otium wrote, unless a test changes it.</summary>
    private sealed class XmrigApi(string configPath) : HttpMessageHandler
    {
        public string ConfigPath => configPath;
        public string? PoolUser { get; set; }
        public bool ExtraPool { get; set; }
        public bool Unreachable { get; set; }
        public HttpStatusCode ConfigStatus { get; set; } = HttpStatusCode.OK;
        public int ConfigRequests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (Unreachable)
                throw new HttpRequestException("The miner doesn't answer.");
            switch (request.RequestUri!.AbsolutePath)
            {
                case "/2/summary":
                    return Json("""{"hashrate":{"total":[5000.0,5000.0,null]},"connection":{"uptime":60}}""");
                case "/1/config":
                    ConfigRequests++;
                    if (ConfigStatus != HttpStatusCode.OK)
                        return Task.FromResult(new HttpResponseMessage(ConfigStatus));
                    var config = JsonNode.Parse(File.ReadAllText(configPath))!;
                    var pools = config["pools"]!.AsArray();
                    if (PoolUser is { } user)
                        pools[0]!["user"] = user;
                    if (ExtraPool)
                        pools.Add(new JsonObject { ["url"] = "pool.example:3333", ["user"] = TestAddresses.Generate() });
                    return Json(config.ToJsonString());
                default:
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }
        }

        private static Task<HttpResponseMessage> Json(string body) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }

    /// <summary>Serves one download.</summary>
    private sealed class Files(byte[] content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

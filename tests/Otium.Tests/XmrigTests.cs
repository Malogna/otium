using System.Runtime.InteropServices;
using System.Text.Json;
using Otium.Core.Providers;
using Otium.Providers.XMRig;

namespace Otium.Tests;

public class XmrigTests
{
    [Fact]
    public void Config_points_the_pool_at_the_target_wallet_with_low_priority()
    {
        // A US user's Monero: MoneroOcean, the bare address, the worker as password.
        var wallet = TestAddresses.Generate();
        var json = XmrigConfig.Build(
            new ProviderRunOptions(new MiningTarget(wallet, PayoutRole.User), "sc 1234/abcd", 40),
            PoolEndpoint.MoneroOcean, httpPort: 45678, accessToken: "secret", unitedStates: true);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var pool = root.GetProperty("pools")[0];
        Assert.Equal(wallet, pool.GetProperty("user").GetString());
        Assert.Equal("gulf.moneroocean.stream:20128", pool.GetProperty("url").GetString());
        Assert.True(pool.GetProperty("tls").GetBoolean());
        Assert.Equal("sc1234abcd", pool.GetProperty("pass").GetString());
        Assert.Equal(40, root.GetProperty("cpu").GetProperty("max-threads-hint").GetInt32());
        Assert.Equal(0, root.GetProperty("cpu").GetProperty("priority").GetInt32());
        Assert.Equal("127.0.0.1", root.GetProperty("http").GetProperty("host").GetString());
        Assert.Equal(45678, root.GetProperty("http").GetProperty("port").GetInt32());
        Assert.False(root.GetProperty("http").GetProperty("restricted").GetBoolean()); // pause and resume need it; loopback and token-only
        Assert.Equal(1, root.GetProperty("donate-level").GetInt32());
        Assert.False(root.GetProperty("opencl").GetBoolean());
    }

    [Fact]
    public async Task Pause_and_resume_send_plain_json_because_xmrig_refuses_a_charset()
    {
        // XMRig answered 415 to "application/json; charset=utf-8", so "keep the miner ready" stopped it instead.
        using var content = XmrigProvider.RpcContent("pause");
        Assert.Equal("application/json", content.Headers.ContentType?.ToString());
        Assert.Equal("""{"method":"pause","id":1,"jsonrpc":"2.0"}""", await content.ReadAsStringAsync());
    }

    [Fact]
    public void While_the_user_is_away_the_miner_runs_at_normal_priority()
    {
        var json = XmrigConfig.Build(
            new ProviderRunOptions(new MiningTarget(TestAddresses.Generate(), PayoutRole.User), "w", 50, LowPriority: false),
            PoolEndpoint.MoneroOcean, httpPort: 45678, accessToken: "secret", unitedStates: true);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal(2, doc.RootElement.GetProperty("cpu").GetProperty("priority").GetInt32());
    }

    [Fact]
    public void The_servers_pool_route_replaces_the_default_pool_and_login()
    {
        var route = new Otium.Core.Accounts.CpuPoolRoute("kryptex", "xmr.kryptex.network", 8029, true, "otium.ot1234abcd5678", "x");
        var json = XmrigConfig.Build(
            new ProviderRunOptions(new MiningTarget(TestAddresses.Generate(), PayoutRole.User), "ot1234abcd5678", 50, route),
            PoolEndpoint.MoneroOcean, httpPort: 45678, accessToken: "secret");

        using var doc = JsonDocument.Parse(json);
        var pool = doc.RootElement.GetProperty("pools")[0];
        Assert.Equal("xmr.kryptex.network:8029", pool.GetProperty("url").GetString());
        Assert.Equal("otium.ot1234abcd5678", pool.GetProperty("user").GetString());
        Assert.Equal("x", pool.GetProperty("pass").GetString());
        Assert.True(pool.GetProperty("tls").GetBoolean());
    }

    [Fact]
    public void Summary_prefers_the_sixty_second_average()
    {
        const string json = """
            {"hashrate":{"total":[5120.5,4987.25,null]},"connection":{"uptime":120},"results":{"shares_good":7}}
            """;
        var summary = XmrigSummary.Parse(json);
        Assert.Equal(4987.25, summary.HashrateHps);
        Assert.True(summary.PoolConnected);
        Assert.Equal(7, summary.AcceptedShares);
    }

    [Fact]
    public void Summary_falls_back_to_ten_second_rate_while_warming_up()
    {
        const string json = """{"hashrate":{"total":[3000.0,null,null]},"connection":{"uptime":0}}""";
        var summary = XmrigSummary.Parse(json);
        Assert.Equal(3000.0, summary.HashrateHps);
        Assert.False(summary.PoolConnected);
    }

    [Fact]
    public void Summary_with_no_hashrate_is_zero()
    {
        Assert.Equal(0, XmrigSummary.Parse("""{"hashrate":{"total":[null,null,null]}}""").HashrateHps);
    }

    [Theory]
    [InlineData("windows", Architecture.X64, "xmrig-6.26.0-windows-x64.zip")]
    [InlineData("windows", Architecture.Arm64, "xmrig-6.26.0-windows-arm64.zip")]
    [InlineData("osx", Architecture.Arm64, "xmrig-6.26.0-macos-arm64.tar.gz")]
    [InlineData("osx", Architecture.X64, "xmrig-6.26.0-macos-x64.tar.gz")]
    [InlineData("linux", Architecture.X64, "xmrig-6.26.0-linux-static-x64.tar.gz")]
    public void Release_asset_matches_platform(string os, Architecture arch, string expected)
    {
        var platform = os switch { "windows" => OSPlatform.Windows, "osx" => OSPlatform.OSX, _ => OSPlatform.Linux };
        var asset = XmrigRelease.For(platform, arch);
        Assert.NotNull(asset);
        Assert.Equal(expected, asset.FileName);
        Assert.Equal(64, asset.Sha256.Length);
        Assert.StartsWith("https://github.com/xmrig/xmrig/releases/download/v6.26.0/", asset.DownloadUrl.ToString());
    }

    [Fact]
    public void Linux_arm_has_no_official_build()
    {
        Assert.Null(XmrigRelease.For(OSPlatform.Linux, Architecture.Arm64));
    }
}

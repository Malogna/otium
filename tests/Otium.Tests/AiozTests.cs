using System.Runtime.InteropServices;
using System.Text.Json;
using Otium.Core.Bandwidth;
using Otium.Providers.Aioz;

namespace Otium.Tests;

/// <summary>
/// Sharing storage and upload bandwidth through AIOZ's DePIN node: the pinned 1.2.5 node per platform, its own local
/// port, its balance read in AIOZ and shown in dollars, withdrawals to the user's address, and the storage limit Otium
/// enforces because 1.2.5 has none.
/// </summary>
public class AiozTests
{
    [Theory]
    [InlineData("WINDOWS", Architecture.X64, "aioz-depin-windows-amd64-1.2.5.zip", "aioz-depin-cli-windows-amd64.exe")]
    [InlineData("LINUX", Architecture.X64, "aioz-depin-linux-amd64-1.2.5.tar.gz", "aioz-depin-cli-linux-amd64")]
    [InlineData("OSX", Architecture.Arm64, null, null)] // 1.2.5's Mac archives don't say which is for Apple silicon
    [InlineData("OSX", Architecture.X64, null, null)]
    [InlineData("WINDOWS", Architecture.Arm64, null, null)]
    [InlineData("LINUX", Architecture.Arm64, null, null)]
    public void Each_platform_gets_the_pinned_node(string os, Architecture arch, string? file, string? binary)
    {
        var asset = AiozRelease.For(OSPlatform.Create(os), arch);
        Assert.Equal(file, asset?.FileName);
        Assert.Equal(binary, asset?.BinaryName);
        if (asset is not null)
        {
            Assert.Matches("^[0-9a-f]{64}$", asset.Sha256);
            Assert.Equal($"https://github.com/AIOZNetwork/aioz-depin-cli/releases/download/v1.2.5/{file}", asset.DownloadUrl.ToString());
        }
    }

    [Fact]
    public void The_node_listens_on_its_own_loopback_port_and_keeps_its_files_in_otiums_folder()
    {
        var args = AiozBandwidthProvider.StartArguments("/data/home", "/data/privkey.json", 41234);
        Assert.Equal("start", args[0]);
        Assert.Contains("--laddr=tcp://127.0.0.1:41234", args); // not AIOZ's default 1317, often reserved on Windows
        Assert.Contains("--home=/data/home", args); // or it would use ~/.aiozworker
        Assert.Contains("--priv-key-file=/data/privkey.json", args);
        Assert.Contains("--no-auto-update", args); // Otium pins the version
    }

    [Fact]
    public void Withdrawals_go_to_the_users_address_in_whole_aioz()
    {
        var args = AiozBandwidthProvider.WithdrawArguments("/data/home", "/data/privkey.json", 41234, "0xabc", 7m);
        Assert.Equal(["reward", "withdraw"], args.Take(2));
        Assert.Contains("--address=0xabc", args);
        Assert.Contains("--amount=7aioz", args);
        Assert.Contains("--endpoint=http://127.0.0.1:41234", args);
        Assert.Contains("--home=/data/home", args);
    }

    [Fact]
    public void The_balance_is_read_in_aioz_from_its_smallest_unit()
    {
        var balance = AiozBandwidthProvider.ParseBalance("""
            {
              "balance": [{"denom": "attoaioz", "amount": "2500000000000000000"}],
              "storage_earned": [{"denom": "attoaioz", "amount": "3000000000000000000"}],
              "delivery_earned": [{"denom": "attoaioz", "amount": "500000000000000000"}, {"denom": "other", "amount": "9"}],
              "transcoding_earned": [],
              "withdraw": [{"denom": "attoaioz", "amount": "1000000000000000000"}],
              "delivery_counter": 3
            }
            """);
        Assert.Equal(2.5m, balance.Waiting);
        Assert.Equal(3.5m, balance.Earned);
        Assert.Equal(1m, balance.Withdrawn);

        // A brand-new node: every list empty.
        var empty = AiozBandwidthProvider.ParseBalance("""{"balance":[],"storage_earned":[],"delivery_earned":[],"transcoding_earned":[],"withdraw":[],"delivery_counter":0}""");
        Assert.Equal(0m, empty.Earned);
    }

    [Fact]
    public void Stats_say_what_the_node_stores()
    {
        var stats = AiozBandwidthProvider.ParseStats("""{"storage":{"total_count":4,"total_size":311320},"delivery":{"upstream_speed":0},"transcoding":{"status":"Standby"}}""");
        Assert.Equal(4, stats.StoredFiles);
        Assert.Equal("4 files stored for AIOZ (304 KB)", AiozBandwidthProvider.ActivityText(stats));
        Assert.Equal("Waiting for AIOZ to send files", AiozBandwidthProvider.ActivityText(stats with { StoredFiles = 0 }));
    }

    [Fact]
    public void Earnings_turn_into_dollars_at_todays_price()
    {
        var start = new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
        var earned = AiozEarned.Empty.With(start, 1.0m, null);
        Assert.Null(earned.UserUsd); // no price yet

        earned = earned.With(start.AddHours(12), 3.0m, 0.25);
        Assert.Equal(3.0 * 0.25, earned.UserUsd!.Value, 9);
        Assert.Equal(2.0 * 0.25 / 12 * 24, earned.UserUsdPerDay(start.AddHours(12))!.Value, 9);

        var restored = JsonSerializer.Deserialize<AiozEarned>(JsonSerializer.Serialize(earned))!;
        Assert.Equal(earned.UserUsd, restored.UserUsd);
    }

    [Fact]
    public async Task The_node_pauses_once_its_files_reach_the_users_storage_limit()
    {
        var root = Directory.CreateTempSubdirectory("otium-aioz-").FullName;
        try
        {
            var data = Directory.CreateDirectory(Path.Combine(root, "aioz-data", "home", "data")).FullName;
            await using (var file = File.Create(Path.Combine(data, "piece")))
                file.SetLength(AiozBandwidthProvider.LimitBytes(1) + 1);

            await using var provider = new AiozBandwidthProvider(new HttpClient(), root, () => "0xabc", () => 1,
                AiozRelease.For(OSPlatform.Windows, Architecture.X64));
            // Not started, and no node needed: the limit is checked first.
            await provider.StartAsync(new BandwidthRunOptions("worker"), CancellationToken.None);
            var status = await provider.GetStatusAsync(CancellationToken.None);
            Assert.Equal(BandwidthProviderState.Error, status.State);
            Assert.Contains("1 GB storage limit", status.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Without_an_0x_address_it_asks_for_one()
    {
        var provider = new AiozBandwidthProvider(new HttpClient(), Path.GetTempPath(), () => null, () => 20,
            AiozRelease.For(OSPlatform.Windows, Architecture.X64));
        Assert.Equal("Add an 0x address for AIOZ payouts.", provider.UnavailableReason);
        Assert.Equal("aioz", provider.Id);
    }
}

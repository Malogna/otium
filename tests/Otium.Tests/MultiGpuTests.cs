using System.Runtime.InteropServices;
using Otium.Core.Gpu;
using Otium.Core.Switching;
using Otium.Providers.SrbMiner;
using Otium.Server.RealEarnings;

namespace Otium.Tests;

/// <summary>
/// Computers with more than one graphics card: each card is judged on its own, a coin runs on the cards that can mine
/// it (SRBMiner told which by PCI address), the processor's built-in graphics is left out, and multi-card earnings stay
/// out of the website's per-card figures.
/// </summary>
public sealed class MultiGpuTests
{
    private static readonly GpuInfo Rtx5090 = new(GpuVendor.Nvidia, 0, "NVIDIA GeForce RTX 5090", 32, "01:00.0");
    private static readonly GpuInfo Rtx3080 = new(GpuVendor.Nvidia, 1, "NVIDIA GeForce RTX 3080", 10, "02:00.0");
    private static readonly GpuInfo Rtx3050 = new(GpuVendor.Nvidia, 1, "NVIDIA GeForce RTX 3050", 6, "02:00.0");
    private static readonly GpuInfo Rx6700 = new(GpuVendor.Amd, 0, "AMD Radeon RX 6700 XT", 12, "03:00.0");

    /// <summary>What `SRBMiner-MULTI --list-devices` printed on a real RTX 3080 computer, plus a second card and an AMD one.</summary>
    private const string DeviceList = """
        [2026-10-20 20:17:50] Found 1 OpenCL platforms, but none is AMD, NVIDIA OR INTEL
        CUDA devices

        GPU0  [CUDA][0] [0000:02:00.0] : nvidia_geforce_rtx_3080 [ampere] [CC: 8.6] [SM: 70] [12287 MB]
        GPU1  [CUDA][1] [0000:01:00.0] : nvidia_geforce_rtx_5090 [blackwell] [CC: 12.0] [SM: 170] [32607 MB]
        GPU2  [OPENCL][0] [0000:03:00.0] : amd_radeon_rx_6700_xt [12272 MB]
        """;

    private static GpuPolicy Policy(IReadOnlyList<string>? pearlOnly) => new(
        DateTimeOffset.UtcNow,
        [new GpuAlgorithmPolicy("pearlhash", GpuTiers.Primary, "unmineable-pearlhash", 3.4e-14, 0.75, OnlyGpus: pearlOnly)],
        [new GpuPoolRoute("unmineable-pearlhash", GpuRoutes.Unmineable, "pearlpow.unmineable.com", 443, null, Tls: true)]);

    private static SrbMinerGpuProvider Gpu(GpuPolicy policy, string? devices, params GpuInfo[] gpus) => new(
        new HttpClient(), null!, Path.GetTempPath(), Path.Combine(Path.GetTempPath(), $"otium-bench-{Guid.NewGuid():N}.json"),
        gpus, null!, () => 0.15, asset: SrbMinerRelease.For(OSPlatform.Windows, Architecture.X64),
        policy: () => policy, referralCode: "ztjq-jr7s",
        listDevices: _ => devices is null ? null : SrbMinerArgs.ParseDeviceList(devices));

    private static async Task<IEnumerable<string>> Coins(SrbMinerGpuProvider provider) =>
        (await provider.QuotesAsync(CancellationToken.None)).Select(q => q.Algorithm.PoolName);

    [Fact]
    public void Pci_addresses_from_nvml_srbminer_windows_and_linux_match()
    {
        Assert.Equal("01:00.0", GpuInfo.NormalizePciBus("00000000:01:00.0")); // NVML
        Assert.Equal("01:00.0", GpuInfo.NormalizePciBus("0000:01:00.0")); // SRBMiner, Linux sysfs
        Assert.Equal("1a:00.0", GpuInfo.NormalizePciBus("PCI bus 26, device 0, function 0")); // Windows
        Assert.Null(GpuInfo.NormalizePciBus("not a bus"));

        var ids = SrbMinerArgs.ParseDeviceList(DeviceList);
        Assert.Equal(0, ids["02:00.0"]);
        Assert.Equal(1, ids["01:00.0"]); // SRBMiner's order isn't NVML's: matched by address, not position
        Assert.Equal(2, ids["03:00.0"]);
    }

    [Fact]
    public async Task One_card_a_coin_doesnt_take_no_longer_keeps_it_from_the_other()
    {
        string[] blackwell = [@"\bRTX 50\d0\b"];
        // Pearl after its fork: only the 5090 qualifies. With SRBMiner's card numbers it mines Pearl on the 5090 alone.
        Assert.Contains("pearlhash", await Coins(Gpu(Policy(blackwell), DeviceList, Rtx5090, Rtx3080)));
        // A card with too little memory (Pearl needs 8 GB) is left out the same way.
        Assert.Contains("pearlhash", await Coins(Gpu(Policy(null), DeviceList, Rtx3080, Rtx3050)));
        // Without the card numbers there's no way to leave the 3080 out, so Pearl isn't offered.
        Assert.DoesNotContain("pearlhash", await Coins(Gpu(Policy(blackwell), null, Rtx5090, Rtx3080)));
        // Different vendors need no numbers: the vendor switches do it.
        Assert.Contains("pearlhash", await Coins(Gpu(Policy(["NVIDIA"]), null, Rtx3080, Rx6700)));
    }

    [Fact]
    public void Srbminer_is_told_exactly_which_cards_mine()
    {
        var pearl = GpuAlgorithms.Find("pearlhash")!;
        var login = new PoolLogin(new GpuPoolEndpoint("pearlpow.unmineable.com", 443, Tls: true), "USDT:T.ot1234abcdg#ztjq-jr7s", "x", null);
        var ids = SrbMinerArgs.ParseDeviceList(DeviceList);

        var only5090 = SrbMinerArgs.Build(pearl, login, [Rtx5090], allGpus: [Rtx5090, Rtx3080, Rx6700], deviceIds: ids);
        Assert.Equal("1", only5090[only5090.ToList().IndexOf("--gpu-id") + 1]);
        Assert.Contains("--disable-gpu-amd", only5090);

        var nvidiaOnly = SrbMinerArgs.Build(pearl, login, [Rtx5090, Rtx3080], allGpus: [Rtx5090, Rtx3080, Rx6700], deviceIds: ids);
        Assert.DoesNotContain("--gpu-id", nvidiaOnly); // every NVIDIA card: the vendor switch is enough
        Assert.Contains("--disable-gpu-amd", nvidiaOnly);

        var everything = SrbMinerArgs.Build(pearl, login, [Rtx5090, Rtx3080, Rx6700]);
        Assert.DoesNotContain("--gpu-id", everything);
        Assert.DoesNotContain("--disable-gpu-amd", everything);
        Assert.DoesNotContain("--disable-gpu-nvidia", everything);
    }

    [Fact]
    public void The_processors_built_in_graphics_isnt_a_mining_card()
    {
        Assert.False(GpuDetector.IsMiningCard(new GpuInfo(GpuVendor.Amd, 0, "AMD Radeon(TM) Graphics", 0.5))); // Ryzen 7000/9000
        Assert.False(GpuDetector.IsMiningCard(new GpuInfo(GpuVendor.Amd, 0, "AMD Radeon(TM) Graphics", 2)));
        Assert.True(GpuDetector.IsMiningCard(Rx6700));
        Assert.True(GpuDetector.IsMiningCard(new GpuInfo(GpuVendor.Amd, 0, "AMD Radeon", 0))); // unknown memory: kept
        Assert.True(GpuDetector.IsMiningCard(Rtx3050));
    }

    [Fact]
    public void Hardware_stats_read_as_one_short_line_whatever_the_language()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("nb-NO"); // decimal comma
        try
        {
            // What an RTX 3080 reported through NVML on 20 October 2026.
            var card = new GpuStats("NVIDIA GeForce RTX 3080", 61, 38, 36, 50, 285, 1.5, 12);
            Assert.Equal("61 °C · 38% load · fan 36% · 50 W · 285 MHz · 1.5 of 12 GB memory", card.Describe());
            Assert.Equal("98% load", new CpuStats(98.4, null).Describe()); // no temperature on Windows
            Assert.Null(new GpuStats("AMD Radeon RX 6700 XT", null, null, null, null, null, null, 12).Describe());
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Several_cards_are_named_and_reported_as_a_whole_computer()
    {
        Assert.Equal("NVIDIA GeForce RTX 3080", GpuInfo.NamesOf([Rtx3080]));
        Assert.Equal("2× NVIDIA GeForce RTX 3080", GpuInfo.NamesOf([Rtx3080, Rtx3080 with { Index = 2, PciBus = "04:00.0" }]));
        Assert.Equal("NVIDIA GeForce RTX 5090 + AMD Radeon RX 6700 XT", GpuInfo.NamesOf([Rtx5090, Rx6700]));
        Assert.Equal("3 graphics cards", GpuInfo.NamesOf([Rtx5090, Rtx3080, Rx6700]));
        Assert.Null(GpuInfo.NamesOf([]));

        Assert.Equal("NVIDIA GeForce RTX 3080", GpuInfo.ReportedModel([Rtx3080]));
        Assert.Equal("multi:3", GpuInfo.ReportedModel([Rtx5090, Rtx3080, Rx6700]));
        Assert.Equal(3, EarningsReportService.MultiCardCount("multi:3"));
        Assert.Null(EarningsReportService.MultiCardCount("multi:40"));
        Assert.Null(EarningsReportService.MultiCardCount("RTX 3080"));
    }
}

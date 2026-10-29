using Otium.Core.Gpu;
using Otium.Core.Switching;
using Otium.Providers.SrbMiner;

namespace Otium.Tests;

/// <summary>
/// Tuning SRBMiner's intensity without administrator rights: which settings are compared, how they're judged, and
/// what the miner's console says about its own pick.
/// </summary>
public class GpuTuningTests
{
    private static readonly DateTimeOffset T = new(2026, 10, 29, 22, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Settings_are_tried_around_the_miners_own_pick()
    {
        Assert.Equal([null, 21, 19, 25], IntensityTuner.Candidates("quantus", 23));
        Assert.Equal([null, 28, 26], IntensityTuner.Candidates("quantus", 30)); // never past 31
        // Pearl prints no pick (SRBMiner tunes it itself): fixed steps to compare against that.
        Assert.Equal([null, 12, 16, 20, 24], IntensityTuner.Candidates("pearlhash", null));
        Assert.Equal([(int?)null], IntensityTuner.Candidates("xelishash", null)); // nothing known to try
    }

    [Fact]
    public void The_next_trial_is_the_first_setting_not_measured_yet()
    {
        IReadOnlyList<int?> candidates = [null, 21, 19];
        var trials = new List<IntensityTrial> { new("quantus", null, 268e6, 180, T) };
        Assert.Equal(21, IntensityTuner.NextTrial(candidates, trials, out var any));
        Assert.True(any);
        trials.Add(new("quantus", 21, 480e6, 314, T));
        trials.Add(new("quantus", 19, 0, null, T, Failed: true)); // a failed one isn't tried again
        Assert.Null(IntensityTuner.NextTrial(candidates, trials, out any));
        Assert.False(any);
    }

    [Fact]
    public void The_best_setting_is_what_the_user_keeps_after_electricity()
    {
        // Quantus at $4.5e-9 per H/s a day and $0.15/kWh: 21 earns more after power, though it draws more (the RTX 3080 run).
        static double Net(double hashrate, double? watts) => hashrate * 4.5e-9 - (watts ?? 0) * 24 / 1000 * 0.15;
        var trials = new List<IntensityTrial>
        {
            new("quantus", null, 268e6, 180, T),
            new("quantus", 21, 480e6, 314, T),
            new("quantus", 19, 900e6, 2000, T), // fast, but its power costs more than it earns
            new("quantus", 25, 2e9, 100, T, Failed: true),
        };
        Assert.Equal(21, IntensityTuner.Best(trials, Net));

        // Without measured watts for every setting, the fastest working one wins.
        trials.Add(new("quantus", 17, 500e6, null, T));
        Assert.Equal(19, IntensityTuner.Best(trials, Net));
        Assert.Null(IntensityTuner.Best([], Net)); // nothing measured: the miner's own pick
    }

    [Theory]
    [InlineData("[2026-10-29 20:09:26] GPU0 |A0|quantus             |INTENSITY:         23|", 23, true)]
    [InlineData("GPU0 |A0|fishhash            |INTENSITY:       AUTO|", null, true)]
    [InlineData("[2026-10-29 20:09:32] Connected to quantus.unmineable.com:443 [quantus]", null, false)]
    public void The_miners_own_pick_is_read_from_its_startup_line(string line, int? intensity, bool found)
    {
        Assert.Equal(intensity, SrbMinerStats.ParseIntensity(line, out var seen));
        Assert.Equal(found, seen);
    }

    [Fact]
    public void Tuning_results_are_kept_with_the_benchmarks_until_the_card_is_tested_again()
    {
        var path = Path.Combine(Path.GetTempPath(), $"otium-tuning-{Guid.NewGuid():N}.json");
        var card = new GpuInfo(GpuVendor.Nvidia, 0, "NVIDIA GeForce RTX 3080", 12);
        var time = new ManualTime(T);
        try
        {
            var store = new BenchmarkStore(path, [card], time);
            store.RecordTrial(new IntensityTrial("quantus", 21, 480e6, 314, T));
            store.RecordTrial(new IntensityTrial("quantus", 21, 470e6, 310, T)); // the same setting again replaces it
            store.RecordTrial(new IntensityTrial("quantus", null, 268e6, 180, T, MinerPick: 23));

            var reloaded = new BenchmarkStore(path, [card], time);
            Assert.Equal([470e6, 268e6], reloaded.Tuning("quantus").Select(t => t.HashrateHps));
            Assert.Equal(23, reloaded.Tuning("quantus").Single(t => t.Intensity is null).MinerPick);
            Assert.Empty(reloaded.Tuning("pearlhash"));

            time.Advance(IntensityTuner.Expiry + TimeSpan.FromMinutes(1));
            Assert.Empty(reloaded.Tuning("quantus")); // measured again after a month

            reloaded.RecordTrial(new IntensityTrial("quantus", 21, 480e6, 314, time.GetUtcNow()));
            reloaded.Clear(); // "Test GPU again"
            Assert.Empty(new BenchmarkStore(path, [card], time).Tuning("quantus"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void The_intensity_goes_to_srbminer_as_one_value_for_every_card()
    {
        var login = new PoolLogin(new GpuPoolEndpoint("quantus.unmineable.com", 443, Tls: true), "USDT:T", "x");
        var card = new GpuInfo(GpuVendor.Nvidia, 0, "NVIDIA GeForce RTX 3080", 12);
        var args = SrbMinerArgs.Build(GpuAlgorithms.Find("quantus")!, login, [card], intensity: 21);
        Assert.Equal("21", args[args.ToList().IndexOf("--gpu-intensity") + 1]);
        Assert.DoesNotContain("--gpu-intensity", SrbMinerArgs.Build(GpuAlgorithms.Find("quantus")!, login, [card]));
    }
}

using System.Runtime.InteropServices;
using Otium.Core.Gpu;

namespace Otium.Core.Projections;

/// <summary>
/// Typical speeds for common hardware, used to show "what your PC could earn" before anything has run. These are
/// deliberately middle-of-the-road numbers; once the miners run, real measurements replace them.
/// </summary>
public static class HardwareEstimates
{
    /// <summary>RandomX hashrate per logical CPU thread. Desktop Ryzen/Intel parts land around 400–800 H/s per thread.</summary>
    public const double CpuHpsPerThread = 450;

    public const double AppleSiliconHpsPerThread = 250;

    /// <summary>KawPow MH/s and typical board power (W) by GPU model, keyed by a distinctive part of the name.</summary>
    private static readonly (string Key, double Mhs, double Watts)[] Gpus =
    [
        ("5090", 95, 400), ("5080", 70, 300), ("5070 ti", 55, 250), ("5070", 45, 220), ("5060", 28, 150),
        ("4090", 65, 350), ("4080", 50, 280), ("4070 ti", 42, 240), ("4070", 33, 190), ("4060 ti", 26, 150), ("4060", 22, 120),
        ("3090", 50, 330), ("3080 ti", 48, 320), ("3080", 45, 300), ("3070 ti", 32, 240), ("3070", 30, 200),
        ("3060 ti", 26, 180), ("3060", 23, 150), ("2080", 31, 200), ("2070", 25, 170), ("2060", 18, 140),
        ("1080 ti", 22, 200), ("1080", 17, 160), ("1070", 14, 130), ("1660", 11, 100),
        ("7900 xtx", 45, 300), ("7900 xt", 40, 280), ("7800", 32, 230), ("7700", 26, 200), ("7600", 18, 140),
        ("6950", 36, 280), ("6900", 34, 270), ("6800", 30, 230), ("6750", 24, 180), ("6700", 22, 170), ("6650", 16, 140),
        ("6600", 14, 120), ("5700", 18, 160), ("5600", 15, 140), ("580", 11, 140), ("570", 10, 130),
    ];

    /// <summary>
    /// Pearl (pearlhash) TH/s by GPU model. Pearl runs on tensor cores, so GTX cards and unknown models get none.
    /// Rough figures back-calculated from WhatToMine's October 2026 profitability (RTX 4090 ≈ 310 TH/s); the miner
    /// benchmarks the real speed on first run.
    /// </summary>
    private static readonly (string Key, double Ths)[] PearlSpeeds =
    [
        ("5090", 300), ("5080", 190), ("5070 ti", 150), ("5070", 110), ("5060", 60),
        ("4090", 310), ("4080", 200), ("4070 ti", 150), ("4070", 100), ("4060 ti", 70), ("4060", 55),
        ("3090", 120), ("3080 ti", 110), ("3080", 104), ("3070 ti", 80), ("3070", 75), ("3060 ti", 60), ("3060", 45),
        ("2080", 50), ("2070", 40), ("2060", 35), ("9070", 50),
    ];

    /// <summary>Pearl hashrate in H/s, or null when the card can't usefully mine it.</summary>
    public static double? PearlHashrate(GpuInfo gpu)
    {
        var name = gpu.Name.ToLowerInvariant();
        foreach (var (key, ths) in PearlSpeeds)
        {
            if (name.Contains(key, StringComparison.Ordinal))
                return ths * 1e12;
        }
        return null;
    }

    public static double CpuHashrate(int logicalThreads, int usagePercent)
    {
        var perThread = OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64
            ? AppleSiliconHpsPerThread
            : CpuHpsPerThread;
        return logicalThreads * perThread * Math.Clamp(usagePercent, 1, 100) / 100.0;
    }

    /// <summary>KawPow hashrate (H/s) and typical extra watts for a GPU, with a modest default for unknown cards.</summary>
    public static (double Hps, double Watts) Gpu(GpuInfo gpu)
    {
        var name = gpu.Name.ToLowerInvariant();
        foreach (var (key, mhs, watts) in Gpus)
        {
            if (name.Contains(key, StringComparison.Ordinal))
                return (mhs * 1e6, watts);
        }
        return gpu.MemoryGb >= 8 ? (20e6, 170) : (12e6, 130);
    }
}

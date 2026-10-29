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

    /// <summary>
    /// Quantus MH/s by GPU model: WhatToMine's card pages (18 October 2026, as on otium.lol) and hashrate.no for the
    /// GTX and older AMD cards (29 October 2026). It needs little graphics memory, so it's what cards without Pearl mine.
    /// </summary>
    private static readonly (string Key, double Mhs)[] QuantusSpeeds =
    [
        ("5080", 760), ("5070 ti", 615), ("5070", 430), ("5060 ti", 300), ("5060", 230),
        ("4090", 880), ("4080", 690), ("4070 ti", 540), ("4070", 410), ("4060 ti", 300), ("4060", 210),
        ("3090", 460), ("3080 ti", 450), ("3080", 380), ("3070 ti", 260), ("3070", 255), ("3060 ti", 190), ("3060", 155),
        ("3050", 95), ("2080 ti", 340), ("2080", 230), ("2070", 180), ("2060 super", 170), ("2060", 140),
        ("1660 super", 113), ("1660", 100), ("6700", 130), ("6600", 60), ("580", 49),
    ];

    /// <summary>Pearl hashrate in H/s, or null when the card can't usefully mine it.</summary>
    public static double? PearlHashrate(GpuInfo gpu) => Lookup(PearlSpeeds, gpu, 1e12);

    /// <summary>Quantus hashrate in H/s, or null for a card without a known speed.</summary>
    public static double? QuantusHashrate(GpuInfo gpu) => Lookup(QuantusSpeeds, gpu, 1e6);

    private static double? Lookup((string Key, double Speed)[] table, GpuInfo gpu, double unit)
    {
        var name = gpu.Name.ToLowerInvariant();
        foreach (var (key, speed) in table)
        {
            if (name.Contains(key, StringComparison.Ordinal))
                return speed * unit;
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

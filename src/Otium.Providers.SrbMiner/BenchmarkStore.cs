using Otium.Core.Gpu;
using Otium.Core.Settings;
using Otium.Core.Switching;

namespace Otium.Providers.SrbMiner;

public sealed class BenchmarkFile
{
    /// <summary>Which GPUs these numbers belong to; a hardware change invalidates them.</summary>
    public string GpuSignature { get; set; } = "";

    public string MinerVersion { get; set; } = "";

    public List<BenchmarkResult> Results { get; set; } = [];

    /// <summary>Intensities tried per algorithm (IntensityTuner); missing in files from before tuning.</summary>
    public List<IntensityTrial> Tuning { get; set; } = [];

    /// <summary>
    /// Algorithms whose load doesn't change with the miner's intensity on these cards (a quiet-mode step didn't lower the
    /// power: Pearl on an RTX 3080), so neither tuning nor quiet mode steps it.
    /// </summary>
    public List<string> IntensityIgnored { get; set; } = [];

    /// <summary>
    /// The power limit (percent of the card's default) each algorithm settled at while the fans stayed under the user's limit
    /// (the silent-fans tuner), so the next start begins there instead of climbing again. Missing in older files.
    /// </summary>
    public Dictionary<string, int> PowerCaps { get; set; } = [];
}

/// <summary>Persists per-algorithm benchmark results so profit switching works immediately on the next start.</summary>
public sealed class BenchmarkStore
{
    private readonly JsonFileStore<BenchmarkFile> _store;
    private readonly string _signature;
    private readonly TimeProvider _time;
    private readonly Lock _lock = new();
    private BenchmarkFile _file;

    public BenchmarkStore(string path, IReadOnlyList<GpuInfo> gpus, TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _store = new JsonFileStore<BenchmarkFile>(path);
        _signature = string.Join("|", gpus.Select(g => $"{g.Vendor}:{g.Name}:{g.MemoryGb:0}"));
        _file = _store.Load();
        if (_file.GpuSignature != _signature || _file.MinerVersion != SrbMinerRelease.Version)
            _file = new BenchmarkFile { GpuSignature = _signature, MinerVersion = SrbMinerRelease.Version };
    }

    public IReadOnlyList<BenchmarkResult> Results
    {
        get
        {
            lock (_lock)
                return _file.Results.ToList();
        }
    }

    /// <summary>Tested, and not due for another try (a failed test is retried after a while; see <see cref="BenchmarkResult.IsCurrent"/>).</summary>
    public bool Has(string algorithm)
    {
        var now = _time.GetUtcNow();
        lock (_lock)
            return _file.Results.Any(r => string.Equals(r.Algorithm, algorithm, StringComparison.OrdinalIgnoreCase) && r.IsCurrent(now));
    }

    public void Record(BenchmarkResult result)
    {
        lock (_lock)
        {
            var previous = _file.Results.FirstOrDefault(r => string.Equals(r.Algorithm, result.Algorithm, StringComparison.OrdinalIgnoreCase));
            if (!result.Succeeded)
                result = result with { FailedTimes = previous is { Succeeded: false } ? previous.FailedTimes + 1 : 1 };
            _file.Results.RemoveAll(r => string.Equals(r.Algorithm, result.Algorithm, StringComparison.OrdinalIgnoreCase));
            _file.Results.Add(result);
            _store.Save(_file);
        }
    }

    /// <summary>This algorithm's intensity trials still in date (<see cref="IntensityTuner.Expiry"/>).</summary>
    public IReadOnlyList<IntensityTrial> Tuning(string algorithm)
    {
        var now = _time.GetUtcNow();
        lock (_lock)
            return _file.Tuning.Where(t => string.Equals(t.Algorithm, algorithm, StringComparison.OrdinalIgnoreCase) && now - t.At < IntensityTuner.Expiry).ToList();
    }

    /// <summary>Whether this algorithm's load was found not to change with intensity (see <see cref="BenchmarkFile.IntensityIgnored"/>).</summary>
    public bool IntensityIgnored(string algorithm)
    {
        lock (_lock)
            return _file.IntensityIgnored.Contains(algorithm, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The power limit this algorithm settled at, or null when none is known yet.</summary>
    public int? PowerCap(string algorithm)
    {
        lock (_lock)
            return _file.PowerCaps.TryGetValue(algorithm, out var percent) ? percent : null;
    }

    public void SetPowerCap(string algorithm, int percent)
    {
        lock (_lock)
        {
            if (_file.PowerCaps.TryGetValue(algorithm, out var current) && current == percent)
                return;
            _file.PowerCaps[algorithm] = percent;
            _store.Save(_file);
        }
    }

    public void MarkIntensityIgnored(string algorithm)
    {
        lock (_lock)
        {
            if (_file.IntensityIgnored.Contains(algorithm, StringComparer.OrdinalIgnoreCase))
                return;
            _file.IntensityIgnored.Add(algorithm);
            _store.Save(_file);
        }
    }

    /// <summary>Keeps a trial, replacing an earlier one of the same algorithm and intensity.</summary>
    public void RecordTrial(IntensityTrial trial)
    {
        lock (_lock)
        {
            _file.Tuning.RemoveAll(t => string.Equals(t.Algorithm, trial.Algorithm, StringComparison.OrdinalIgnoreCase) && t.Intensity == trial.Intensity);
            _file.Tuning.Add(trial);
            _store.Save(_file);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _file = new BenchmarkFile { GpuSignature = _signature, MinerVersion = SrbMinerRelease.Version };
            _store.Save(_file);
        }
    }
}

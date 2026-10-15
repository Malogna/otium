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
}

/// <summary>Persists per-algorithm benchmark results so profit switching works immediately on the next start.</summary>
public sealed class BenchmarkStore
{
    private readonly JsonFileStore<BenchmarkFile> _store;
    private readonly string _signature;
    private readonly Lock _lock = new();
    private BenchmarkFile _file;

    public BenchmarkStore(string path, IReadOnlyList<GpuInfo> gpus)
    {
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

    public bool Has(string algorithm)
    {
        lock (_lock)
            return _file.Results.Any(r => string.Equals(r.Algorithm, algorithm, StringComparison.OrdinalIgnoreCase));
    }

    public void Record(BenchmarkResult result)
    {
        lock (_lock)
        {
            _file.Results.RemoveAll(r => string.Equals(r.Algorithm, result.Algorithm, StringComparison.OrdinalIgnoreCase));
            _file.Results.Add(result);
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

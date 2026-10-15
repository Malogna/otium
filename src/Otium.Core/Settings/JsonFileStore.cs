using System.Text.Json;

namespace Otium.Core.Settings;

/// <summary>Loads and saves a JSON document atomically (write to temp file, then replace).</summary>
public sealed class JsonFileStore<T>(string path) where T : new()
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly Lock _lock = new();

    public string Path { get; } = path;

    public T Load()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(Path))
                    return new T();
                using var stream = File.OpenRead(Path);
                return JsonSerializer.Deserialize<T>(stream, Options) ?? new T();
            }
            catch (JsonException)
            {
                // A corrupt file shouldn't brick the app; keep a copy for diagnosis and start fresh.
                File.Copy(Path, Path + ".corrupt", overwrite: true);
                return new T();
            }
        }
    }

    public void Save(T value)
    {
        lock (_lock)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            var temp = Path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
            File.Move(temp, Path, overwrite: true);
        }
    }
}

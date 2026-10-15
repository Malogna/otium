using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Otium.Core.Providers;

/// <summary>
/// Downloads a third-party engine archive, verifies it against a SHA-256 pinned in code, unpacks it into its own
/// folder and checks antivirus didn't remove the binary. Shared by every provider that runs an external miner.
/// </summary>
public sealed class EngineInstaller(HttpClient http, string engineDir, string binaryName)
{
    public string EngineDirectory { get; } = engineDir;

    private string MarkerPath => Path.Combine(EngineDirectory, ".verified");

    public bool IsInstalled => File.Exists(MarkerPath) && FindBinary() is not null;

    public string? FindBinary() =>
        Directory.Exists(EngineDirectory)
            ? Directory.EnumerateFiles(EngineDirectory, binaryName, SearchOption.AllDirectories).FirstOrDefault()
            : null;

    public async Task InstallAsync(string displayName, Uri url, string fileName, string sha256, IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        if (IsInstalled)
            return;

        Directory.CreateDirectory(EngineDirectory);
        var archivePath = Path.Combine(EngineDirectory, fileName);

        progress?.Report(new InstallProgress($"Downloading {displayName}", 0));
        await DownloadAsync(url, archivePath, displayName, progress, ct);

        progress?.Report(new InstallProgress("Verifying download", 0.9));
        string hash;
        await using (var archive = File.OpenRead(archivePath))
            hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(archive, ct));
        if (!string.Equals(hash, sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(archivePath);
            throw new InvalidDataException($"The downloaded {displayName} didn't match its published checksum, so it was deleted. Please try again.");
        }

        progress?.Report(new InstallProgress("Unpacking", 0.95));
        if (fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            ZipFile.ExtractToDirectory(archivePath, EngineDirectory, overwriteFiles: true);
        }
        else
        {
            await using var file = File.OpenRead(archivePath);
            await using var gzip = new GZipStream(file, CompressionMode.Decompress);
            await TarFile.ExtractToDirectoryAsync(gzip, EngineDirectory, overwriteFiles: true, ct);
        }
        File.Delete(archivePath);

        // Antivirus usually quarantines miners within a second or two of them landing on disk.
        await Task.Delay(TimeSpan.FromSeconds(2), ct);
        var binary = FindBinary();
        if (binary is null || !CanRead(binary))
            throw new EngineBlockedException($"{displayName} was removed or blocked by antivirus software.", EngineDirectory);

        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(binary, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        await File.WriteAllTextAsync(MarkerPath, sha256, ct);
        progress?.Report(new InstallProgress("Ready", 1));
    }

    private async Task DownloadAsync(Uri url, string destination, string displayName, IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;

        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var target = File.Create(destination);
        var buffer = new byte[81920];
        long received = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), ct);
            received += read;
            if (total is > 0)
                progress?.Report(new InstallProgress($"Downloading {displayName}", 0.9 * received / total.Value));
        }
    }

    private static bool CanRead(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return stream.Length > 0;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

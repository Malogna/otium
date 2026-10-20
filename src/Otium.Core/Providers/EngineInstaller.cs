using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Otium.Core.Providers;

/// <summary>
/// Downloads a third-party engine archive, verifies it against a SHA-256 pinned in code, unpacks it into its own
/// folder and checks antivirus didn't remove the binary. Shared by every provider that runs an external miner.
/// </summary>
/// <param name="keepArchive">
/// Keep the verified download next to the engine, so <see cref="VerifiedBinaryAsync"/> can check the binary against it
/// before each launch (the miners: a binary swapped for one that ignores Otium's pool settings would skip the fee).
/// </param>
public sealed class EngineInstaller(HttpClient http, string engineDir, string binaryName, bool keepArchive = false)
{
    private readonly Dictionary<string, (long Length, DateTime WrittenUtc, string Hash)> _fileHashes = [];
    private readonly Dictionary<string, (long Length, DateTime WrittenUtc, string Entry, string Hash)> _archiveBinaries = [];

    public string EngineDirectory { get; } = engineDir;

    private string MarkerPath => Path.Combine(EngineDirectory, ".verified");

    /// <summary>How many files were hashed so far (tests check unchanged files aren't hashed again).</summary>
    internal int FilesHashed { get; private set; }

    public bool IsInstalled => File.Exists(MarkerPath) && FindBinary() is not null && (!keepArchive || KeptArchiveExists());

    /// <summary>The marker of an engine whose download is kept names it on its second line (older installs didn't keep it).</summary>
    private bool KeptArchiveExists()
    {
        try
        {
            var lines = File.ReadAllLines(MarkerPath);
            return lines.Length >= 2 && lines[1].Length > 0 && File.Exists(Path.Combine(EngineDirectory, Path.GetFileName(lines[1])));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// The engine's binary, checked before a launch: the kept download still matches its SHA-256 pinned in code, and the
    /// binary on disk matches the one inside it. Hashes are remembered per file size and modification time, so launching
    /// again (every slice switch) doesn't hash again. Null when the binary is gone (antivirus removed it).
    /// </summary>
    /// <exception cref="ProviderProblemException">
    /// The files were changed: the engine is marked as not installed, so the next try downloads a fresh copy.
    /// </exception>
    public async Task<string?> VerifiedBinaryAsync(string displayName, string fileName, string sha256, CancellationToken ct)
    {
        var archive = Path.Combine(EngineDirectory, fileName);
        if (!File.Exists(archive) || !string.Equals(await HashAsync(archive, ct), sha256, StringComparison.OrdinalIgnoreCase))
            throw Tampered(displayName);
        if (await BinaryInArchiveAsync(archive, ct) is not { } expected)
            throw Tampered(displayName);
        var binary = Path.GetFullPath(Path.Combine(EngineDirectory, expected.Entry));
        if (!File.Exists(binary))
            return null;
        if (!string.Equals(await HashAsync(binary, ct), expected.Hash, StringComparison.OrdinalIgnoreCase))
            throw Tampered(displayName);
        return binary;
    }

    private ProviderProblemException Tampered(string displayName)
    {
        try
        {
            File.Delete(MarkerPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
        return new ProviderProblemException($"{displayName}'s files on this computer were changed, so Otium is downloading a fresh copy.");
    }

    /// <summary>A file's SHA-256 (lowercase hex), hashed again only when its size or modification time changed.</summary>
    private async Task<string> HashAsync(string path, CancellationToken ct)
    {
        var info = new FileInfo(path);
        lock (_fileHashes)
        {
            if (_fileHashes.TryGetValue(path, out var known) && known.Length == info.Length && known.WrittenUtc == info.LastWriteTimeUtc)
                return known.Hash;
        }
        string hash;
        await using (var stream = File.OpenRead(path))
            hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct));
        lock (_fileHashes)
        {
            FilesHashed++;
            _fileHashes[path] = (info.Length, info.LastWriteTimeUtc, hash);
        }
        return hash;
    }

    /// <summary>Where the binary sits in the (verified) archive, relative to the engine folder, and its SHA-256.</summary>
    private async Task<(string Entry, string Hash)?> BinaryInArchiveAsync(string archive, CancellationToken ct)
    {
        var info = new FileInfo(archive);
        lock (_archiveBinaries)
        {
            if (_archiveBinaries.TryGetValue(archive, out var known) && known.Length == info.Length && known.WrittenUtc == info.LastWriteTimeUtc)
                return (known.Entry, known.Hash);
        }

        (string Entry, string Hash)? found = null;
        if (archive.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var zip = ZipFile.OpenRead(archive);
            if (zip.Entries.FirstOrDefault(e => e.Name == binaryName) is { } entry)
            {
                await using var stream = entry.Open();
                found = (entry.FullName, Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct)));
            }
        }
        else
        {
            await using var file = File.OpenRead(archive);
            await using var gzip = new GZipStream(file, CompressionMode.Decompress);
            using var tar = new TarReader(gzip);
            while (await tar.GetNextEntryAsync(cancellationToken: ct) is { } entry)
            {
                if (entry.EntryType is TarEntryType.RegularFile or TarEntryType.V7RegularFile
                    && Path.GetFileName(entry.Name) == binaryName && entry.DataStream is { } data)
                {
                    found = (entry.Name, Convert.ToHexStringLower(await SHA256.HashDataAsync(data, ct)));
                    break;
                }
            }
        }

        if (found is { } result)
        {
            lock (_archiveBinaries)
                _archiveBinaries[archive] = (info.Length, info.LastWriteTimeUtc, result.Entry, result.Hash);
        }
        return found;
    }

    /// <summary>How long an install waits for antivirus to act on the new files before checking they're still there.</summary>
    internal TimeSpan InstallSettle { get; init; } = TimeSpan.FromSeconds(2);

    public string? FindBinary() =>
        Directory.Exists(EngineDirectory)
            ? Directory.EnumerateFiles(EngineDirectory, binaryName, SearchOption.AllDirectories).FirstOrDefault()
            : null;

    public async Task InstallAsync(string displayName, Uri url, string fileName, string sha256, IProgress<InstallProgress>? progress, CancellationToken ct)
    {
        if (IsInstalled)
            return;

        Directory.CreateDirectory(EngineDirectory);
        File.Delete(MarkerPath); // a reinstall: not installed until it's verified again
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
        if (!keepArchive)
            File.Delete(archivePath);

        // Antivirus usually quarantines miners within a second or two of them landing on disk.
        await Task.Delay(InstallSettle, ct);
        var binary = FindBinary();
        if (binary is null || !CanRead(binary) || keepArchive && !CanRead(archivePath))
            throw new EngineBlockedException($"{displayName} was removed or blocked by antivirus software.", EngineDirectory);

        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(binary, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        await File.WriteAllTextAsync(MarkerPath, keepArchive ? $"{sha256}\n{fileName}" : sha256, ct);
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

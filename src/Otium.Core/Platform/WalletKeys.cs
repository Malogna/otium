namespace Otium.Core.Platform;

/// <summary>
/// The sharing networks' keys that can hold earnings not yet paid out: Proxybase's local wallet (credits waiting to be
/// converted to the user's coin), Mysterium's node identity (MYST not yet settled) and the AIOZ node's key (AIOZ under
/// its withdrawal threshold, and Otium's share). Uninstalling used to delete them with everything else, so whatever was
/// waiting was lost. Now they're set aside first, in a folder next to Otium's data folder that the uninstaller leaves,
/// with a note saying what it is; reinstalling Otium puts them back before the networks start, so the waiting earnings
/// are paid out as usual. Only these small files are kept: not AIOZ's stored files, logs or anything else.
/// </summary>
public static class WalletKeys
{
    /// <summary>What's kept, relative to the engines folder: folders (with everything in them) and single files.</summary>
    public static readonly IReadOnlyList<string> KeptPaths =
    [
        Path.Combine("proxybase-data", "wallet"),
        Path.Combine("proxybase-data", "seller_config.json"),
        Path.Combine("proxybase-data", "otium-fee.json"),
        Path.Combine("proxybase-data", "otium-payouts.json"),
        Path.Combine("myst-data", "keystore"),
        Path.Combine("myst-data", "config-mainnet.toml"),
        Path.Combine("myst-data", "otium-sponsored.txt"),
        Path.Combine("aioz-data", "privkey.json"),
        Path.Combine("aioz-data", "otium-fee.json"),
    ];

    public const string NoteFile = "README.txt";

    /// <summary>Where the keys wait between uninstalling and reinstalling: next to the data folder, which is deleted.</summary>
    public static string KeepDir(string dataRoot) =>
        Path.Combine(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(dataRoot)) ?? dataRoot, "Otium wallet keys");

    /// <summary>
    /// Copies the keys out of <paramref name="enginesDir"/> into <paramref name="keepDir"/> (with a note), before the
    /// engines folder is deleted. Returns how many files were kept; with none, no folder is left behind.
    /// </summary>
    public static int Keep(string enginesDir, string keepDir)
    {
        var kept = 0;
        foreach (var relative in KeptPaths)
        {
            var source = Path.Combine(enginesDir, relative);
            var files = Directory.Exists(source)
                ? Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
                : File.Exists(source) ? [source] : [];
            foreach (var file in files)
            {
                var target = Path.Combine(keepDir, Path.GetRelativePath(enginesDir, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: true);
                kept++;
            }
        }
        if (kept > 0)
            File.WriteAllText(Path.Combine(keepDir, NoteFile), Note);
        return kept;
    }

    /// <summary>
    /// Puts kept keys back into <paramref name="enginesDir"/> (never over a key that's already there), then removes the
    /// folder. Returns how many files were restored.
    /// </summary>
    public static int Restore(string keepDir, string enginesDir)
    {
        if (!Directory.Exists(keepDir))
            return 0;
        var restored = 0;
        foreach (var file in Directory.EnumerateFiles(keepDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(keepDir, file);
            if (relative == NoteFile || !KeptPaths.Any(p => relative == p || relative.StartsWith(p + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
                continue;
            var target = Path.Combine(enginesDir, relative);
            if (File.Exists(target))
                continue;
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
            restored++;
        }
        Directory.Delete(keepDir, recursive: true);
        return restored;
    }

    /// <summary>
    /// Everything in Otium's data folder that can go, except what's under <paramref name="keep"/>; a file still held open
    /// (rare) stays behind.
    /// </summary>
    public static void DeleteDataFolder(string root, string? keep = null)
    {
        if (!Directory.Exists(root))
            return;
        bool Kept(string path) => keep is not null
            && (path == keep || path.StartsWith(Path.TrimEndingDirectorySeparator(keep) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Where(f => !Kept(f)))
        {
            try
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
        foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length).Append(root))
        {
            try
            {
                Directory.Delete(dir);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private const string Note =
        "Otium kept these keys when it was uninstalled, because they can still hold earnings that weren't paid out yet:\r\n" +
        "Proxybase's wallet (credits waiting to be converted to your coin), Mysterium's node identity (MYST not settled yet)\r\n" +
        "and the AIOZ node's key (AIOZ under its 5 AIOZ withdrawal threshold).\r\n\r\n" +
        "Reinstall Otium (otium.lol/download) and it puts them back by itself, so whatever was waiting is paid out as usual.\r\n" +
        "If you don't plan to use Otium again, you can delete this folder. Don't share these files: they control those balances.\r\n";
}

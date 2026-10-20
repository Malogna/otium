using System.Diagnostics;
using System.Security.Cryptography;
using Otium.Core;
using Otium.Core.Updates;

// Signs a release's update feeds after the release workflow has published it. The key stays on this computer.
//
//   keygen <folder>                  make the signing key (DPAPI-protected) and a backup key (a PEM file to move offline)
//   sign <tag> --key <file>          sign the release's releases.*.json and upload the .sig files next to them
//   verify <tag>                     check that every feed of the release has a signature Otium accepts
//   check <updates-url>              check that an update server (e.g. https://otium.lol/updates/pre/) serves feeds that verify
//
// <file> is the DPAPI-protected key from keygen, or the backup key's .pem.

const string Repository = "Malogna/otium";

return args switch
{
    ["keygen", var folder] => KeyGen(folder),
    ["sign", var tag, "--key", var key] => Sign(tag, key),
    ["verify", var tag] => Verify(tag),
    ["check", var url] => await Check(url),
    _ => Usage(),
};

static async Task<int> Check(string url)
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    var updates = new SignedUpdates(http, AppIdentity.UpdateSigningKeys);
    var origin = new ServerUpdateOrigin(http, url);
    var failed = 0;
    foreach (var channel in new[] { "win-x64", "osx-arm64", "osx-x64", "linux-x64" })
    {
        try
        {
            var feed = await updates.GetFeedAsync(origin, $"releases.{channel}.json", CancellationToken.None);
            Console.WriteLine($"ok      releases.{channel}.json: {string.Join(", ", feed.Packages.Keys)}");
        }
        catch (Exception e) when (e is UpdateVerificationException or HttpRequestException)
        {
            Console.WriteLine($"FAILED  releases.{channel}.json: {e.Message}");
            failed++;
        }
    }
    return failed == 0 ? 0 : 1;
}

static int Usage()
{
    Console.Error.WriteLine("usage: keygen <folder> | sign <tag> --key <file> | verify <tag> | check <updates-url>");
    return 2;
}

static int KeyGen(string folder)
{
    if (!OperatingSystem.IsWindows())
        throw new PlatformNotSupportedException("The signing key is protected with Windows DPAPI.");
    Directory.CreateDirectory(folder);
    var primaryFile = Path.Combine(folder, "update-signing.key");
    var backupFile = Path.Combine(folder, "update-signing-backup.pem");
    if (File.Exists(primaryFile) || File.Exists(backupFile))
        throw new IOException($"Keys already exist in {folder}; refusing to replace them (installed copies trust the old ones).");

    using var primary = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    using var backup = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    File.WriteAllBytes(primaryFile, ProtectedData.Protect(primary.ExportPkcs8PrivateKey(), null, DataProtectionScope.CurrentUser));
    File.WriteAllText(backupFile, backup.ExportPkcs8PrivateKeyPem());

    Console.WriteLine("Public keys for AppIdentity.UpdateSigningKeys (primary first):");
    foreach (var key in new[] { primary, backup })
    {
        var spki = key.ExportSubjectPublicKeyInfo();
        Console.WriteLine($"  \"{Convert.ToBase64String(spki)}\", // {UpdateSignature.KeyId(spki)}");
    }
    Console.WriteLine($"Signing key: {primaryFile} (opens only for this Windows account).");
    Console.WriteLine($"Backup key:  {backupFile} (move it somewhere offline, e.g. a USB stick or password manager, then delete it here).");
    return 0;
}

static int Sign(string tag, string keyFile)
{
    using var key = LoadKey(keyFile);
    var spki = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());
    if (!AppIdentity.UpdateSigningKeys.Contains(spki))
        throw new InvalidOperationException("This key isn't one Otium trusts (AppIdentity.UpdateSigningKeys); installed copies would ignore its signatures.");

    var folder = Directory.CreateTempSubdirectory("otium-sign-");
    try
    {
        Gh("release", "download", tag, "--repo", Repository, "--pattern", "releases.*.json", "--dir", folder.FullName);
        var feeds = folder.GetFiles("releases.*.json");
        if (feeds.Length == 0)
            throw new InvalidOperationException($"{tag} has no releases.*.json feeds.");

        var signatures = new List<string>();
        foreach (var feed in feeds)
        {
            var bytes = File.ReadAllBytes(feed.FullName);
            var signature = UpdateSignature.Sign(key, feed.Name, bytes);
            if (!UpdateSignature.Verify(feed.Name, bytes, signature, AppIdentity.UpdateSigningKeys))
                throw new InvalidOperationException($"The signature for {feed.Name} doesn't verify.");
            var file = Path.Combine(folder.FullName, UpdateSignature.FileFor(feed.Name));
            File.WriteAllText(file, signature);
            signatures.Add(file);
            Console.WriteLine($"signed {feed.Name} ({bytes.Length} bytes)");
        }

        Gh(["release", "upload", tag, "--repo", Repository, "--clobber", .. signatures]);
        Console.WriteLine($"Uploaded {signatures.Count} signatures to {tag}.");
        return Verify(tag);
    }
    finally
    {
        folder.Delete(recursive: true);
    }
}

static int Verify(string tag)
{
    var folder = Directory.CreateTempSubdirectory("otium-verify-");
    try
    {
        Gh("release", "download", tag, "--repo", Repository, "--pattern", "releases.*.json*", "--dir", folder.FullName);
        var failed = 0;
        foreach (var feed in folder.GetFiles("releases.*.json"))
        {
            var signatureFile = Path.Combine(folder.FullName, UpdateSignature.FileFor(feed.Name));
            var ok = File.Exists(signatureFile)
                     && UpdateSignature.Verify(feed.Name, File.ReadAllBytes(feed.FullName), File.ReadAllText(signatureFile), AppIdentity.UpdateSigningKeys);
            Console.WriteLine($"{(ok ? "ok     " : "MISSING OR INVALID")} {feed.Name}");
            failed += ok ? 0 : 1;
        }
        return failed == 0 ? 0 : 1;
    }
    finally
    {
        folder.Delete(recursive: true);
    }
}

static ECDsa LoadKey(string file)
{
    var key = ECDsa.Create();
    if (file.EndsWith(".pem", StringComparison.OrdinalIgnoreCase))
        key.ImportFromPem(File.ReadAllText(file));
    else if (OperatingSystem.IsWindows())
        key.ImportPkcs8PrivateKey(ProtectedData.Unprotect(File.ReadAllBytes(file), null, DataProtectionScope.CurrentUser), out _);
    else
        throw new PlatformNotSupportedException("The DPAPI-protected key opens on Windows only; use the backup .pem elsewhere.");
    return key;
}

static void Gh(params string[] arguments)
{
    var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("GH_PATH") ?? "gh") { UseShellExecute = false };
    foreach (var argument in arguments)
        start.ArgumentList.Add(argument);
    using var process = Process.Start(start) ?? throw new InvalidOperationException("Couldn't start gh (GitHub CLI).");
    process.WaitForExit();
    if (process.ExitCode != 0)
        throw new InvalidOperationException($"gh {arguments[0]} {arguments[1]} failed (exit {process.ExitCode}).");
}

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Otium.Core.Updates;

/// <summary>
/// Signatures on the update feeds ("releases.win-x64.json"). The feed lists every package of a release with its
/// SHA-256, so a valid signature on it vouches for the packages too. The signing key never goes to GitHub or Otium's
/// server: releases are signed on the developer's own computer after GitHub has built them (tools/Otium.UpdateSigner),
/// so someone who takes over otium.lol or the GitHub account still can't get an installed Otium to run their code.
/// </summary>
/// <remarks>
/// A signature file is three lines: <c>otium-update-signature 1</c>, <c>key &lt;id&gt;</c> and the base64 ECDSA P-256
/// signature (IEEE P1363) over "otium-update-feed 1\n&lt;feed file name&gt;\n" followed by the feed's bytes. The file
/// name is signed too, so one platform's feed can't be passed off as another's.
/// </remarks>
public static class UpdateSignature
{
    public const string Header = "otium-update-signature 1";

    public static string FileFor(string feedFile) => feedFile + ".sig";

    public static string KeyId(byte[] subjectPublicKeyInfo) => Convert.ToHexStringLower(SHA256.HashData(subjectPublicKeyInfo)[..8]);

    public static string Sign(ECDsa key, string feedFile, byte[] feed) =>
        $"{Header}\nkey {KeyId(key.ExportSubjectPublicKeyInfo())}\n{Convert.ToBase64String(key.SignData(Message(feedFile, feed), HashAlgorithmName.SHA256))}\n";

    /// <summary>True when <paramref name="signature"/> is a valid signature on this feed by one of <paramref name="trustedKeys"/> (base64 SubjectPublicKeyInfo).</summary>
    public static bool Verify(string feedFile, byte[] feed, string? signature, IEnumerable<string> trustedKeys)
    {
        var lines = (signature ?? "").Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length != 3 || lines[0] != Header || !lines[1].StartsWith("key ", StringComparison.Ordinal))
            return false;
        byte[] signed;
        try
        {
            signed = Convert.FromBase64String(lines[2].Trim());
        }
        catch (FormatException)
        {
            return false;
        }
        foreach (var trusted in trustedKeys)
        {
            var spki = Convert.FromBase64String(trusted);
            if (KeyId(spki) != lines[1][4..].Trim())
                continue;
            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(spki, out _);
            if (key.VerifyData(Message(feedFile, feed), signed, HashAlgorithmName.SHA256))
                return true;
        }
        return false;
    }

    private static byte[] Message(string feedFile, byte[] feed) => [.. Encoding.UTF8.GetBytes($"otium-update-feed 1\n{feedFile}\n"), .. feed];
}

/// <summary>An update was refused because its feed, signature or package didn't check out.</summary>
public sealed class UpdateVerificationException(string message) : Exception(message);

/// <summary>Where a release's feed, its signature and its packages come from.</summary>
public interface IUpdateOrigin
{
    string Name { get; }

    /// <summary>The feed and its signature from one release, or null when this origin has no feed by that name.</summary>
    Task<(byte[] Feed, string? Signature)?> GetFeedAsync(string feedFile, CancellationToken ct);

    /// <summary>Where to download a package listed in the feed <see cref="GetFeedAsync"/> last returned.</summary>
    Uri PackageUri(string fileName);
}

/// <summary>Otium's server: /updates/pre/ or /updates/stable/ serve the feed, its signature and the packages (as redirects).</summary>
public sealed class ServerUpdateOrigin(HttpClient http, string baseUrl) : IUpdateOrigin
{
    private readonly Uri _base = new(baseUrl.TrimEnd('/') + "/");

    public string Name => "Otium's update server";

    public async Task<(byte[] Feed, string? Signature)?> GetFeedAsync(string feedFile, CancellationToken ct)
    {
        using var feed = await http.GetAsync(new Uri(_base, feedFile), ct);
        if (feed.StatusCode == HttpStatusCode.NotFound)
            return null;
        feed.EnsureSuccessStatusCode();
        using var signature = await http.GetAsync(new Uri(_base, UpdateSignature.FileFor(feedFile)), ct);
        return (await feed.Content.ReadAsByteArrayAsync(ct),
            signature.IsSuccessStatusCode ? await signature.Content.ReadAsStringAsync(ct) : null);
    }

    public Uri PackageUri(string fileName) => new(_base, Uri.EscapeDataString(fileName));
}

/// <summary>
/// GitHub's releases, read directly (the fallback when Otium's server can't be reached): the newest release on the
/// track that has both the feed and its signature, and that release's packages.
/// </summary>
public sealed class GitHubUpdateOrigin(HttpClient http, string repository, bool prereleases) : IUpdateOrigin
{
    private Dictionary<string, string> _assets = [];

    public string Name => "GitHub";

    public async Task<(byte[] Feed, string? Signature)?> GetFeedAsync(string feedFile, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{repository}/releases?per_page=30");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var releases = doc.RootElement.EnumerateArray()
            .Where(r => !(r.TryGetProperty("draft", out var d) && d.GetBoolean()))
            .Where(r => prereleases || !(r.TryGetProperty("prerelease", out var p) && p.GetBoolean()))
            .Select(r => (Tag: r.GetProperty("tag_name").GetString() ?? "", Assets: r.GetProperty("assets").EnumerateArray()
                .ToDictionary(a => a.GetProperty("name").GetString() ?? "", a => a.GetProperty("browser_download_url").GetString() ?? "")))
            .Where(r => r.Assets.ContainsKey(feedFile) && r.Assets.ContainsKey(UpdateSignature.FileFor(feedFile)))
            .OrderByDescending(r => r.Tag, ReleaseVersion.Comparer)
            .ToList();
        if (releases.Count == 0)
            return null;
        _assets = releases[0].Assets;
        return (await http.GetByteArrayAsync(_assets[feedFile], ct), await http.GetStringAsync(_assets[UpdateSignature.FileFor(feedFile)], ct));
    }

    public Uri PackageUri(string fileName) => _assets.TryGetValue(fileName, out var url)
        ? new Uri(url)
        : throw new UpdateVerificationException($"{fileName} isn't part of the release that was checked.");
}

/// <summary>A feed whose signature checked out, and the packages it vouches for (file name → SHA-256).</summary>
public sealed record VerifiedFeed(IUpdateOrigin Origin, string Json, IReadOnlyDictionary<string, string> Packages);

/// <summary>Reads only signed feeds, and downloads only the packages they list, checked against their SHA-256.</summary>
public sealed class SignedUpdates(HttpClient http, IReadOnlyList<string> trustedKeys)
{
    public async Task<VerifiedFeed> GetFeedAsync(IUpdateOrigin origin, string feedFile, CancellationToken ct)
    {
        var fetched = await origin.GetFeedAsync(feedFile, ct)
                      ?? throw new UpdateVerificationException($"{origin.Name} has no signed update feed ({feedFile}).");
        if (!UpdateSignature.Verify(feedFile, fetched.Feed, fetched.Signature, trustedKeys))
            throw new UpdateVerificationException($"The update feed from {origin.Name} isn't signed by Otium's developer, so it was ignored.");

        var json = Encoding.UTF8.GetString(fetched.Feed).TrimStart('﻿');
        var packages = new Dictionary<string, string>(StringComparer.Ordinal);
        using (var doc = JsonDocument.Parse(json))
        {
            foreach (var asset in doc.RootElement.GetProperty("Assets").EnumerateArray())
            {
                if (asset.TryGetProperty("FileName", out var name) && asset.TryGetProperty("SHA256", out var hash)
                    && name.GetString() is { Length: > 0 } file && hash.GetString() is { Length: 64 } sha256)
                    packages[file] = sha256.ToUpperInvariant();
            }
        }
        return new VerifiedFeed(origin, json, packages);
    }

    /// <summary>
    /// Downloads a package the verified feed lists, and keeps it only if its SHA-256 matches the feed's. Velopack checks
    /// packages too, but this doesn't depend on it.
    /// </summary>
    public async Task DownloadAsync(VerifiedFeed feed, string fileName, string localFile, Action<int>? progress, CancellationToken ct)
    {
        if (!feed.Packages.TryGetValue(fileName, out var expected))
            throw new UpdateVerificationException($"{fileName} isn't in the signed update feed.");

        using (var response = await http.GetAsync(feed.Origin.PackageUri(fileName), HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength;
            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var target = File.Create(localFile);
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), ct);
                done += read;
                if (total is > 0)
                    progress?.Invoke((int)(done * 100 / total.Value));
            }
        }

        string actual;
        await using (var file = File.OpenRead(localFile))
            actual = Convert.ToHexString(await SHA256.HashDataAsync(file, ct));
        if (actual != expected)
        {
            File.Delete(localFile);
            throw new UpdateVerificationException($"{fileName} didn't match the signed update feed, so it was deleted.");
        }
        progress?.Invoke(100);
    }
}

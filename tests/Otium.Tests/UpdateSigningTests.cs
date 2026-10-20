using System.Net;
using System.Security.Cryptography;
using System.Text;
using Otium.Core;
using Otium.Core.Updates;

namespace Otium.Tests;

/// <summary>
/// Signed updates: an installed Otium only takes a feed signed with a key built into it, and only the packages that
/// feed lists, so someone who takes over otium.lol or the GitHub account can't push code to it.
/// </summary>
public sealed class UpdateSigningTests : IDisposable
{
    private const string FeedFile = "releases.win-x64.json";
    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly ECDsa _stranger = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly string _dir = Directory.CreateTempSubdirectory("otium-updates-").FullName;

    private string[] Trusted => [Convert.ToBase64String(_key.ExportSubjectPublicKeyInfo())];

    private static readonly byte[] Package = Encoding.UTF8.GetBytes("the real package");

    private static byte[] Feed(byte[] package) => Encoding.UTF8.GetBytes(
        $$"""{"Assets":[{"PackageId":"Otium.Desktop","Version":"0.1.0-rc.15","Type":"Full","FileName":"Otium.Desktop-0.1.0-rc.15-win-x64-full.nupkg","SHA256":"{{Convert.ToHexString(SHA256.HashData(package))}}","Size":{{package.Length}}}]}""");

    [Fact]
    public void A_signature_covers_the_feed_its_file_name_and_the_key()
    {
        var feed = Feed(Package);
        var signature = UpdateSignature.Sign(_key, FeedFile, feed);
        Assert.StartsWith(UpdateSignature.Header + "\nkey ", signature);
        Assert.True(UpdateSignature.Verify(FeedFile, feed, signature, Trusted));
        Assert.True(UpdateSignature.Verify(FeedFile, feed, signature.Replace("\n", "\r\n"), Trusted)); // line endings don't matter

        var tampered = (byte[])feed.Clone();
        tampered[^3] ^= 1;
        Assert.False(UpdateSignature.Verify(FeedFile, tampered, signature, Trusted));
        Assert.False(UpdateSignature.Verify("releases.osx-arm64.json", feed, signature, Trusted)); // another platform's feed
        Assert.False(UpdateSignature.Verify(FeedFile, feed, UpdateSignature.Sign(_stranger, FeedFile, feed), Trusted));
        Assert.False(UpdateSignature.Verify(FeedFile, feed, null, Trusted));
        Assert.False(UpdateSignature.Verify(FeedFile, feed, "otium-update-signature 1\nkey 00\nnot base64!", Trusted));
    }

    [Fact]
    public void The_keys_built_into_otium_are_two_distinct_p256_public_keys()
    {
        Assert.Equal(2, AppIdentity.UpdateSigningKeys.Count);
        Assert.Equal(2, AppIdentity.UpdateSigningKeys.Distinct().Count());
        foreach (var key in AppIdentity.UpdateSigningKeys)
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(key), out _);
            Assert.Equal(256, ecdsa.KeySize);
        }
    }

    [Fact]
    public async Task A_signed_feed_is_used_and_its_packages_download_only_if_they_match()
    {
        var feed = Feed(Package);
        var origin = new FakeOrigin(feed, UpdateSignature.Sign(_key, FeedFile, feed));
        var http = new HttpClient(new Files { ["https://dl.example/Otium.Desktop-0.1.0-rc.15-win-x64-full.nupkg"] = Package });
        var updates = new SignedUpdates(http, Trusted);

        var verified = await updates.GetFeedAsync(origin, FeedFile, CancellationToken.None);
        Assert.Contains("0.1.0-rc.15", verified.Json);
        var target = Path.Combine(_dir, "ok.nupkg");
        var progress = new List<int>();
        await updates.DownloadAsync(verified, "Otium.Desktop-0.1.0-rc.15-win-x64-full.nupkg", target, progress.Add, CancellationToken.None);
        Assert.Equal(Package, await File.ReadAllBytesAsync(target));
        Assert.Equal(100, progress[^1]);

        // A package the feed doesn't list is never downloaded.
        await Assert.ThrowsAsync<UpdateVerificationException>(() =>
            updates.DownloadAsync(verified, "evil.nupkg", Path.Combine(_dir, "evil.nupkg"), null, CancellationToken.None));
        Assert.False(File.Exists(Path.Combine(_dir, "evil.nupkg")));
    }

    [Fact]
    public async Task A_swapped_package_is_deleted_and_refused()
    {
        // The feed is genuine, but whoever serves the downloads hands out something else.
        var feed = Feed(Package);
        var origin = new FakeOrigin(feed, UpdateSignature.Sign(_key, FeedFile, feed));
        var http = new HttpClient(new Files { ["https://dl.example/Otium.Desktop-0.1.0-rc.15-win-x64-full.nupkg"] = Encoding.UTF8.GetBytes("malware") });
        var updates = new SignedUpdates(http, Trusted);
        var verified = await updates.GetFeedAsync(origin, FeedFile, CancellationToken.None);

        var target = Path.Combine(_dir, "swapped.nupkg");
        var e = await Assert.ThrowsAsync<UpdateVerificationException>(() =>
            updates.DownloadAsync(verified, "Otium.Desktop-0.1.0-rc.15-win-x64-full.nupkg", target, null, CancellationToken.None));
        Assert.Contains("didn't match", e.Message);
        Assert.False(File.Exists(target));
    }

    [Theory]
    [InlineData("unsigned")]
    [InlineData("stranger")]
    [InlineData("edited")]
    public async Task Feeds_without_a_trusted_signature_are_refused(string how)
    {
        var feed = Feed(Package);
        var signature = how switch
        {
            "unsigned" => null,
            "stranger" => UpdateSignature.Sign(_stranger, FeedFile, feed),
            _ => UpdateSignature.Sign(_key, FeedFile, Feed(Encoding.UTF8.GetBytes("the package before someone edited the feed"))),
        };
        var updates = new SignedUpdates(new HttpClient(new Files()), Trusted);
        var e = await Assert.ThrowsAsync<UpdateVerificationException>(() =>
            updates.GetFeedAsync(new FakeOrigin(feed, signature), FeedFile, CancellationToken.None));
        Assert.Contains("isn't signed by Otium's developer", e.Message);
    }

    [Fact]
    public async Task On_github_the_newest_release_with_a_signed_feed_is_read_and_its_packages_come_from_it()
    {
        var feed = Feed(Package);
        var files = new Files
        {
            ["https://api.github.com/repos/Malogna/otium/releases?per_page=30"] = Encoding.UTF8.GetBytes("""
                [
                  {"tag_name":"v0.1.0-rc.16","draft":false,"prerelease":true,"assets":[{"name":"releases.win-x64.json","browser_download_url":"https://gh.example/16/feed"}]},
                  {"tag_name":"v0.1.0-rc.9","draft":false,"prerelease":true,"assets":[
                    {"name":"releases.win-x64.json","browser_download_url":"https://gh.example/9/feed"},
                    {"name":"releases.win-x64.json.sig","browser_download_url":"https://gh.example/9/sig"}]},
                  {"tag_name":"v0.1.0-rc.15","draft":false,"prerelease":true,"assets":[
                    {"name":"releases.win-x64.json","browser_download_url":"https://gh.example/15/feed"},
                    {"name":"releases.win-x64.json.sig","browser_download_url":"https://gh.example/15/sig"},
                    {"name":"Otium.Desktop-0.1.0-rc.15-win-x64-full.nupkg","browser_download_url":"https://gh.example/15/pkg"}]}
                ]
                """),
            ["https://gh.example/15/feed"] = feed,
            ["https://gh.example/15/sig"] = Encoding.UTF8.GetBytes(UpdateSignature.Sign(_key, FeedFile, feed)),
            ["https://gh.example/15/pkg"] = Package,
        };
        var http = new HttpClient(files);
        var updates = new SignedUpdates(http, Trusted);
        var origin = new GitHubUpdateOrigin(http, "Malogna/otium", prereleases: true);

        // rc.16 has no signature; of the signed ones, rc.15 is the newest version, whatever order GitHub lists them in.
        var verified = await updates.GetFeedAsync(origin, FeedFile, CancellationToken.None);
        Assert.Contains("0.1.0-rc.15", verified.Json);
        await updates.DownloadAsync(verified, "Otium.Desktop-0.1.0-rc.15-win-x64-full.nupkg", Path.Combine(_dir, "gh.nupkg"), null, CancellationToken.None);

        // Stable installs ignore release candidates.
        var stable = new GitHubUpdateOrigin(http, "Malogna/otium", prereleases: false);
        await Assert.ThrowsAsync<UpdateVerificationException>(() => updates.GetFeedAsync(stable, FeedFile, CancellationToken.None));
    }

    private sealed class FakeOrigin(byte[] feed, string? signature) : IUpdateOrigin
    {
        public string Name => "the test origin";

        public Task<(byte[] Feed, string? Signature)?> GetFeedAsync(string feedFile, CancellationToken ct) =>
            Task.FromResult<(byte[], string?)?>((feed, signature));

        public Uri PackageUri(string fileName) => new($"https://dl.example/{fileName}");
    }

    /// <summary>Serves fixed bytes per URL; anything else is a 404.</summary>
    private sealed class Files : HttpMessageHandler, IEnumerable<KeyValuePair<string, byte[]>>
    {
        private readonly Dictionary<string, byte[]> _files = [];

        public byte[] this[string url] { set => _files[url] = value; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(_files.TryGetValue(request.RequestUri!.ToString(), out var bytes)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));

        public IEnumerator<KeyValuePair<string, byte[]>> GetEnumerator() => _files.GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(string url, byte[] bytes) => _files[url] = bytes;
    }

    public void Dispose()
    {
        _key.Dispose();
        _stranger.Dispose();
        Directory.Delete(_dir, recursive: true);
    }
}

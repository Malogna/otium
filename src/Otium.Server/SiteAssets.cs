using System.Security.Cryptography;

namespace Otium.Server;

/// <summary>
/// The website's own files (stylesheet, script, logo, link-preview image, fonts), embedded in the server so the site needs nothing else
/// to deploy and loads nothing from other hosts. Pages link them with a content hash, so browsers and Cloudflare can
/// cache them for a year and still get a new version the moment it changes.
/// </summary>
internal static class SiteAssets
{
    public sealed record Asset(byte[] Content, string ContentType, string Version);

    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".css"] = "text/css; charset=utf-8",
        [".js"] = "text/javascript; charset=utf-8",
        [".svg"] = "image/svg+xml",
        [".png"] = "image/png",
        [".woff2"] = "font/woff2",
        [".txt"] = "text/plain; charset=utf-8",
    };

    private static readonly Lazy<IReadOnlyDictionary<string, Asset>> All = new(Load);

    /// <summary>An asset by its path under Site/ ("site.css", "fonts/HankenGrotesk.woff2"), or null.</summary>
    public static Asset? Find(string path) => All.Value.GetValueOrDefault(path.Replace('\\', '/'));

    /// <summary>"/site/site.css?v=1a2b3c4d": the asset's address, changing whenever its content does.</summary>
    public static string Url(string path) => Find(path) is { } asset ? $"/site/{path}?v={asset.Version}" : $"/site/{path}";

    private static IReadOnlyDictionary<string, Asset> Load()
    {
        var assembly = typeof(SiteAssets).Assembly;
        var assets = new Dictionary<string, Asset>(StringComparer.Ordinal);
        foreach (var name in assembly.GetManifestResourceNames())
        {
            var path = name.Replace('\\', '/');
            if (!path.StartsWith("Site/", StringComparison.Ordinal))
                continue;
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var content = buffer.ToArray();
            var type = Types.GetValueOrDefault(Path.GetExtension(path), "application/octet-stream");
            assets[path["Site/".Length..]] = new Asset(content, type, Version(content));
        }

        // The stylesheet names its fonts by plain address; give those addresses the fonts' content hash too, so the
        // fonts can be cached for a year like everything else. The stylesheet's own hash then covers the change.
        foreach (var (path, asset) in assets.Where(a => a.Key.EndsWith(".css", StringComparison.Ordinal)).ToList())
        {
            var css = System.Text.RegularExpressions.Regex.Replace(System.Text.Encoding.UTF8.GetString(asset.Content),
                """url\("/site/([^"?]+)"\)""",
                m => assets.TryGetValue(m.Groups[1].Value, out var linked) ? $"""url("/site/{m.Groups[1].Value}?v={linked.Version}")""" : m.Value);
            var content = System.Text.Encoding.UTF8.GetBytes(css);
            assets[path] = asset with { Content = content, Version = Version(content) };
        }
        return assets;
    }

    private static string Version(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content))[..10];
}

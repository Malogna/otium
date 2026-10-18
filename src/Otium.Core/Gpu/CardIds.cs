using System.Text.RegularExpressions;

namespace Otium.Core.Gpu;

/// <summary>
/// A short id for a graphics card model, shared by the app's earnings reports and the website's card list:
/// "NVIDIA GeForce RTX 4070 Ti SUPER" → "rtx4070tisuper", "AMD Radeon RX 6700 XT" → "rx6700xt". Laptop chips get
/// their own ids ("rtx4070laptop"), because they run at a fraction of the desktop card's power.
/// </summary>
public static partial class CardIds
{
    public static string? FromName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        var match = Model().Match(name);
        if (!match.Success)
            return null;
        var suffix = match.Groups["suffix"].Value.ToLowerInvariant().Replace(" ", "", StringComparison.Ordinal);
        var id = match.Groups["series"].Value.ToLowerInvariant() + match.Groups["number"].Value + suffix;
        return Laptop().IsMatch(name) ? id + "laptop" : id;
    }

    [GeneratedRegex(@"\b(?<series>RTX|GTX|RX)\s*-?\s*(?<number>\d{3,4})(?:\s*(?<suffix>Ti\s*Super|Ti|Super|XTX|XT|GRE))?\b", RegexOptions.IgnoreCase)]
    private static partial Regex Model();

    [GeneratedRegex(@"\b(Laptop|Mobile|Max-Q)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Laptop();
}

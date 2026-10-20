using System.Globalization;
using System.Reflection;

namespace Otium.Core;

/// <summary>
/// Otium's own version numbers ("0.1.1", "0.1.0-rc.16", with or without a "+commit" suffix), as the app sends them to
/// the server in <see cref="Header"/> so the server can require a minimum version: older versions trusted their
/// settings file for the fee and honoured developer overrides, so once a fixed version is out they're told to update.
/// </summary>
public static class AppVersions
{
    public const string Header = "X-Otium-Version";

    /// <summary>This build's version without the commit suffix ("0.1.1"), or null when it has none.</summary>
    public static string? Of(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] is { Length: > 0 } v ? v : null;

    /// <summary>The numbers and the pre-release label ("rc.16"), or null when it isn't a version.</summary>
    public static (Version Number, string? Prerelease)? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var core = text.Trim().TrimStart('v').Split('+')[0];
        var dash = core.IndexOf('-');
        var numbers = dash < 0 ? core : core[..dash];
        if (!Version.TryParse(numbers.Count(c => c == '.') == 1 ? numbers + ".0" : numbers, out var version))
            return null;
        return (version, dash < 0 ? null : core[(dash + 1)..]);
    }

    /// <summary>
    /// Below zero when <paramref name="a"/> is older: numbers first, then a pre-release ("0.1.1-rc.2") before its
    /// release ("0.1.1"), and pre-releases by their numbers ("rc.9" before "rc.10").
    /// </summary>
    public static int Compare((Version Number, string? Prerelease) a, (Version Number, string? Prerelease) b)
    {
        var numbers = a.Number.CompareTo(b.Number);
        if (numbers != 0)
            return numbers;
        if (a.Prerelease is null || b.Prerelease is null)
            return (a.Prerelease is null ? 1 : 0) - (b.Prerelease is null ? 1 : 0);
        var x = a.Prerelease.Split('.');
        var y = b.Prerelease.Split('.');
        for (var i = 0; i < Math.Min(x.Length, y.Length); i++)
        {
            var c = int.TryParse(x[i], NumberStyles.None, CultureInfo.InvariantCulture, out var m) && int.TryParse(y[i], NumberStyles.None, CultureInfo.InvariantCulture, out var n)
                ? m.CompareTo(n)
                : string.CompareOrdinal(x[i], y[i]);
            if (c != 0)
                return c;
        }
        return x.Length.CompareTo(y.Length);
    }

    /// <summary>Whether <paramref name="version"/> is at least <paramref name="minimum"/> (an unreadable or missing version isn't).</summary>
    public static bool AtLeast(string? version, string minimum) =>
        Parse(version) is { } v && Parse(minimum) is { } min && Compare(v, min) >= 0;
}

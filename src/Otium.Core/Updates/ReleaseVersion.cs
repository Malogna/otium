namespace Otium.Core.Updates;

/// <summary>Orders release tags by semantic version: "v0.1.0-rc.10" after "v0.1.0-rc.9", and "v0.1.0" after both.</summary>
public static class ReleaseVersion
{
    public static readonly IComparer<string> Comparer = Comparer<string>.Create(Compare);

    public static int Compare(string? a, string? b)
    {
        var (coreA, preA) = Split(a);
        var (coreB, preB) = Split(b);
        for (var i = 0; i < Math.Max(coreA.Length, coreB.Length); i++)
        {
            var c = Part(coreA, i).CompareTo(Part(coreB, i));
            if (c != 0)
                return c;
        }
        // A release outranks its pre-releases; pre-releases compare part by part, numbers as numbers.
        if (preA.Length == 0 || preB.Length == 0)
            return preB.Length.CompareTo(preA.Length);
        for (var i = 0; i < Math.Min(preA.Length, preB.Length); i++)
        {
            var bothNumbers = long.TryParse(preA[i], out var x) & long.TryParse(preB[i], out var y);
            var c = bothNumbers ? x.CompareTo(y) : string.CompareOrdinal(preA[i], preB[i]);
            if (c != 0)
                return c;
        }
        return preA.Length.CompareTo(preB.Length);
    }

    private static (long[] Core, string[] Pre) Split(string? tag)
    {
        var text = (tag ?? "").TrimStart('v', 'V').Split('+')[0];
        var dash = text.IndexOf('-');
        var core = (dash < 0 ? text : text[..dash]).Split('.').Select(p => long.TryParse(p, out var n) ? n : 0).ToArray();
        return (core, dash < 0 ? [] : text[(dash + 1)..].Split('.'));
    }

    private static long Part(long[] parts, int i) => i < parts.Length ? parts[i] : 0;
}

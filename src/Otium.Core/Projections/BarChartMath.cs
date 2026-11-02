namespace Otium.Core.Projections;

/// <summary>The arithmetic behind the earnings bar chart (kept apart from drawing so it can be tested).</summary>
public static class BarChartMath
{
    /// <summary>The space between bars, in pixels.</summary>
    public const double Gap = 4;

    /// <summary>The least space between two labels, in pixels.</summary>
    public const double MinLabelSpacing = 36;

    /// <summary>Bars start moving one after another; the last begins this far (of the whole morph) after the first.</summary>
    public const double Stagger = 0.3;

    /// <summary>Which bar sits at <paramref name="x"/> (a distance from the chart's left edge); -1 between or outside bars.</summary>
    public static int BarAt(double x, double width, int count)
    {
        if (count <= 0 || width <= 0 || x < 0 || x > width)
            return -1;
        var barWidth = Math.Max(2, (width - Gap * (count - 1)) / count);
        var slot = barWidth + Gap;
        var index = (int)Math.Floor(x / slot);
        return index >= 0 && index < count && x - index * slot <= barWidth + Gap / 2 ? index : -1;
    }

    /// <summary>How many bars apart two labels must be so they don't touch.</summary>
    public static int LabelStep(double width, int count)
    {
        if (count <= 1)
            return 1;
        var slot = (width + Gap) / count;
        return Math.Max(1, (int)Math.Ceiling(MinLabelSpacing / Math.Max(1, slot)));
    }

    /// <summary>How far bar <paramref name="index"/> of <paramref name="count"/> has morphed at <paramref name="growth"/> (0..1).</summary>
    public static double GrowthOf(double growth, int index, int count)
    {
        var delay = count <= 1 ? 0 : Stagger * index / (count - 1);
        return Math.Clamp((Math.Clamp(growth, 0, 1) - delay) / (1 - Stagger), 0, 1);
    }

    /// <summary>
    /// The heights <paramref name="source"/> stretched or squeezed to <paramref name="count"/> bars, by position, so 24
    /// hourly bars can morph into 7 daily ones. Empty source: all zero (the bars grow from nothing).
    /// </summary>
    public static double[] Resample(double[] source, int count)
    {
        var result = new double[Math.Max(0, count)];
        if (source.Length == 0 || result.Length == 0)
            return result;
        for (var i = 0; i < result.Length; i++)
        {
            var position = Math.Clamp((i + 0.5) * source.Length / result.Length - 0.5, 0, source.Length - 1);
            var low = (int)Math.Floor(position);
            var high = Math.Min(low + 1, source.Length - 1);
            result[i] = source[low] + (source[high] - source[low]) * (position - low);
        }
        return result;
    }
}

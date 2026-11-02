using System.Globalization;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Otium.App.Controls;

/// <summary>
/// A compact bar chart of earnings (by hour, day or month). Bars grow in one after another when the data changes, the
/// last bar is the current period, labels sit under the bars, and pointing at a bar shows what it was.
/// </summary>
public sealed class EarningsChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty =
        AvaloniaProperty.Register<EarningsChart, IReadOnlyList<double>?>(nameof(Values));

    /// <summary>A short label per bar ("Tue", "14h"), thinned out to fit.</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> LabelsProperty =
        AvaloniaProperty.Register<EarningsChart, IReadOnlyList<string>?>(nameof(Labels));

    /// <summary>What to say when the pointer is on a bar ("Tuesday 13 October: €1.20").</summary>
    public static readonly StyledProperty<IReadOnlyList<string>?> TipsProperty =
        AvaloniaProperty.Register<EarningsChart, IReadOnlyList<string>?>(nameof(Tips));

    public static readonly StyledProperty<IBrush?> BarBrushProperty =
        AvaloniaProperty.Register<EarningsChart, IBrush?>(nameof(BarBrush), Brushes.MediumSeaGreen);

    public static readonly StyledProperty<IBrush?> FaintBrushProperty =
        AvaloniaProperty.Register<EarningsChart, IBrush?>(nameof(FaintBrush), Brushes.LightGray);

    public static readonly StyledProperty<IBrush?> LabelBrushProperty =
        AvaloniaProperty.Register<EarningsChart, IBrush?>(nameof(LabelBrush), Brushes.Gray);

    /// <summary>0.15 → 1 grow-in factor, animated whenever <see cref="Values"/> changes.</summary>
    public static readonly StyledProperty<double> GrowthProperty =
        AvaloniaProperty.Register<EarningsChart, double>(nameof(Growth), 1);

    private const double Gap = 4;
    private const double LabelHeight = 18;
    private const double MinLabelSpacing = 36;

    // Bars start growing one after another; the last begins this far (of the whole growth) after the first.
    private const double Stagger = 0.45;

    private int _hover = -1;
    private bool _attached;

    static EarningsChart()
    {
        AffectsRender<EarningsChart>(ValuesProperty, LabelsProperty, BarBrushProperty, FaintBrushProperty, LabelBrushProperty, GrowthProperty);
    }

    public EarningsChart()
    {
        Transitions = [new DoubleTransition { Property = GrowthProperty, Duration = TimeSpan.FromMilliseconds(900), Easing = new CubicEaseOut() }];
        ToolTip.SetShowDelay(this, 80);
    }

    public IReadOnlyList<double>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public IReadOnlyList<string>? Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    public IReadOnlyList<string>? Tips
    {
        get => GetValue(TipsProperty);
        set => SetValue(TipsProperty, value);
    }

    public IBrush? BarBrush
    {
        get => GetValue(BarBrushProperty);
        set => SetValue(BarBrushProperty, value);
    }

    public IBrush? FaintBrush
    {
        get => GetValue(FaintBrushProperty);
        set => SetValue(FaintBrushProperty, value);
    }

    public IBrush? LabelBrush
    {
        get => GetValue(LabelBrushProperty);
        set => SetValue(LabelBrushProperty, value);
    }

    public double Growth
    {
        get => GetValue(GrowthProperty);
        set => SetValue(GrowthProperty, value);
    }

    /// <summary>Which bar sits at <paramref name="x"/> (a distance from the chart's left edge); -1 between or outside bars.</summary>
    internal static int BarAt(double x, double width, int count)
    {
        if (count <= 0 || width <= 0 || x < 0 || x > width)
            return -1;
        var barWidth = Math.Max(2, (width - Gap * (count - 1)) / count);
        var slot = barWidth + Gap;
        var index = (int)Math.Floor(x / slot);
        return index >= 0 && index < count && x - index * slot <= barWidth + Gap / 2 ? index : -1;
    }

    /// <summary>How many bars apart two labels must be so they don't touch.</summary>
    internal static int LabelStep(double width, int count)
    {
        if (count <= 1)
            return 1;
        var slot = (width + Gap) / count;
        return Math.Max(1, (int)Math.Ceiling(MinLabelSpacing / Math.Max(1, slot)));
    }

    /// <summary>Where bar <paramref name="index"/> of <paramref name="count"/> is in its grow-in at <paramref name="growth"/> (0..1).</summary>
    internal static double GrowthOf(double growth, int index, int count)
    {
        var overall = Math.Clamp((growth - 0.15) / 0.85, 0, 1);
        var delay = count <= 1 ? 0 : Stagger * index / (count - 1);
        return Math.Clamp((overall - delay) / (1 - Stagger), 0, 1);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValuesProperty && _attached)
        {
            // Restart the grow-in: jump to a low value without a transition, then animate back up.
            var transitions = Transitions;
            Transitions = null;
            Growth = 0.15;
            Transitions = transitions;
            Growth = 1;
        }
        else if (change.Property == TipsProperty || change.Property == ValuesProperty)
        {
            UpdateTip();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var index = BarAt(e.GetPosition(this).X, Bounds.Width, Values?.Count ?? 0);
        if (index == _hover)
            return;
        _hover = index;
        UpdateTip();
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = -1;
        ToolTip.SetTip(this, null);
        InvalidateVisual();
    }

    private void UpdateTip()
    {
        var tips = Tips;
        ToolTip.SetTip(this, _hover >= 0 && tips is not null && _hover < tips.Count ? tips[_hover] : null);
    }

    public override void Render(DrawingContext context)
    {
        var values = Values;
        var bounds = Bounds;
        // Painted (invisibly) so the pointer is seen over the gaps between bars too.
        context.DrawRectangle(Brushes.Transparent, null, new Rect(bounds.Size));
        if (values is null || values.Count == 0 || bounds.Width <= 0 || bounds.Height <= LabelHeight + 4)
            return;

        var labels = Labels is { Count: > 0 } l && l.Count == values.Count ? l : null;
        var plotHeight = bounds.Height - (labels is null ? 0 : LabelHeight);
        var max = Math.Max(values.Max(), 1e-9);
        var barWidth = Math.Max(2, (bounds.Width - Gap * (values.Count - 1)) / values.Count);
        var radius = Math.Min(4, barWidth / 2);

        for (var i = 0; i < values.Count; i++)
        {
            var x = i * (barWidth + Gap);
            var fraction = values[i] / max;
            // Empty periods still show a short stub so the timeline reads as continuous.
            var height = Math.Max(3, fraction * (plotHeight - 2) * GrowthOf(Growth, i, values.Count));
            var rect = new Rect(x, plotHeight - height, barWidth, height);
            // Periods with earnings use the accent (the current one fully opaque); empty ones stay faint.
            var brush = values[i] > 0 ? BarBrush : FaintBrush;
            var opacity = i == _hover || i == values.Count - 1 ? 1 : values[i] > 0 ? 0.75 : 1;
            using (context.PushOpacity(opacity))
                context.DrawRectangle(brush, null, new RoundedRect(rect, radius, radius, 0, 0));
        }

        if (labels is null)
            return;
        var step = LabelStep(bounds.Width, values.Count);
        for (var i = values.Count - 1; i >= 0; i -= step)
        {
            var text = new FormattedText(labels[i], CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Typeface.Default, 11, LabelBrush);
            var center = i * (barWidth + Gap) + barWidth / 2;
            var left = Math.Clamp(center - text.Width / 2, 0, Math.Max(0, bounds.Width - text.Width));
            context.DrawText(text, new Point(left, plotHeight + 3));
        }
    }
}

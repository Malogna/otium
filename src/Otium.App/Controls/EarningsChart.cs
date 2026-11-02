using System.Globalization;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Otium.Core.Projections;

namespace Otium.App.Controls;

/// <summary>
/// A compact bar chart of earnings (by hour, day or month). When the data changes the bars morph from what was drawn
/// to the new bars (one after another, left to right), even when there are a different number of them, the last bar is
/// the current period, labels sit under the bars, and pointing at a bar shows what it was.
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

    /// <summary>0 → 1 progress of the morph from the old bars to the new, animated whenever <see cref="Values"/> changes.</summary>
    public static readonly StyledProperty<double> GrowthProperty =
        AvaloniaProperty.Register<EarningsChart, double>(nameof(Growth), 1);

    private const double Gap = BarChartMath.Gap;
    private const double LabelHeight = 18;


    // What each bar's height was (0..1 of the tallest) when the data last changed, matched to the new bars, and what
    // is on screen now, so a change that comes mid-morph carries on from where the bars are.
    private double[] _from = [];
    private double[] _shown = [];

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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValuesProperty)
        {
            // Morph from what is drawn now: start at 0 without a transition, then animate to 1.
            _from = BarChartMath.Resample(_shown, Values?.Count ?? 0);
            if (_attached)
                Replay();
            UpdateTip();
        }
        else if (change.Property == TipsProperty)
        {
            UpdateTip();
        }
    }

    /// <summary>Runs the morph from the start: jump to 0 without a transition, then animate to 1.</summary>
    private void Replay()
    {
        var transitions = Transitions;
        Transitions = null;
        Growth = 0;
        Transitions = transitions;
        Growth = 1;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        // The first time the chart appears, the bars rise from nothing.
        if (Values is { Count: > 0 } values)
        {
            _from = new double[values.Count];
            _shown = new double[values.Count];
            Replay();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var index = BarChartMath.BarAt(e.GetPosition(this).X, Bounds.Width, Values?.Count ?? 0);
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

        if (_shown.Length != values.Count)
            _shown = new double[values.Count];
        if (_from.Length != values.Count)
            _from = BarChartMath.Resample(_from, values.Count);
        for (var i = 0; i < values.Count; i++)
        {
            var x = i * (barWidth + Gap);
            var fraction = _from[i] + (values[i] / max - _from[i]) * BarChartMath.GrowthOf(Growth, i, values.Count);
            _shown[i] = fraction;
            // Empty periods still show a short stub so the timeline reads as continuous.
            var height = Math.Max(3, fraction * (plotHeight - 2));
            var rect = new Rect(x, plotHeight - height, barWidth, height);
            // Periods with earnings use the accent (the current one fully opaque); empty ones stay faint.
            var brush = values[i] > 0 ? BarBrush : FaintBrush;
            var opacity = i == _hover || i == values.Count - 1 ? 1 : values[i] > 0 ? 0.75 : 1;
            using (context.PushOpacity(opacity))
                context.DrawRectangle(brush, null, new RoundedRect(rect, radius, radius, 0, 0));
        }

        if (labels is null)
            return;
        var step = BarChartMath.LabelStep(bounds.Width, values.Count);
        using var labelFade = context.PushOpacity(0.35 + 0.65 * Math.Clamp(Growth, 0, 1)); // labels settle in as the bars land
        for (var i = values.Count - 1; i >= 0; i -= step)
        {
            var text = new FormattedText(labels[i], CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Typeface.Default, 11, LabelBrush);
            var center = i * (barWidth + Gap) + barWidth / 2;
            var left = Math.Clamp(center - text.Width / 2, 0, Math.Max(0, bounds.Width - text.Width));
            context.DrawText(text, new Point(left, plotHeight + 3));
        }
    }
}

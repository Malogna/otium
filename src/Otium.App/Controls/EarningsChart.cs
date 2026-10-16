using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;

namespace Otium.App.Controls;

/// <summary>
/// A compact bar chart of hourly earnings. Bars grow in when the data changes; the current hour is highlighted.
/// </summary>
public sealed class EarningsChart : Control
{
    public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty =
        AvaloniaProperty.Register<EarningsChart, IReadOnlyList<double>?>(nameof(Values));

    public static readonly StyledProperty<IBrush?> BarBrushProperty =
        AvaloniaProperty.Register<EarningsChart, IBrush?>(nameof(BarBrush), Brushes.MediumSeaGreen);

    public static readonly StyledProperty<IBrush?> FaintBrushProperty =
        AvaloniaProperty.Register<EarningsChart, IBrush?>(nameof(FaintBrush), Brushes.LightGray);

    /// <summary>0 → 1 grow-in factor, animated whenever <see cref="Values"/> changes.</summary>
    public static readonly StyledProperty<double> GrowthProperty =
        AvaloniaProperty.Register<EarningsChart, double>(nameof(Growth), 1);

    static EarningsChart()
    {
        AffectsRender<EarningsChart>(ValuesProperty, BarBrushProperty, FaintBrushProperty, GrowthProperty);
    }

    public EarningsChart()
    {
        Transitions = [new DoubleTransition { Property = GrowthProperty, Duration = TimeSpan.FromMilliseconds(700), Easing = new CubicEaseOut() }];
    }

    public IReadOnlyList<double>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
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

    public double Growth
    {
        get => GetValue(GrowthProperty);
        set => SetValue(GrowthProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValuesProperty && IsAttachedToVisualTree())
        {
            // Restart the grow-in: jump to a low value without a transition, then animate back up.
            var transitions = Transitions;
            Transitions = null;
            Growth = 0.15;
            Transitions = transitions;
            Growth = 1;
        }
    }

    private bool _attached;

    private bool IsAttachedToVisualTree() => _attached;

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

    public override void Render(DrawingContext context)
    {
        var values = Values;
        var bounds = Bounds;
        if (values is null || values.Count == 0 || bounds.Width <= 0 || bounds.Height <= 0)
            return;

        var max = Math.Max(values.Max(), 1e-9);
        var gap = 4.0;
        var barWidth = Math.Max(2, (bounds.Width - gap * (values.Count - 1)) / values.Count);
        var radius = Math.Min(4, barWidth / 2);

        for (var i = 0; i < values.Count; i++)
        {
            var x = i * (barWidth + gap);
            var fraction = values[i] / max;
            // Empty hours still show a short stub so the timeline reads as continuous.
            var height = Math.Max(3, fraction * (bounds.Height - 2) * Growth);
            var rect = new Rect(x, bounds.Height - height, barWidth, height);
            // Hours with earnings use the accent (the current hour fully opaque); empty hours stay faint.
            var brush = values[i] > 0 ? BarBrush : FaintBrush;
            using (context.PushOpacity(values[i] > 0 && i < values.Count - 1 ? 0.75 : 1))
                context.DrawRectangle(brush, null, new RoundedRect(rect, radius, radius, 0, 0));
        }
    }
}

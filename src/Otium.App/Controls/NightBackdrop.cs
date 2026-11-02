using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Otium.App.Controls;

/// <summary>
/// The app's night sky, like the website's: a deep blue-green night with two soft glows that drift slowly, a field of
/// faint stars that twinkle, and a fine film grain. The emerald glow brightens while Otium is earning, so the whole window
/// shows that work is being done. It redraws about ten times a second, and only while it is on screen.
/// </summary>
public sealed class NightBackdrop : Control
{
    /// <summary>0 while Otium is idle, 1 while it earns; the emerald glow follows it (with a transition).</summary>
    public static readonly StyledProperty<double> IntensityProperty =
        AvaloniaProperty.Register<NightBackdrop, double>(nameof(Intensity));

    private static readonly Color Night = Color.Parse("#08131A");
    private static readonly Color Emerald = Color.Parse("#16A672");
    private static readonly Color Blue = Color.Parse("#1B4A6B");

    private static readonly Star[] Stars = MakeStars();
    private static WriteableBitmap? _grain;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private double _phase;

    private readonly record struct Star(double X, double Y, double Radius, double Alpha, double Speed, double Offset);

    static NightBackdrop()
    {
        AffectsRender<NightBackdrop>(IntensityProperty);
    }

    public NightBackdrop()
    {
        IsHitTestVisible = false;
        Transitions = [new DoubleTransition { Property = IntensityProperty, Duration = TimeSpan.FromMilliseconds(1600), Easing = new CubicEaseInOut() }];
        _timer.Tick += (_, _) =>
        {
            _phase += 0.1;
            InvalidateVisual();
        };
    }

    public double Intensity
    {
        get => GetValue(IntensityProperty);
        set => SetValue(IntensityProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer.Stop();
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        // The ground: a night that is a shade lighter toward the bottom.
        context.FillRectangle(new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = [new GradientStop(Night, 0), new GradientStop(Color.Parse("#0A1A22"), 1)],
        }, bounds);

        // Two glows drifting slowly (the website's): emerald, lifted while Otium works, and a blue one at the other corner.
        var drift = Math.Sin(_phase / 11);
        var drift2 = Math.Cos(_phase / 14);
        Glow(context, bounds, 0.74 + 0.06 * drift, 0.24 + 0.07 * drift2, 0.70, Emerald, 0.30 + 0.26 * Math.Clamp(Intensity, 0, 1));
        Glow(context, bounds, 0.14 - 0.05 * drift2, 0.10 + 0.06 * drift, 0.62, Blue, 0.58);
        Glow(context, bounds, 0.88, 0.98, 0.55, Blue, 0.28 + 0.08 * drift);

        foreach (var star in Stars)
        {
            var twinkle = 0.55 + 0.45 * Math.Sin(_phase * star.Speed + star.Offset);
            var alpha = (byte)Math.Clamp(star.Alpha * twinkle * 255, 0, 255);
            context.DrawEllipse(new SolidColorBrush(Color.FromArgb(alpha, 228, 238, 235)), null,
                new Point(star.X * bounds.Width, star.Y * bounds.Height), star.Radius, star.Radius);
        }

        // Film grain, so the dark reads as made rather than flat.
        var grain = Grain();
        using (context.PushOpacity(0.07))
        {
            context.FillRectangle(new ImageBrush(grain)
            {
                TileMode = TileMode.Tile,
                Stretch = Stretch.None,
                DestinationRect = new RelativeRect(0, 0, grain.Size.Width, grain.Size.Height, RelativeUnit.Absolute),
            }, bounds);
        }
    }

    private static void Glow(DrawingContext context, Rect bounds, double x, double y, double radius, Color color, double alpha)
    {
        var brush = new RadialGradientBrush
        {
            Center = new RelativePoint(x, y, RelativeUnit.Relative),
            GradientOrigin = new RelativePoint(x, y, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(radius, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(radius * 1.1, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(Color.FromArgb((byte)Math.Clamp(alpha * 255, 0, 255), color.R, color.G, color.B), 0),
                new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1),
            ],
        };
        context.FillRectangle(brush, bounds);
    }

    private static Star[] MakeStars()
    {
        var random = new Random(7); // the same sky every time
        return Enumerable.Range(0, 70).Select(_ => new Star(
            random.NextDouble(),
            random.NextDouble(),
            0.6 + random.NextDouble() * 1.1,
            0.30 + random.NextDouble() * 0.55,
            0.6 + random.NextDouble() * 1.6,
            random.NextDouble() * Math.PI * 2)).ToArray();
    }

    private static WriteableBitmap Grain()
    {
        if (_grain is { } existing)
            return existing;
        const int size = 128;
        var bitmap = new WriteableBitmap(new PixelSize(size, size), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (var frame = bitmap.Lock())
        {
            var random = new Random(11);
            var row = new byte[frame.RowBytes];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var a = (byte)random.Next(0, 160);
                    // Premultiplied white: every channel is the alpha.
                    row[x * 4] = a;
                    row[x * 4 + 1] = a;
                    row[x * 4 + 2] = a;
                    row[x * 4 + 3] = a;
                }
                System.Runtime.InteropServices.Marshal.Copy(row, 0, frame.Address + y * frame.RowBytes, frame.RowBytes);
            }
        }
        return _grain = bitmap;
    }
}

using System.Diagnostics;
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
/// The app's night sky, like the website's, and alive: a deep blue-green night with two glows and two slow aurora bands
/// drifting across it, stars that twinkle and creep sideways, small lights that rise from below and sway, now and then a
/// shooting star, and a fine film grain. While Otium is earning, the emerald glow brightens and the lights rise faster, so the
/// whole window shows that work is being done. It redraws about thirty times a second, and only while the window is on screen.
/// </summary>
public sealed class NightBackdrop : Control
{
    /// <summary>0 while Otium is idle, 1 while it earns; the emerald glow follows it (with a transition).</summary>
    public static readonly StyledProperty<double> IntensityProperty =
        AvaloniaProperty.Register<NightBackdrop, double>(nameof(Intensity));

    private static readonly Color Night = Color.Parse("#08131A");
    private static readonly Color Emerald = Color.Parse("#16A672");
    private static readonly Color Teal = Color.Parse("#14A3A8");
    private static readonly Color Blue = Color.Parse("#1B4A6B");

    private static readonly Star[] Stars = MakeStars();
    private static readonly Mote[] Motes = MakeMotes();
    private static WriteableBitmap? _grain;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _time;
    private Window? _window;

    private readonly record struct Star(double X, double Y, double Radius, double Alpha, double Speed, double Offset, double Drift);

    private readonly record struct Mote(double X, double Size, double Sway, double Period, double Offset);

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
            _time = _clock.Elapsed.TotalSeconds;
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
        _window = TopLevel.GetTopLevel(this) as Window;
        if (_window is not null)
            _window.PropertyChanged += OnWindowChanged;
        UpdateTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_window is not null)
            _window.PropertyChanged -= OnWindowChanged;
        _window = null;
        _timer.Stop();
    }

    private void OnWindowChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.IsVisibleProperty || e.Property == Window.WindowStateProperty)
            UpdateTimer();
    }

    /// <summary>The sky only moves while the window is on screen: hidden in the tray or minimized it costs no frames, and no wake-ups either.</summary>
    private void UpdateTimer()
    {
        if (_window is { IsVisible: true, WindowState: not WindowState.Minimized })
            _timer.Start();
        else
            _timer.Stop();
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;
        var t = _time;
        var energy = Math.Clamp(Intensity, 0, 1);

        // The ground: a night that is a shade lighter toward the bottom.
        context.FillRectangle(new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = [new GradientStop(Night, 0), new GradientStop(Color.Parse("#0A1A22"), 1)],
        }, bounds);

        // Glows that wander across the window, each on its own slow loop (about 20 to 30 seconds).
        var a = t / 3.6;
        var b = t / 4.4;
        Glow(context, bounds, 0.72 + 0.18 * Math.Sin(a), 0.26 + 0.16 * Math.Cos(a * 0.8), 0.74, Emerald, 0.30 + 0.28 * energy);
        Glow(context, bounds, 0.16 + 0.14 * Math.Cos(b), 0.14 + 0.12 * Math.Sin(b * 1.1), 0.66, Blue, 0.62);
        Glow(context, bounds, 0.86 - 0.10 * Math.Sin(b * 0.9), 0.96 - 0.08 * Math.Cos(a), 0.60, Blue, 0.32);

        // Two aurora bands: wide, soft, slanted, sliding and breathing.
        Aurora(context, bounds, 0.45 + 0.20 * Math.Sin(t / 7.5), 0.30 + 0.05 * Math.Cos(t / 5.0), -16 + 4 * Math.Sin(t / 9), Emerald, 0.12 + 0.12 * energy + 0.04 * Math.Sin(t / 2.3));
        Aurora(context, bounds, 0.62 - 0.22 * Math.Cos(t / 9.5), 0.70 + 0.06 * Math.Sin(t / 6.0), 14 + 5 * Math.Cos(t / 11), Teal, 0.10 + 0.04 * Math.Sin(t / 3.1 + 1));

        foreach (var star in Stars)
        {
            var twinkle = 0.5 + 0.5 * Math.Sin(t * star.Speed + star.Offset);
            var alpha = (byte)Math.Clamp(star.Alpha * (0.25 + 0.75 * twinkle) * 255, 0, 255);
            var x = (star.X + t * star.Drift) % 1.0;
            context.DrawEllipse(new SolidColorBrush(Color.FromArgb(alpha, 228, 238, 235)), null,
                new Point(x * bounds.Width, star.Y * bounds.Height), star.Radius, star.Radius);
        }

        // Small lights rising from below, swaying, brightening as they go and fading out at the top; quicker while earning.
        var rise = 0.55 + 0.9 * energy;
        foreach (var mote in Motes)
        {
            var phase = (t * rise / mote.Period + mote.Offset) % 1.0;
            var y = 1.04 - phase * 1.1;
            var x = mote.X + mote.Sway * Math.Sin(t / 2.2 * mote.Period / 12 + mote.Offset * 6);
            var fade = Math.Sin(Math.Clamp(phase, 0, 1) * Math.PI);
            var alpha = (byte)Math.Clamp(fade * (0.30 + 0.35 * energy) * 255, 0, 255);
            var point = new Point(x * bounds.Width, y * bounds.Height);
            context.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(alpha / 4), 52, 201, 143)), null, point, mote.Size * 3.2, mote.Size * 3.2);
            context.DrawEllipse(new SolidColorBrush(Color.FromArgb(alpha, 150, 240, 205)), null, point, mote.Size, mote.Size);
        }

        ShootingStar(context, bounds, t);

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

    /// <summary>A streak across the upper sky for about a second, every nine seconds or so.</summary>
    private static void ShootingStar(DrawingContext context, Rect bounds, double t)
    {
        const double every = 9.3;
        var cycle = t % every;
        const double length = 1.1;
        if (cycle > length)
            return;
        var progress = cycle / length;
        var n = (int)(t / every);
        var startX = 0.15 + 0.6 * ((n * 0.37) % 1.0);
        var startY = 0.05 + 0.25 * ((n * 0.61) % 1.0);
        var head = new Point((startX + progress * 0.28) * bounds.Width, (startY + progress * 0.16) * bounds.Height);
        var tail = new Point(head.X - 70, head.Y - 40);
        var alpha = (byte)(Math.Sin(progress * Math.PI) * 200);
        var pen = new Pen(new LinearGradientBrush
        {
            StartPoint = new RelativePoint(head, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(tail, RelativeUnit.Absolute),
            GradientStops = [new GradientStop(Color.FromArgb(alpha, 235, 250, 245), 0), new GradientStop(Color.FromArgb(0, 235, 250, 245), 1)],
        }, 1.6, lineCap: PenLineCap.Round);
        context.DrawLine(pen, tail, head);
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

    /// <summary>A wide, soft band of light: a stretched, slanted glow centred at (<paramref name="x"/>, <paramref name="y"/>) as fractions of the window.</summary>
    private static void Aurora(DrawingContext context, Rect bounds, double x, double y, double degrees, Color color, double alpha)
    {
        var centre = new Point(x * bounds.Width, y * bounds.Height);
        var rotate = Matrix.CreateTranslation(-centre.X, -centre.Y) * Matrix.CreateRotation(degrees * Math.PI / 180) * Matrix.CreateTranslation(centre.X, centre.Y);
        using (context.PushTransform(rotate))
        {
            var brush = new RadialGradientBrush
            {
                GradientStops =
                [
                    new GradientStop(Color.FromArgb((byte)Math.Clamp(alpha * 255, 0, 255), color.R, color.G, color.B), 0),
                    new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1),
                ],
            };
            context.DrawEllipse(brush, null, centre, bounds.Width * 0.62, bounds.Height * 0.11);
        }
    }

    private static Star[] MakeStars()
    {
        var random = new Random(7); // the same sky every time
        return Enumerable.Range(0, 70).Select(_ =>
        {
            var radius = 0.6 + random.NextDouble() * 1.1;
            return new Star(
                random.NextDouble(),
                random.NextDouble(),
                radius,
                0.30 + random.NextDouble() * 0.55,
                0.9 + random.NextDouble() * 2.2,
                random.NextDouble() * Math.PI * 2,
                0.0008 + (radius - 0.6) * 0.0016); // bigger (nearer) stars drift a little faster
        }).ToArray();
    }

    private static Mote[] MakeMotes()
    {
        var random = new Random(23);
        return Enumerable.Range(0, 22).Select(_ => new Mote(
            random.NextDouble(),
            0.9 + random.NextDouble() * 1.4,
            0.012 + random.NextDouble() * 0.03,
            14 + random.NextDouble() * 16,
            random.NextDouble())).ToArray();
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

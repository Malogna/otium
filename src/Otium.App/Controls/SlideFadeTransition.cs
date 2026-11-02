using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace Otium.App.Controls;

/// <summary>
/// A page transition for moving between screens: the old page fades out, and the new one fades in while sliding a short
/// way (<see cref="Distance"/>) from the side you are heading to, so going deeper feels different from going back.
/// </summary>
public sealed class SlideFadeTransition : IPageTransition
{
    public TimeSpan Duration { get; set; } = TimeSpan.FromMilliseconds(340);

    /// <summary>How far the new page starts from its place, in pixels.</summary>
    public double Distance { get; set; } = 64;

    public async Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        var tasks = new List<Task>();
        if (from is not null)
        {
            // The old page leaves the way the new one comes from, a little: it fades while drifting off toward the side it came from.
            from.RenderTransform = new TranslateTransform();
            var drift = forward ? -Distance * 0.4 : Distance * 0.4;
            tasks.Add(new Animation
            {
                Duration = TimeSpan.FromTicks(Duration.Ticks * 2 / 3),
                Easing = new CubicEaseIn(),
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(Visual.OpacityProperty, 1d), new Setter(TranslateTransform.XProperty, 0d) } },
                    new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(Visual.OpacityProperty, 0d), new Setter(TranslateTransform.XProperty, drift) } },
                },
            }.RunAsync(from, cancellationToken));
        }
        if (to is not null)
        {
            to.IsVisible = true;
            to.RenderTransform = new TranslateTransform();
            var start = forward ? Distance : -Distance;
            tasks.Add(new Animation
            {
                Duration = Duration,
                Easing = new CubicEaseOut(),
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(Visual.OpacityProperty, 0d), new Setter(TranslateTransform.XProperty, start) } },
                    new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(Visual.OpacityProperty, 1d), new Setter(TranslateTransform.XProperty, 0d) } },
                },
            }.RunAsync(to, cancellationToken));
        }

        await Task.WhenAll(tasks);

        if (from is not null && !cancellationToken.IsCancellationRequested)
        {
            from.IsVisible = false;
            from.RenderTransform = null;
        }
        if (to is not null)
            to.RenderTransform = null;
    }

    private static Animation Fade(double fromOpacity, double toOpacity, Easing easing, TimeSpan duration) => new()
    {
        Duration = duration,
        Easing = easing,
        FillMode = FillMode.Forward,
        Children =
        {
            new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(Visual.OpacityProperty, fromOpacity) } },
            new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(Visual.OpacityProperty, toOpacity) } },
        },
    };
}

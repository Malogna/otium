using Avalonia.Threading;

namespace Otium.App.Services;

/// <summary>
/// Animates a number from its current value to a new target with an ease-out curve (for the balance and projections),
/// calling <paramref name="update"/> on the UI thread for every frame.
/// </summary>
public sealed class CountUp(Action<double> update, TimeSpan? duration = null)
{
    private readonly TimeSpan _duration = duration ?? TimeSpan.FromMilliseconds(900);
    private DispatcherTimer? _timer;
    private double _from;
    private double _to;
    private DateTimeOffset _start;

    public double Current { get; private set; }

    public void AnimateTo(double target)
    {
        if (Math.Abs(target - _to) < 1e-9 && _timer is not null)
            return;
        _from = Current;
        _to = target;
        _start = DateTimeOffset.UtcNow;
        if (Math.Abs(_to - _from) < 1e-9)
        {
            Set(_to);
            return;
        }

        _timer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => Tick());
        _timer.Start();
    }

    private void Tick()
    {
        var t = Math.Clamp((DateTimeOffset.UtcNow - _start) / _duration, 0, 1);
        var eased = 1 - Math.Pow(1 - t, 3);
        Set(_from + (_to - _from) * eased);
        if (t >= 1)
            _timer?.Stop();
    }

    private void Set(double value)
    {
        Current = value;
        update(value);
    }
}

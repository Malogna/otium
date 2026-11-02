using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Otium.App.Controls;

/// <summary>
/// Holds the screens of the app and moves between them. Unlike a plain transitioning content control it keeps the screen
/// it just left (hidden, as it was), so going back shows the same screen again, instantly, instead of building it anew and
/// letting everything pop in a second time. Only the current screen and the one before it are kept.
/// </summary>
public sealed class PageStack : Panel
{
    public static readonly StyledProperty<object?> ContentProperty =
        AvaloniaProperty.Register<PageStack, object?>(nameof(Content));

    /// <summary>True while going back, so the new screen slides in from the left.</summary>
    public static readonly StyledProperty<bool> IsReversedProperty =
        AvaloniaProperty.Register<PageStack, bool>(nameof(IsReversed));

    private readonly List<(object Content, ContentControl Host)> _pages = [];
    private ContentControl? _current;
    private CancellationTokenSource? _moving;

    static PageStack()
    {
        ContentProperty.Changed.AddClassHandler<PageStack>((stack, e) => stack.Show(e.NewValue));
    }

    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public bool IsReversed
    {
        get => GetValue(IsReversedProperty);
        set => SetValue(IsReversedProperty, value);
    }

    private void Show(object? content)
    {
        if (content is null)
            return;

        var host = _pages.FirstOrDefault(p => ReferenceEquals(p.Content, content)).Host;
        if (host is null)
        {
            host = new ContentControl
            {
                Content = content,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                IsVisible = _current is null,
            };
            _pages.Add((content, host));
            Children.Add(host);
        }

        var previous = _current;
        _current = host;
        if (ReferenceEquals(previous, host))
            return;

        // Keep the current and the previous screen, drop anything older (a replaced Advanced page, the welcome screen).
        foreach (var page in _pages.ToList())
        {
            if (page.Host == host || page.Host == previous)
                continue;
            _pages.Remove(page);
            Children.Remove(page.Host);
        }

        _moving?.Cancel();
        _moving = new CancellationTokenSource();
        if (previous is null)
            return;
        _ = Move(previous, host, forward: !IsReversed, _moving.Token);
    }

    private static async Task Move(ContentControl from, ContentControl to, bool forward, CancellationToken cancel)
    {
        try
        {
            await new SlideFadeTransition { Duration = TimeSpan.FromMilliseconds(380), Distance = 56 }.Start(from, to, forward, cancel);
        }
        catch (OperationCanceledException)
        {
        }
        if (!cancel.IsCancellationRequested)
        {
            from.IsVisible = false;
        }
    }
}

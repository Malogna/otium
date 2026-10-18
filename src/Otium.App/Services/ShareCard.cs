using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Otium.App.Services;

/// <summary>
/// A picture of a result to post (1200 × 630, the size link previews use): the amount, what it's for, and the invite
/// code. Drawn off screen from ordinary controls, so it looks like the app.
/// </summary>
public static class ShareCard
{
    public static readonly PixelSize Size = new(1200, 630);

    public static byte[] Render(string caption, string amount, string detail, string? inviteCode)
    {
        var white = Brushes.White;
        var soft = new SolidColorBrush(Color.FromArgb(0xD9, 0xFF, 0xFF, 0xFF));
        var content = new StackPanel
        {
            Spacing = 18,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = "Otium", FontSize = 40, FontWeight = FontWeight.Bold, Foreground = white },
                new TextBlock { Text = caption, FontSize = 40, Foreground = soft, TextWrapping = TextWrapping.Wrap },
                new TextBlock { Text = amount, FontSize = 150, FontWeight = FontWeight.Bold, Foreground = white },
                new TextBlock { Text = detail, FontSize = 34, Foreground = soft, TextWrapping = TextWrapping.Wrap },
                new TextBlock
                {
                    Text = string.IsNullOrEmpty(inviteCode) ? "otium.lol" : $"otium.lol  ·  invite code {inviteCode}",
                    FontSize = 34,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = white,
                    Margin = new Thickness(0, 12, 0, 0),
                },
            },
        };
        var card = new Border
        {
            Width = Size.Width,
            Height = Size.Height,
            Padding = new Thickness(80, 60),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#1DB97F"), 0),
                    new GradientStop(Color.Parse("#0E8A8A"), 0.6),
                    new GradientStop(Color.Parse("#0B6E8F"), 1),
                },
            },
            Child = content,
        };

        var size = new Avalonia.Size(Size.Width, Size.Height);
        card.Measure(size);
        card.Arrange(new Rect(size));
        using var bitmap = new RenderTargetBitmap(Size);
        bitmap.Render(card);
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        return stream.ToArray();
    }
}

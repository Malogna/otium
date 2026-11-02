using Avalonia;
using Avalonia.Controls;
using Otium.App.Services;

namespace Otium.App.Views;

/// <summary>
/// The window has one size, which isn't changed by dragging or maximizing: the size where the overview's two columns
/// fill it. On a screen too small for that (a laptop at a high display scale) it is made to fit the screen, and the pages
/// scroll and rearrange themselves for the narrower width.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>The window's size, in device-independent pixels.</summary>
    public const double FixedWidth = 1100;

    public const double FixedHeight = 780;

    public MainWindow()
    {
        InitializeComponent();
        ClipboardService.Owner = this;
        Opened += (_, _) => FitToScreen();
    }

    private void FitToScreen()
    {
        if (Screens.ScreenFromWindow(this) is not { } screen)
            return;
        var area = screen.WorkingArea;
        // The work area is in physical pixels; leave room for the title bar and the screen's edges.
        var width = Math.Min(FixedWidth, area.Width / screen.Scaling - 24);
        var height = Math.Min(FixedHeight, area.Height / screen.Scaling - 64);
        if (width < FixedWidth || height < FixedHeight)
        {
            Width = Math.Max(560, width);
            Height = Math.Max(480, height);
            Position = new PixelPoint(area.X + (int)((area.Width - Width * screen.Scaling) / 2), area.Y + (int)((area.Height - (Height + 32) * screen.Scaling) / 2));
        }
    }
}

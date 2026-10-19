using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Otium.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    /// <summary>A section button: scrolls so that section starts at the top.</summary>
    private void JumpTo(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string name } || this.FindControl<Control>(name) is not { } section)
            return;
        if (section.TranslatePoint(default, Sections) is { } top)
            Scroller.Offset = new Vector(Scroller.Offset.X, Math.Max(0, top.Y - 4));
    }
}

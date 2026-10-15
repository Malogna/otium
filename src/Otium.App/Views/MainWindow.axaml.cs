using Avalonia.Controls;
using Otium.App.Services;

namespace Otium.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ClipboardService.Owner = this;
    }
}

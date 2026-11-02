using Avalonia.Controls;

namespace Otium.App.Views;

public partial class DashboardView : UserControl
{
    /// <summary>Narrower than this, the "So far" and pool balance cards stack instead of sitting side by side.</summary>
    public const double StackBelow = 640;

    private bool? _stacked;

    public DashboardView()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Arrange(e.NewSize.Width);
    }

    private void Arrange(double width)
    {
        var stacked = width < StackBelow;
        if (stacked == _stacked)
            return;
        _stacked = stacked;

        var totals = this.FindControl<Grid>("Totals")!;
        totals.ColumnDefinitions = new ColumnDefinitions(stacked ? "*" : "*,16,*");
        totals.RowDefinitions = new RowDefinitions(stacked ? "Auto,Auto" : "Auto");
        totals.RowSpacing = stacked ? 16 : 0;
        if (totals.Children.Count > 1)
        {
            Grid.SetColumn(totals.Children[1], stacked ? 0 : 2);
            Grid.SetRow(totals.Children[1], stacked ? 1 : 0);
        }
    }
}

using Avalonia.Controls;

namespace Otium.App.Views;

public partial class HomeView : UserControl
{
    /// <summary>From this width (in device-independent pixels) the overview uses two columns, so a big window isn't mostly empty.</summary>
    public const double TwoColumnsFrom = 1000;

    private bool? _wide;

    public HomeView()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Arrange(e.NewSize.Width);
    }

    /// <summary>
    /// Narrow: everything in one column, 640 wide, in order (left column, right column, footer). Wide: the left column
    /// (notices, balance, results) beside the right (what is running now, earnings, tips, invite), up to 1280 wide.
    /// </summary>
    private void Arrange(double width)
    {
        var wide = width >= TwoColumnsFrom;
        if (wide == _wide)
            return;
        _wide = wide;

        var columns = this.FindControl<Grid>("Columns")!;
        var right = this.FindControl<StackPanel>("RightColumn")!;
        var footer = this.FindControl<WrapPanel>("Footer")!;
        columns.MaxWidth = wide ? 1280 : 640;
        columns.ColumnDefinitions = new ColumnDefinitions(wide ? "*,*" : "*");
        columns.RowDefinitions = new RowDefinitions(wide ? "Auto,Auto" : "Auto,Auto,Auto");
        Grid.SetColumn(right, wide ? 1 : 0);
        Grid.SetRow(right, wide ? 0 : 1);
        Grid.SetRow(footer, wide ? 1 : 2);
        Grid.SetColumnSpan(footer, wide ? 2 : 1);
    }
}

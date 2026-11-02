using Avalonia.Controls;

namespace Otium.App.Views;

public partial class HomeView : UserControl
{
    /// <summary>From this width (in device-independent pixels) the overview uses two columns that fill the window.</summary>
    public const double TwoColumnsFrom = 1000;

    private const double VerticalMargins = 14 + 18;

    /// <summary>The screen is only too tall for the window when something unusual is open; then it still scrolls rather than cuts off.</summary>

    private bool? _wide;

    public HomeView()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Arrange(e.NewSize.Width);
        this.FindControl<ScrollViewer>("Scroller")!.SizeChanged += (_, _) => FillHeight();
    }

    /// <summary>
    /// Wide: the left column (balance, what is running now, invite) beside the right (earnings, one suggestion), both as tall as
    /// the window, so the card in the middle of each takes the height that is left and nothing is empty. Narrow: one column,
    /// 640 wide, that scrolls.
    /// </summary>
    private void Arrange(double width)
    {
        var wide = width >= TwoColumnsFrom;
        if (wide == _wide)
            return;
        _wide = wide;

        var columns = this.FindControl<Grid>("Columns")!;
        var left = this.FindControl<Grid>("LeftColumn")!;
        var right = this.FindControl<Grid>("RightColumn")!;
        columns.MaxWidth = wide ? 1200 : 640;
        columns.ColumnDefinitions = new ColumnDefinitions(wide ? "2*,3*" : "*");
        columns.RowDefinitions = new RowDefinitions(wide ? "*" : "Auto,Auto");
        left.RowDefinitions = new RowDefinitions(wide ? "Auto,*,Auto" : "Auto,Auto,Auto");
        right.RowDefinitions = new RowDefinitions(wide ? "*,Auto" : "Auto,Auto");
        this.FindControl<ScrollViewer>("Scroller")!.VerticalScrollBarVisibility = wide ? Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled : Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
        Grid.SetColumn(right, wide ? 1 : 0);
        Grid.SetRow(right, wide ? 0 : 1);
        FillHeight();
    }

    /// <summary>Wide: the columns are exactly as tall as the window shows, so the stretching cards fill it and nothing scrolls.</summary>
    private void FillHeight()
    {
        var scroller = this.FindControl<ScrollViewer>("Scroller")!;
        var columns = this.FindControl<Grid>("Columns")!;
        columns.Height = _wide == true ? Math.Max(0, scroller.Bounds.Height - VerticalMargins) : double.NaN;
    }
}

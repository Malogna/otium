using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using Otium.App.Services;
using Otium.App.ViewModels;
using Otium.App.Views;

namespace Otium.App;

public partial class App : Application
{
    private static readonly TimeSpan AccountSyncInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromHours(6);
    private AppHost? _host;
    private MainWindow? _window;
    private EarningsNotifications? _notifications;
    private NativeMenuItem? _toggleItem;
    private bool _quitting;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _host = AppHost.Create();
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _window = new MainWindow { DataContext = new MainWindowViewModel(_host) };
            _window.Closing += (_, e) =>
            {
                // Closing the window keeps Otium earning from the tray; Quit is in the tray menu.
                if (_quitting)
                    return;
                e.Cancel = true;
                _window.Hide();
            };

            CreateTrayIcon(desktop);

            _host.StartEngines();
            _notifications = new EarningsNotifications(_host);
            _host.ApplyDeviceSwitches();
            foreach (var engine in _host.Engines)
                engine.SnapshotChanged += _ => Dispatcher.UIThread.Post(UpdateTrayMenu);

            if (_host.Settings.OnboardingComplete)
                _ = _host.SyncAccountAsync(CancellationToken.None);
            var accountTimer = new DispatcherTimer { Interval = AccountSyncInterval };
            accountTimer.Tick += (_, _) =>
            {
                if (_host.Settings.OnboardingComplete)
                    _ = _host.SyncAccountAsync(CancellationToken.None);
            };
            accountTimer.Start();

            // Hour-by-hour earnings history for the chart and projections, and the live electricity price (its hourly
            // shape changes without any settings change, so re-apply it every minute; the daily fetch is cached).
            _ = _host.RefreshElectricityAsync(CancellationToken.None);
            var historyTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            historyTimer.Tick += (_, _) =>
            {
                _host.RecordHistory();
                _host.UpdateKeepAwake(); // also notices a laptop's charger being unplugged
                _ = _host.RefreshElectricityAsync(CancellationToken.None);
                _ = _host.ReportEarningsAsync(CancellationToken.None); // opt-in; sends only every few hours
                _host.ObserveMeasurement(); // the test also ends on time when the engines go quiet
            };
            historyTimer.Start();

            if (_host.Updates.IsEnabled)
            {
                var host = _host;
                DispatcherTimer.RunOnce(() => _ = host.Updates.CheckAsync(), TimeSpan.FromMinutes(1));
                var updateTimer = new DispatcherTimer { Interval = UpdateCheckInterval };
                updateTimer.Tick += (_, _) => _ = host.Updates.CheckAsync();
                updateTimer.Start();
            }

            var startMinimized = desktop.Args?.Contains("--minimized") == true && _host.Settings.OnboardingComplete;
            if (!startMinimized)
                _window.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void CreateTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var open = new NativeMenuItem("Open Otium");
        open.Click += (_, _) => ShowWindow();

        _toggleItem = new NativeMenuItem("Start earning");
        _toggleItem.Click += (_, _) =>
        {
            if (_host is null)
                return;
            if (!_host.Settings.OnboardingComplete)
                ShowWindow();
            else
                _host.SetEarning(!_host.IsEarning);
            UpdateTrayMenu();
        };

        var quit = new NativeMenuItem("Quit");
        quit.Click += async (_, _) => await QuitAsync(desktop);

        var tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Otium/Assets/otium.ico"))),
            ToolTipText = "Otium",
            Menu = new NativeMenu { Items = { open, _toggleItem, new NativeMenuItemSeparator(), quit } },
        };
        tray.Clicked += (_, _) => ShowWindow();
        TrayIcon.SetIcons(this, new TrayIcons { tray });
        UpdateTrayMenu();
    }

    private void UpdateTrayMenu()
    {
        if (_toggleItem is not null && _host is not null)
            _toggleItem.Header = _host.IsEarning ? "Stop earning" : "Start earning";
    }

    private void ShowWindow()
    {
        if (_window is null)
            return;
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private async Task QuitAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        _quitting = true;
        if (_host is not null)
            await _host.DisposeAsync(); // stops the miner before the process exits
        _window?.Close();
        desktop.Shutdown();
    }
}

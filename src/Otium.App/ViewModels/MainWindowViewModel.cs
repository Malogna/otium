using CommunityToolkit.Mvvm.ComponentModel;
using Otium.App.Services;

namespace Otium.App.ViewModels;

/// <summary>Navigation: Welcome (first run) → Home ⇄ Advanced.</summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AppHost _host;
    private HomeViewModel? _home;
    private ShellViewModel? _advanced;

    public MainWindowViewModel(AppHost host)
    {
        _host = host;
        // The window's glow follows whether anything is earning.
        foreach (var engine in host.Engines)
            engine.SnapshotChanged += _ => Avalonia.Threading.Dispatcher.UIThread.Post(UpdateGlow);
        UpdateGlow();
        CurrentPage = host.Settings.OnboardingComplete
            ? Home
            : new WelcomeViewModel(host, () => Go(Home, back: false));
    }

    [ObservableProperty]
    public partial ViewModelBase CurrentPage { get; set; }

    /// <summary>0 while nothing earns, 1 while something does: how bright the emerald glow behind the window is.</summary>
    [ObservableProperty]
    public partial double EarningGlow { get; set; }

    private void UpdateGlow() =>
        EarningGlow = _host.Engines.Any(e => e.Snapshot.State == Otium.Core.Engine.EngineState.Earning) ? 1 : 0;

    /// <summary>True while going back (Advanced → Overview), so the page slides in from the left.</summary>
    [ObservableProperty]
    public partial bool IsGoingBack { get; set; }

    private HomeViewModel Home => _home ??= new HomeViewModel(_host, () => Go(Advanced, back: false));

    private ShellViewModel Advanced => _advanced ??= new ShellViewModel(_host, () => Go(Home, back: true), StartOver);

    private void Go(ViewModelBase page, bool back)
    {
        IsGoingBack = back; // before the page changes, which is what starts the transition
        CurrentPage = page;
    }

    /// <summary>After the account was deleted: back to the first-run welcome, with a fresh Advanced page next time.</summary>
    private void StartOver()
    {
        _advanced = null;
        Go(new WelcomeViewModel(_host, () => Go(Home, back: false)), back: true);
    }
}

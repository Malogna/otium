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
        CurrentPage = host.Settings.OnboardingComplete
            ? Home
            : new WelcomeViewModel(host, () => Go(Home, back: false));
    }

    [ObservableProperty]
    public partial ViewModelBase CurrentPage { get; set; }

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

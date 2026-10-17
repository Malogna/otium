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
            : new WelcomeViewModel(host, () => CurrentPage = Home);
    }

    [ObservableProperty]
    public partial ViewModelBase CurrentPage { get; set; }

    private HomeViewModel Home => _home ??= new HomeViewModel(_host, () => CurrentPage = Advanced);

    private ShellViewModel Advanced => _advanced ??= new ShellViewModel(_host, () => CurrentPage = Home, StartOver);

    /// <summary>After the account was deleted: back to the first-run welcome, with a fresh Advanced page next time.</summary>
    private void StartOver()
    {
        _advanced = null;
        CurrentPage = new WelcomeViewModel(_host, () => CurrentPage = Home);
    }
}

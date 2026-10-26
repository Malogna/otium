using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Otium.App.Services;
using Otium.Core.Wallets;

namespace Otium.App.ViewModels;

/// <summary>
/// Turning internet sharing on in one go: every network this computer can share through, each ticked by default, with
/// a line on how it's paid and its full explanation one click away. Used by the overview's suggestion and by Advanced
/// settings when sharing is off; each network's own switch stays in Settings.
/// </summary>
public partial class SharingSetupViewModel : ViewModelBase
{
    private readonly AppHost _host;

    public SharingSetupViewModel(AppHost host, InternetSharingViewModel proxybase, MysteriumSharingViewModel mysterium)
    {
        _host = host;
        Proxybase = proxybase;
        Mysterium = mysterium;
        PolygonAddress = host.Settings.MysteriumPayoutAddress ?? "";
        proxybase.PropertyChanged += (_, _) => Refresh();
        mysterium.PropertyChanged += (_, _) => Refresh();
    }

    public InternetSharingViewModel Proxybase { get; }

    public MysteriumSharingViewModel Mysterium { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTurnOn))]
    public partial bool UseProxybase { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanTurnOn), nameof(NeedsPolygonAddress))]
    public partial bool UseMysterium { get; set; } = true;

    [ObservableProperty] public partial string PolygonAddress { get; set; } = "";
    [ObservableProperty] public partial string? PolygonAddressError { get; set; }
    [ObservableProperty] public partial string? Error { get; set; }
    [ObservableProperty] public partial bool IsBusy { get; set; }

    /// <summary>Proxybase is offered here and still off.</summary>
    public bool OffersProxybase => Proxybase.CanTurnOn;

    /// <summary>Mysterium is offered here and still off.</summary>
    public bool OffersMysterium => Mysterium.CanTurnOn;

    /// <summary>At least one network can still be turned on.</summary>
    public bool HasAnything => OffersProxybase || OffersMysterium;

    public bool CanTurnOn => !IsBusy && ((OffersProxybase && UseProxybase) || (OffersMysterium && UseMysterium));

    public bool NeedsPolygonAddress => OffersMysterium && UseMysterium && Mysterium.NeedsPayoutAddress;

    public string ProxybaseLine =>
        "AI agents and businesses that collect public web pages. Paid in your coin, like mining (Otium keeps its usual share). " +
        $"Typically {Proxybase.EstimateText}.";

    public string MysteriumLine =>
        "Business customers only. Paid in MYST to your own Polygon address; Otium takes nothing and pays the one-time " +
        "setup fee.";

    public void Refresh()
    {
        OnPropertyChanged(nameof(OffersProxybase));
        OnPropertyChanged(nameof(OffersMysterium));
        OnPropertyChanged(nameof(HasAnything));
        OnPropertyChanged(nameof(CanTurnOn));
        OnPropertyChanged(nameof(NeedsPolygonAddress));
        OnPropertyChanged(nameof(ProxybaseLine));
    }

    /// <summary>The overview's card offers "Not now"; Settings doesn't.</summary>
    public bool CanDismiss { get; init; }

    /// <summary>"Not now" on the overview: neither network is suggested again (both stay in Advanced settings).</summary>
    [RelayCommand]
    private void NotNow()
    {
        _host.Settings.BandwidthPromptDismissed = true;
        _host.Settings.MysteriumPromptDismissed = true;
        _host.SaveSettings();
        Dismissed?.Invoke();
    }

    public event Action? Dismissed;

    [RelayCommand]
    private async Task TurnOn()
    {
        if (NeedsPolygonAddress)
        {
            if (!CryptoAddresses.TryValidate(AddressFormat.Evm, PolygonAddress, out var error))
            {
                PolygonAddressError = error ?? "That isn't a valid 0x address. Copy it from your wallet.";
                return;
            }
            PolygonAddressError = null;
            _host.Settings.MysteriumPayoutAddress = PolygonAddress.Trim().ToLowerInvariant();
            _host.SaveSettings();
        }

        IsBusy = true;
        Error = null;
        Refresh();
        try
        {
            if (OffersProxybase && UseProxybase)
                await _host.SetBandwidthSharingAsync(true);
            if (OffersMysterium && UseMysterium)
                await _host.SetMysteriumSharingAsync(true);
        }
        catch (InvalidOperationException e)
        {
            Error = e.Message;
        }
        finally
        {
            IsBusy = false;
            Refresh();
        }
    }
}

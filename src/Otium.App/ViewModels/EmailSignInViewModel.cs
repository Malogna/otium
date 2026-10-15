using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Otium.App.Services;
using Otium.Core.Accounts;

namespace Otium.App.ViewModels;

/// <summary>
/// Email + 6-digit code. The same flow secures a new account and signs a new computer in to an existing one;
/// the server decides which based on whether the email is already in use.
/// </summary>
public partial class EmailSignInViewModel(AppHost host, Action? completed = null) : ViewModelBase
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCodeCommand))]
    public partial string Email { get; set; } = host.Settings.Account.LastPayPalEmail ?? "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(VerifyCommand))]
    public partial string Code { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEnteringEmail))]
    public partial bool IsEnteringCode { get; set; }

    public bool IsEnteringEmail => !IsEnteringCode;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCodeCommand), nameof(VerifyCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty] public partial string? Error { get; set; }
    [ObservableProperty] public partial string? Info { get; set; }

    private bool CanSend() => !IsBusy && EmailAddresses.IsValid(Email);

    private bool CanVerify() => !IsBusy && Code.Trim().Length == 6;

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendCode()
    {
        await RunAsync(async () =>
        {
            await host.SendEmailCodeAsync(Email);
            IsEnteringCode = true;
            Code = "";
            Info = $"We sent a 6-digit code to {Email.Trim()}. It's valid for 15 minutes.";
        });
    }

    [RelayCommand(CanExecute = nameof(CanVerify))]
    private async Task Verify()
    {
        await RunAsync(async () =>
        {
            await host.VerifyEmailCodeAsync(Email, Code);
            IsEnteringCode = false;
            Info = null;
            completed?.Invoke();
        });
    }

    [RelayCommand]
    private void ChangeEmail()
    {
        IsEnteringCode = false;
        Error = null;
        Info = null;
    }

    private async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        Error = null;
        try
        {
            await action();
        }
        catch (AccountRequestException e)
        {
            Error = e.Message;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            Error = "Couldn't reach Otium. Check your internet connection and try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

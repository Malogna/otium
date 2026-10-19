using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Avalonia.Threading;
using Otium.App.Services;
using Otium.App.ViewModels;
using Otium.App.Views;

// Usage: dotnet run --project tools/Otium.Screenshots -- <output folder>
// Draws each screen off-screen, in light and dark, into PNG files. A temporary data folder and simulated miners keep
// any real Otium install (settings, engines, running miners) out of it.
// Velopack's locator, which the update service expects (outside an installed app it does nothing else).
Velopack.VelopackApp.Build().Run();
var output = Path.GetFullPath(args.Length > 0 ? args[0] : "screenshots");
Directory.CreateDirectory(output);
var home = Path.Combine(Path.GetTempPath(), $"otium-screens-{Guid.NewGuid():N}");
Environment.SetEnvironmentVariable("OTIUM_HOME", home);
Environment.SetEnvironmentVariable("OTIUM_SIMULATE", "1");
Environment.SetEnvironmentVariable("OTIUM_SERVER_URL", "http://127.0.0.1:9"); // nothing listens: no server calls succeed

AppBuilder.Configure<Otium.App.App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .WithInterFont()
    .SetupWithoutStarting();

var host = AppHost.Create();
host.Settings.OnboardingComplete = true;
host.Settings.EarningEnabled = true;
host.Settings.PayoutAddress = "USDT:TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t";
host.Settings.Referral.MyCode = "7KQ2M9X";
// A day and a half of earning, so the earnings tiles and the chart show real totals.
var historyStart = DateTimeOffset.Now.AddHours(-36);
for (var hour = 0; hour <= 36; hour++)
    host.History.Record(historyStart.AddHours(hour), hour * 0.09, hour * 0.02, TimeSpan.FromHours(1));

foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
{
    Application.Current!.RequestedThemeVariant = variant;
    var theme = variant == ThemeVariant.Light ? "light" : "dark";

    // Welcome: the two ways in, then Quick start's and Customize's last step.
    var welcome = new WelcomeViewModel(host, () => { });
    Shoot(new WelcomeView { DataContext = welcome }, $"welcome-choice-{theme}", 900, 1000);
    welcome.Payout.Address = "TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t";
    welcome.QuickStartCommand.Execute(null);
    Pump();
    welcome.Step = WelcomeViewModel.LastStep;
    Shoot(new WelcomeView { DataContext = welcome }, $"welcome-quick-summary-{theme}", 900, 1450);
    welcome.IsQuick = false;
    Shoot(new WelcomeView { DataContext = welcome }, $"welcome-custom-summary-{theme}", 900, 1500);

    // Home, with every result card showing.
    var homeVm = new HomeViewModel(host, () => { });
    var results = homeVm.Results;
    results.ShowMeasureOffer = true;
    results.IsMeasuring = true;
    results.MeasureProgress = 45;
    results.MeasureStatus = "Measuring… about 1 min left";
    results.ShowMeasureResult = true;
    results.MeasureAmount = "$41.20";
    results.MeasureDetail = "a month, earning about 16 hours a day while you're away. That's $2.06 per full day of earning, after Otium's fee and electricity.";
    results.MeasuredDevices.Add(new MeasuredRow("AMD Ryzen 7 7700X", "Monero at 9.12 kH/s", "$0.14/day"));
    results.MeasuredDevices.Add(new MeasuredRow("NVIDIA GeForce RTX 4090", "Pearl at 104.00 TH/s", "$1.92/day"));
    results.ShowAway = true;
    results.AwayAmount = "$0.842";
    results.AwayText = "in 7 h 40 min of earning, overnight, after Otium's fee. Electricity for it: about $0.290.";
    results.ShowMonthly = true;
    results.MonthlyTitle = "October in numbers";
    results.MonthlyAmount = "$24.18";
    results.MonthlyText = "earned by this PC in 212 hours of earning, after Otium's fee, and used about $8.400 of electricity.";
    results.ShareStatus = "Copied, with your invite code. Paste it anywhere.";
    homeVm.InternetSharing.ShowSuggestion = true;
    homeVm.InternetSharing.ShowConsent = true;
    Shoot(new HomeView { DataContext = homeVm }, $"home-{theme}", 900, 2800);

    // Settings.
    var settings = new SettingsViewModel(host);
    Shoot(new SettingsView { DataContext = settings }, $"settings-{theme}", 900, 2650);
}

// The picture "Save picture" makes.
File.WriteAllBytes(Path.Combine(output, "share-card.png"), ShareCard.Render("While I was away, my PC earned", "$0.842", "in 7 h 40 min of earning, overnight", "7KQ2M9X"));

Console.WriteLine($"Saved to {output}");
try
{
    Directory.Delete(home, recursive: true);
}
catch (IOException)
{
}
return;

// Runs queued work and renders frames for about two seconds of real time (animations follow the real clock), so
// entrance animations have finished before the capture.
void Pump()
{
    for (var i = 0; i < 100; i++)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Thread.Sleep(20);
    }
}

void Shoot(Control view, string name, int width, int height)
{
    // Delayed entrance animations ("delay1", "delay2") never start off-screen, which would leave those cards
    // invisible in the picture; the real app shows them. Without the delay they render like the rest.
    foreach (var control in view.GetLogicalDescendants().OfType<StyledElement>().Prepend(view))
        control.Classes.RemoveAll(["delay1", "delay2", "delay3"]);
    var window = new Window { Width = width, Height = height, Content = view };
    window.Show();
    Pump();
    var frame = window.CaptureRenderedFrame();
    frame?.Save(Path.Combine(output, $"{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    window.Close();
    Console.WriteLine(name);
}

using Otium.Core.Platform;
using Otium.Providers.Mysterium;

namespace Otium.Tests;

/// <summary>Naming what the antivirus removed, and offering the fix that can actually work.</summary>
public class AntivirusTests
{
    private const string Engines = @"C:\Users\pat\AppData\Local\Otium\engines";

    [Fact]
    public void Defender_removals_in_the_engines_folder_are_named_with_counts()
    {
        // As Windows PowerShell's ConvertTo-Json writes it: one product as an object, one file as a bare string.
        var report = Antivirus.Parse("""
            {"products":{"displayName":"Windows Defender","productState":397568},
             "detections":[
               {"at":"2026-10-17T17:20:23.2800000Z","files":"file:_C:\\Users\\pat\\AppData\\Local\\Otium\\engines\\xmrig-6.26.0\\xmrig-6.26.0\\xmrig.exe"},
               {"at":"2026-10-17T17:22:16.5030000Z","files":["file:_C:\\Users\\pat\\AppData\\Local\\Otium\\engines\\xmrig-6.26.0\\xmrig-6.26.0\\xmrig.exe"]},
               {"at":"2026-10-16T10:00:00.0000000Z","files":"file:_C:\\Users\\pat\\Downloads\\keygen.exe"}]}
            """, Engines);

        Assert.True(report.DefenderIsActive);
        Assert.Equal(2, report.Removals.Count); // the download elsewhere isn't Otium's business
        Assert.Equal("Microsoft Defender removed xmrig.exe 2 times, most recently on Oct 17 at 17:22.",
            report.Describe(Engines, TimeZoneInfo.Utc));
    }

    [Fact]
    public void Another_antivirus_gets_its_own_steps_and_no_defender_button()
    {
        var report = Antivirus.Parse("""
            {"products":[{"displayName":"Windows Defender","productState":393472},{"displayName":"Avast Antivirus","productState":266240}],
             "detections":[]}
            """, Engines);

        Assert.False(report.DefenderIsActive);
        Assert.Equal("Avast Antivirus", report.Active!.Name);
        var text = report.Describe(Engines, TimeZoneInfo.Utc);
        Assert.Contains("Your antivirus is Avast Antivirus", text);
        Assert.Contains(Engines, text);
    }

    [Theory]
    [InlineData(397568, true)]  // 0x61100: Defender on
    [InlineData(393472, false)] // 0x60100: Defender off (another antivirus took over)
    [InlineData(266240, true)]  // 0x41000: a third-party product on
    public void Security_center_states_are_read(int state, bool enabled) =>
        Assert.Equal(enabled, Antivirus.IsEnabled(state));

    [Fact]
    public void Nothing_known_still_offers_the_one_click_fix()
    {
        var report = Antivirus.Parse("", Engines);
        Assert.True(report.DefenderIsActive);
        Assert.Equal("", report.Describe(Engines));
    }

    [Theory]
    [InlineData("        BINARY_PATH_NAME   : C:\\Users\\pat\\AppData\\Local\\Otium\\engines\\myst-1.39.7\\myst_supervisor.exe -winservice", @"C:\Users\pat\AppData\Local\Otium\engines\myst-1.39.7\myst_supervisor.exe")]
    [InlineData("        BINARY_PATH_NAME   : \"C:\\Program Files\\MysteriumVPN\\myst_supervisor.exe\" -winservice", @"C:\Program Files\MysteriumVPN\myst_supervisor.exe")]
    [InlineData("SERVICE_NAME: MysteriumVPNSupervisor", null)]
    public void The_supervisor_service_path_is_read_from_sc(string line, string? expected) =>
        Assert.Equal(expected, WindowsMystSupervisor.ServiceBinary($"[SC] QueryServiceConfig SUCCESS\r\n\r\n{line}\r\n"));

    [Fact]
    public void Only_a_service_inside_otiums_engines_folder_is_otiums()
    {
        Assert.True(WindowsMystSupervisor.IsUnder(@"C:\Users\pat\AppData\Local\Otium\engines\myst-1.39.7\myst_supervisor.exe", Engines));
        Assert.False(WindowsMystSupervisor.IsUnder(@"C:\Program Files\MysteriumVPN\myst_supervisor.exe", Engines));
        Assert.False(WindowsMystSupervisor.IsUnder(@"C:\Users\pat\AppData\Local\Otium\engines-other\myst_supervisor.exe", Engines));
    }
}

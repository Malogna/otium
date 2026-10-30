using Otium.Core.Platform;

namespace Otium.Tests;

/// <summary>
/// Asking Windows for administrator rights: through the system's command prompt, since some PCs refuse to elevate a
/// program in AppData directly (error 3, before any prompt), and only with plain helper arguments.
/// </summary>
public class ElevationTests
{
    [Fact]
    public void Otium_is_started_elevated_through_the_systems_command_prompt()
    {
        var start = Elevation.StartInfo(@"C:\Users\someone\AppData\Local\Otium.Desktop\current\Otium.exe", ["--gpu-power-setup"]);
        Assert.Equal(Path.Combine(Environment.SystemDirectory, "cmd.exe"), start.FileName);
        Assert.Equal("""/d /c ""C:\Users\someone\AppData\Local\Otium.Desktop\current\Otium.exe" --gpu-power-setup" """.TrimEnd(), start.Arguments);
        Assert.Equal("runas", start.Verb);
        Assert.True(start.UseShellExecute);

        var withSid = Elevation.StartInfo(@"C:\Otium\Otium.exe", ["--grant-large-pages", "S-1-5-21-1-2-3-1001"]);
        Assert.EndsWith(@"Otium.exe"" --grant-large-pages S-1-5-21-1-2-3-1001""", withSid.Arguments);
    }

    [Fact]
    public void Nothing_the_command_prompt_would_read_gets_through()
    {
        Assert.Throws<ArgumentException>(() => Elevation.StartInfo(@"C:\Otium\Otium.exe", ["--gpu-power", "cap & calc"]));
        Assert.Throws<ArgumentException>(() => Elevation.StartInfo(@"C:\Otium\Otium.exe", ["%PATH%"]));
        // A program path the prompt would read specially is started directly instead.
        var odd = Elevation.StartInfo(@"C:\Users\50%off\Otium.exe", ["--gpu-power-setup"]);
        Assert.Equal(@"C:\Users\50%off\Otium.exe", odd.FileName);
        Assert.Equal(["--gpu-power-setup"], odd.ArgumentList);
    }
}

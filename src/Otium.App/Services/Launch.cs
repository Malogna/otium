using System.Diagnostics;

namespace Otium.App.Services;

/// <summary>Opens URLs and folders with the OS default handler.</summary>
public static class Launch
{
    public static void Open(string target)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", [target]);
            else
                Process.Start("xdg-open", [target]);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Nothing sensible to do if the desktop has no handler; the UI also shows the path/URL as text.
        }
    }
}

namespace Otium.Core.Engine;

/// <summary>
/// Why nothing is running when the user pressed Start and every engine is off, in the plain words the Home screen
/// shows. Without it that state read "Getting ready…" for as long as it lasted.
/// </summary>
public static class NothingRunningReason
{
    public static string? Explain(bool cpuEnabled, bool gpuEnabled, int gpuCount)
    {
        if (!cpuEnabled && !gpuEnabled)
            return "Neither your processor nor your graphics card is switched on, so there's nothing to earn with. Turn one on in Advanced.";
        if (!cpuEnabled && gpuCount == 0)
            return "Only the graphics card is switched on and Otium doesn't see one it can use. Turn your processor on in Advanced to earn.";
        return null;
    }
}

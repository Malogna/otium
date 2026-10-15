using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace Otium.App.Services;

/// <summary>Lets view models copy text without holding a reference to a window.</summary>
public static class ClipboardService
{
    public static TopLevel? Owner { get; set; }

    public static async Task CopyAsync(string text)
    {
        if (Owner?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(text);
    }
}

using Avalonia.Platform.Storage;

namespace Otium.App.Services;

/// <summary>Lets view models ask where to save a file without holding a reference to a window.</summary>
public static class SaveFileService
{
    /// <summary>Shows the save dialog and writes the file. Returns the chosen path, or null when the user cancelled.</summary>
    public static async Task<string?> SaveAsync(string title, string suggestedName, string extension, byte[] content)
    {
        if (ClipboardService.Owner?.StorageProvider is not { CanSave: true } storage)
            throw new InvalidOperationException("Saving files isn't available on this system.");

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = extension,
            ShowOverwritePrompt = true,
            FileTypeChoices = [new FilePickerFileType(extension.ToUpperInvariant()) { Patterns = [$"*.{extension}"] }],
        });
        if (file is null)
            return null;

        await using (var stream = await file.OpenWriteAsync())
        {
            if (stream.CanSeek)
                stream.SetLength(0); // overwriting a longer file mustn't leave its tail behind
            await stream.WriteAsync(content);
        }
        return file.TryGetLocalPath() ?? file.Name;
    }
}

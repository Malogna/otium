using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Otium.Core.Platform;

/// <summary>
/// One Otium per data folder. A second copy would start its own miners and internet sharing next to the first, so it
/// asks the running Otium to show its window instead (it's often only in the tray) and exits.
/// </summary>
public sealed partial class SingleInstance : IDisposable
{
    private const string ShowMessage = "show";
    private const string AckMessage = "ok";

    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _stop = new();

    private SingleInstance(Mutex mutex, string pipeName)
    {
        _mutex = mutex;
        _pipeName = pipeName;
    }

    /// <summary>Claims the data folder <paramref name="root"/>, or returns null when another Otium already runs on it.</summary>
    public static SingleInstance? TryAcquire(string root)
    {
        var name = NameFor(root);
        var mutex = new Mutex(initiallyOwned: true, name, out var createdNew);
        if (createdNew)
            return new SingleInstance(mutex, name);
        mutex.Dispose();
        return null;
    }

    /// <summary>
    /// Asks the Otium running on <paramref name="root"/> to show its window, and waits for it to confirm. False when it
    /// didn't answer in time.
    /// </summary>
    public static bool AskToShow(string root, TimeSpan timeout)
    {
        var name = NameFor(root);
        var deadline = DateTime.UtcNow + timeout;
        while (deadline - DateTime.UtcNow is { Ticks: > 0 } left)
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
                pipe.Connect((int)Math.Ceiling(left.TotalMilliseconds));
                // Windows only lets the app the user just started bring a window forward; pass that right on.
                if (OperatingSystem.IsWindows())
                    AllowSetForegroundWindow(AnyProcess);
                pipe.Write(Encoding.UTF8.GetBytes(ShowMessage + "\n"));
                pipe.Flush();
                using var reader = new StreamReader(pipe, Encoding.UTF8);
                if (reader.ReadLine() == AckMessage)
                    return true;
                // No answer: on Linux a start can reach the listener the running Otium is just closing. Try again.
            }
            catch (TimeoutException)
            {
                return false;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
            Thread.Sleep(50);
        }
        return false;
    }

    /// <summary>Calls <paramref name="show"/> (on a background thread) whenever another start asks for the window.</summary>
    public void Listen(Action show) => _ = Task.Run(() => ListenAsync(show, _stop.Token));

    private async Task ListenAsync(Action show, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    _pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(ct);
                using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
                if (await reader.ReadLineAsync(ct) != ShowMessage)
                    continue;
                await pipe.WriteAsync(Encoding.UTF8.GetBytes(AckMessage + "\n"), ct);
                await pipe.FlushAsync(ct);
                show();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (IOException)
            {
                // A start that gave up halfway, or the pipe is briefly busy: wait a moment rather than spin.
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    /// <summary>The mutex and pipe name for a data folder: the same folder always gives the same name.</summary>
    internal static string NameFor(string root)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
            full = full.ToLowerInvariant();
        return "otium-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(full)))[..16];
    }

    private const int AnyProcess = -1;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllowSetForegroundWindow(int processId);

    public void Dispose()
    {
        _stop.Cancel();
        _mutex.Dispose();
    }
}

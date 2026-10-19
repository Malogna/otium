using Otium.Core.Platform;

namespace Otium.Tests;

public class SingleInstanceTests
{
    private static string NewRoot() => Path.Combine(Path.GetTempPath(), "otium-single-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void A_second_Otium_on_the_same_data_folder_doesnt_start()
    {
        var root = NewRoot();
        using var first = SingleInstance.TryAcquire(root);
        Assert.NotNull(first);
        Assert.Null(SingleInstance.TryAcquire(root));
        Assert.Null(SingleInstance.TryAcquire(root + Path.DirectorySeparatorChar)); // the same folder, written differently

        using var other = SingleInstance.TryAcquire(NewRoot()); // dev mode's second computer has its own folder
        Assert.NotNull(other);
    }

    [Fact]
    public void Once_the_first_Otium_quits_another_can_start()
    {
        var root = NewRoot();
        SingleInstance.TryAcquire(root)!.Dispose();
        using var next = SingleInstance.TryAcquire(root);
        Assert.NotNull(next);
    }

    [Fact]
    public async Task Starting_Otium_again_shows_the_running_ones_window_every_time()
    {
        var root = NewRoot();
        using var running = SingleInstance.TryAcquire(root)!;
        var shows = 0;
        var second = new TaskCompletionSource();
        running.Listen(() =>
        {
            if (Interlocked.Increment(ref shows) == 2)
                second.TrySetResult();
        });

        Assert.True(SingleInstance.AskToShow(root, TimeSpan.FromSeconds(5)));
        Assert.True(SingleInstance.AskToShow(root, TimeSpan.FromSeconds(5)));
        await second.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, shows);
    }

    [Fact]
    public void Asking_when_nothing_runs_gives_up_quietly()
    {
        Assert.False(SingleInstance.AskToShow(NewRoot(), TimeSpan.FromMilliseconds(200)));
    }
}

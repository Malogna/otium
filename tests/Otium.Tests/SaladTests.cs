using Otium.Core.Platform;

namespace Otium.Tests;

/// <summary>
/// Salad is a handoff: Otium never drives it, only notices when its WSL distribution runs a job so the graphics-card
/// miner can step aside.
/// </summary>
public class SaladTests
{
    [Theory]
    [InlineData("salad-enterprise-linux\r\n", true)]
    [InlineData("Ubuntu\r\nSalad Enterprise Linux\r\n", true)]
    [InlineData("Ubuntu\r\n", false)]
    [InlineData("There are no running distributions.\r\n", false)]
    [InlineData("", false)]
    [InlineData(null, false)] // no WSL, or it didn't answer
    public void A_running_salad_distribution_means_a_job(string? output, bool runs) =>
        Assert.Equal(runs, Salad.RunsJob(output));

    [Fact]
    public void An_empty_folder_left_by_an_uninstall_doesnt_count()
    {
        var folder = Directory.CreateTempSubdirectory("otium-salad-").FullName;
        try
        {
            Assert.False(Salad.HoldsAProgram(folder));
            File.WriteAllText(Path.Combine(folder, "Salad.exe"), "");
            Assert.True(Salad.HoldsAProgram(folder));
            Assert.False(Salad.HoldsAProgram(Path.Combine(folder, "missing")));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}

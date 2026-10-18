using Otium.Core;
using Otium.Core.Settings;

namespace Otium.Tests;

/// <summary>
/// Found in the launch rehearsal: older builds saved their default server address into settings, so a release build
/// installed over them kept talking to http://localhost:5080. Only a server the user chose is kept now.
/// </summary>
public sealed class ServerAddressSettingsTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"otium-settings-{Guid.NewGuid():N}.json");

    private AppSettings LoadWith(string json)
    {
        File.WriteAllText(_path, json);
        return new JsonFileStore<AppSettings>(_path).Load();
    }

    [Theory]
    [InlineData("""{"ServerUrl":"http://localhost:5080"}""")]
    [InlineData("""{"ServerUrl":"http://localhost:5080/"}""")]
    [InlineData("""{"ServerUrl":"  "}""")]
    [InlineData("""{"ServerUrl":null}""")]
    [InlineData("""{}""")]
    public void A_saved_build_default_means_use_this_builds_server(string json)
    {
        var settings = LoadWith(json);
        Assert.Null(settings.ServerUrl);
        Assert.Equal(AppIdentity.DefaultServerUrl, AppIdentity.ResolveServerUrl(settings.ServerUrl));
    }

    [Fact]
    public void A_server_the_user_chose_is_kept_and_saved()
    {
        var settings = LoadWith("""{"ServerUrl":" https://otium.example.org "}""");
        Assert.Equal("https://otium.example.org", settings.ServerUrl);

        new JsonFileStore<AppSettings>(_path).Save(settings);
        Assert.Equal("https://otium.example.org", new JsonFileStore<AppSettings>(_path).Load().ServerUrl);
    }

    [Fact]
    public void New_settings_dont_pin_a_server()
    {
        Assert.Null(new AppSettings().ServerUrl);
        new JsonFileStore<AppSettings>(_path).Save(new AppSettings());
        Assert.Contains("\"ServerUrl\": null", File.ReadAllText(_path));
    }

    public void Dispose() => File.Delete(_path);
}

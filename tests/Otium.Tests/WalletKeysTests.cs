using Otium.Core.Platform;

namespace Otium.Tests;

/// <summary>
/// Uninstalling sets the sharing networks' wallet keys aside (they can hold earnings not paid out yet), and
/// reinstalling puts them back, so nothing waiting is lost.
/// </summary>
public class WalletKeysTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"otium-keys-{Guid.NewGuid():N}");

    private string DataRoot => Path.Combine(_root, "Otium");
    private string Engines => Path.Combine(DataRoot, "engines");

    private void Write(string relative, string content = "x")
    {
        var path = Path.Combine(Engines, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Only_the_keys_are_kept_and_a_reinstall_puts_them_back()
    {
        Write(Path.Combine("proxybase-data", "wallet", "keyfile.enc"), "proxybase key");
        Write(Path.Combine("proxybase-data", "seller_config.json"));
        Write(Path.Combine("proxybase-data", "session_token")); // not a key: not kept
        Write(Path.Combine("myst-data", "keystore", "UTC--2026-10-25--0xabc"), "myst identity");
        Write(Path.Combine("myst-data", "logs", "node.log")); // not kept
        Write(Path.Combine("aioz-data", "privkey.json"), "aioz key");
        Write(Path.Combine("aioz-data", "home", "data", "piece")); // the stored files: not kept
        Write(Path.Combine("xmrig-6.26.0", "xmrig.exe")); // engines: not kept

        var keep = WalletKeys.KeepDir(DataRoot);
        Assert.Equal(Path.Combine(_root, "Otium wallet keys"), keep); // next to the data folder, which uninstalling deletes
        Assert.Equal(4, WalletKeys.Keep(Engines, keep));
        Assert.True(File.Exists(Path.Combine(keep, WalletKeys.NoteFile)));
        Assert.False(File.Exists(Path.Combine(keep, "proxybase-data", "session_token")));
        Assert.False(Directory.Exists(Path.Combine(keep, "aioz-data", "home")));

        // Uninstalled, then reinstalled: the keys come back before anything starts, and the folder goes.
        Directory.Delete(DataRoot, recursive: true);
        Assert.Equal(4, WalletKeys.Restore(keep, Engines));
        Assert.Equal("proxybase key", File.ReadAllText(Path.Combine(Engines, "proxybase-data", "wallet", "keyfile.enc")));
        Assert.Equal("myst identity", File.ReadAllText(Path.Combine(Engines, "myst-data", "keystore", "UTC--2026-10-25--0xabc")));
        Assert.Equal("aioz key", File.ReadAllText(Path.Combine(Engines, "aioz-data", "privkey.json")));
        Assert.False(Directory.Exists(keep));
        Assert.Equal(0, WalletKeys.Restore(keep, Engines)); // nothing left to do
    }

    [Fact]
    public void A_key_already_there_is_never_overwritten()
    {
        Write(Path.Combine("aioz-data", "privkey.json"), "old key");
        var keep = WalletKeys.KeepDir(DataRoot);
        WalletKeys.Keep(Engines, keep);
        Write(Path.Combine("aioz-data", "privkey.json"), "new key");
        Assert.Equal(0, WalletKeys.Restore(keep, Engines));
        Assert.Equal("new key", File.ReadAllText(Path.Combine(Engines, "aioz-data", "privkey.json")));
    }

    [Fact]
    public void Without_keys_nothing_is_left_behind()
    {
        Write(Path.Combine("xmrig-6.26.0", "xmrig.exe"));
        var keep = WalletKeys.KeepDir(DataRoot);
        Assert.Equal(0, WalletKeys.Keep(Engines, keep));
        Assert.False(Directory.Exists(keep));
    }

    [Fact]
    public void Uninstalling_leaves_the_engines_folder_when_the_keys_couldnt_be_set_aside()
    {
        Write(Path.Combine("aioz-data", "privkey.json"), "aioz key");
        File.WriteAllText(Path.Combine(DataRoot, "settings.json"), "{}");
        WalletKeys.DeleteDataFolder(DataRoot, keep: Engines);
        Assert.True(File.Exists(Path.Combine(Engines, "aioz-data", "privkey.json")));
        Assert.False(File.Exists(Path.Combine(DataRoot, "settings.json")));
    }
}

using Velopack;
using Velopack.Logging;
using Velopack.Sources;

namespace Otium.App.Services;

/// <summary>
/// Asks Otium's server for updates first (it serves GitHub's release feed from a cache, so app update checks don't
/// spend GitHub's 60-requests-an-hour allowance for the user's internet address), and GitHub directly if the server
/// can't be reached. Downloads come from whichever source answered the check.
/// </summary>
public sealed class FallbackUpdateSource(IUpdateSource primary, IUpdateSource fallback) : IUpdateSource
{
    private IUpdateSource _answered = primary;

    public async Task<VelopackAssetFeed> GetReleaseFeed(IVelopackLogger logger, string? appId, string channel,
        Guid? stagingId = null, VelopackAsset? latestLocalRelease = null)
    {
        try
        {
            var feed = await primary.GetReleaseFeed(logger, appId, channel, stagingId, latestLocalRelease);
            _answered = primary;
            return feed;
        }
        catch (Exception e)
        {
            logger.Warn($"Otium's update server didn't answer ({e.Message}); asking GitHub directly.");
            var feed = await fallback.GetReleaseFeed(logger, appId, channel, stagingId, latestLocalRelease);
            _answered = fallback;
            return feed;
        }
    }

    public Task DownloadReleaseEntry(IVelopackLogger logger, VelopackAsset releaseEntry, string localFile, Action<int> progress,
        CancellationToken cancelToken = default) =>
        _answered.DownloadReleaseEntry(logger, releaseEntry, localFile, progress, cancelToken);
}

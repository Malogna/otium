using Otium.Core.Updates;
using Velopack;
using Velopack.Logging;
using Velopack.Sources;

namespace Otium.App.Services;

/// <summary>
/// Velopack's view of <see cref="SignedUpdates"/>: a feed is only used when its signature checks out against a key built
/// into Otium, and only the packages that feed lists are downloaded, each checked against its SHA-256.
/// </summary>
public sealed class SignedUpdateSource(SignedUpdates updates, IUpdateOrigin origin) : IUpdateSource
{
    private VerifiedFeed? _feed;

    public async Task<VelopackAssetFeed> GetReleaseFeed(IVelopackLogger logger, string? appId, string channel,
        Guid? stagingId = null, VelopackAsset? latestLocalRelease = null)
    {
        _feed = await updates.GetFeedAsync(origin, $"releases.{channel}.json", CancellationToken.None);
        return VelopackAssetFeed.FromJson(_feed.Json);
    }

    public Task DownloadReleaseEntry(IVelopackLogger logger, VelopackAsset releaseEntry, string localFile, Action<int> progress,
        CancellationToken cancelToken = default) =>
        _feed is null
            ? throw new UpdateVerificationException("No signed update feed has been checked yet.")
            : updates.DownloadAsync(_feed, releaseEntry.FileName, localFile, progress, cancelToken);
}

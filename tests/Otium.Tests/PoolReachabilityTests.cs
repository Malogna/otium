using Otium.Core.Platform;

namespace Otium.Tests;

/// <summary>
/// Telling "something on this computer blocks the pool" (antivirus web protection, e.g. Malwarebytes flagging
/// unmineable.com) apart from "the internet is down", and saying where to allow it.
/// </summary>
public sealed class PoolReachabilityTests
{
    private sealed class FakeProbe : IConnectionProbe
    {
        public HashSet<string> Reachable { get; } = [PoolReachability.ControlHost];
        public int Calls { get; private set; }

        public Task<bool> CanConnectAsync(string host, int port, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(Reachable.Contains(host));
        }
    }

    private static PoolReachability Checker(FakeProbe probe, params string[] products) =>
        new(probe, _ => Task.FromResult<IReadOnlyList<string>>(products));

    [Fact]
    public async Task A_blocked_pool_is_explained_with_the_active_antivirus()
    {
        var probe = new FakeProbe();
        var reason = await Checker(probe, "Malwarebytes").BlockedReasonAsync("pearlpow.unmineable.com", 443, CancellationToken.None);
        Assert.Equal("something on this computer blocks the mining pool (unmineable.com). That's usually antivirus web protection: " +
                     "Malwarebytes is active here. Add unmineable.com to its allowed websites (its exclusions or allow list), and Otium carries on by itself", reason);
    }

    [Fact]
    public async Task A_reachable_pool_or_a_dead_internet_connection_isnt_called_a_block()
    {
        var probe = new FakeProbe();
        probe.Reachable.Add("rx.unmineable.com");
        Assert.Null(await Checker(probe).BlockedReasonAsync("rx.unmineable.com", 443, CancellationToken.None));

        probe.Reachable.Clear(); // nothing at all: the miner's own "connecting" says enough
        Assert.Null(await Checker(probe).BlockedReasonAsync("gulf.moneroocean.stream", 20128, CancellationToken.None));
    }

    [Fact]
    public async Task Answers_are_kept_for_a_few_minutes()
    {
        var probe = new FakeProbe();
        var time = new ManualTime(DateTimeOffset.UtcNow);
        var checker = new PoolReachability(probe, _ => Task.FromResult<IReadOnlyList<string>>([]), time);
        Assert.NotNull(await checker.BlockedReasonAsync("pearlpow.unmineable.com", 443, CancellationToken.None));
        var calls = probe.Calls;
        Assert.NotNull(await checker.BlockedReasonAsync("pearlpow.unmineable.com", 443, CancellationToken.None));
        Assert.Equal(calls, probe.Calls);

        time.Advance(PoolReachability.CacheFor);
        probe.Reachable.Add("pearlpow.unmineable.com"); // the user allowed it
        Assert.Null(await checker.BlockedReasonAsync("pearlpow.unmineable.com", 443, CancellationToken.None));
    }

    [Theory]
    [InlineData("pearlpow.unmineable.com", "unmineable.com")]
    [InlineData("gulf.moneroocean.stream", "moneroocean.stream")]
    [InlineData("unmineable.com", "unmineable.com")]
    public void The_site_to_allow_is_the_pools_domain(string host, string site) => Assert.Equal(site, PoolReachability.SiteOf(host));
}

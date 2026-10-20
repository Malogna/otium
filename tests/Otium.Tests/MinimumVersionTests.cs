using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Testing;
using Otium.Core;
using Otium.Core.Accounts;

namespace Otium.Tests;

/// <summary>
/// Once a fixed version is out, the server can require it: 0.1.0 trusted its settings file for the fee, so a copy kept
/// on 0.1.0 (updates blocked) mustn't keep using invites, GPU prices or sharing payouts. Deleting and exporting an
/// account stay open to every version.
/// </summary>
public sealed class MinimumVersionTests
{
    [Theory]
    [InlineData("0.1.1", "0.1.1", true)]
    [InlineData("0.1.2", "0.1.1", true)]
    [InlineData("0.2.0+abc123", "0.1.1", true)]
    [InlineData("0.1.0", "0.1.1", false)]
    [InlineData("0.1.1-rc.2", "0.1.1", false)] // a release candidate comes before its release
    [InlineData("0.1.0-rc.10", "0.1.0-rc.9", true)] // numbers, not text: rc.10 after rc.9
    [InlineData(null, "0.1.1", false)] // 0.1.0 and older send no version
    [InlineData("not a version", "0.1.1", false)]
    public void Versions_compare_like_releases(string? version, string minimum, bool atLeast) =>
        Assert.Equal(atLeast, AppVersions.AtLeast(version, minimum));

    [Fact]
    public async Task An_app_older_than_the_minimum_is_told_to_update_but_can_still_leave()
    {
        var db = Path.Combine(Path.GetTempPath(), $"otium-minver-{Guid.NewGuid():N}.db");
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            {
                b.UseSetting("ConnectionStrings:Otium", $"Data Source={db};Pooling=False");
                b.UseSetting("DisableBackgroundJobs", "true");
                b.UseSetting("App:MinimumVersion", "0.1.1");
            });
            var device = Guid.NewGuid().ToString("N");
            var secret = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            var register = new RegisterDeviceRequest(device, secret, "USDT:TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t", null);

            using var old = factory.CreateClient(); // like 0.1.0: no version header
            using (var refused = await old.PostAsJsonAsync("/api/v1/devices", register))
            {
                Assert.Equal(HttpStatusCode.UpgradeRequired, refused.StatusCode);
                Assert.Contains("Please update to 0.1.1", await refused.Content.ReadAsStringAsync());
            }
            Assert.Equal(HttpStatusCode.UpgradeRequired, (await old.GetAsync("/api/v1/gpu-policy")).StatusCode);

            using var current = factory.CreateClient();
            current.DefaultRequestHeaders.Add(AppVersions.Header, "0.1.1");
            (await current.PostAsJsonAsync("/api/v1/devices", register)).EnsureSuccessStatusCode();

            // The old app can still export or delete the account.
            using var export = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/devices/{device}/account/export");
            export.Headers.Add(AccountClient.SecretHeader, secret);
            Assert.Equal(HttpStatusCode.OK, (await old.SendAsync(export)).StatusCode);
        }
        finally
        {
            foreach (var file in new[] { db, db + "-wal", db + "-shm" })
                File.Delete(file);
        }
    }
}

using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Testing;
using Otium.Core;
using Otium.Core.Accounts;
using Otium.Core.Fees;

namespace Otium.Tests;

/// <summary>
/// Copies of Otium from otium.lol keep Otium's fee: they take fee-free days, the inviter's slice and the fee itself only
/// from a grant the server signed for that computer, so editing settings.json, another server or blocking otium.lol
/// gets the full built-in fee.
/// </summary>
public sealed class FeeGrantTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private string PublicKey => Convert.ToBase64String(_key.ExportSubjectPublicKeyInfo());

    private static FeeGrant Grant(string device = "device-a", DateTimeOffset? feeFree = null) =>
        new(device, 2, 0.25, "USDT:TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t", Now.AddDays(-3), feeFree ?? Now.AddDays(20), false, null, Now, Now + FeeGrant.Lifetime);

    [Fact]
    public void A_signed_grant_checks_out_for_its_own_computer_only()
    {
        var grant = Grant();
        var signature = FeeGrants.Sign(grant, _key);

        Assert.Equal(grant, FeeGrants.Verify(grant.Payload(), signature, [PublicKey], "device-a", Now.AddHours(1)));
        Assert.Null(FeeGrants.Verify(grant.Payload(), signature, [PublicKey], "device-b", Now.AddHours(1))); // copied to another computer
        Assert.Null(FeeGrants.Verify(grant.Payload(), signature, [PublicKey], "device-a", Now + FeeGrant.Lifetime)); // expired: otium.lol blocked for a week
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        Assert.Null(FeeGrants.Verify(grant.Payload(), signature, [Convert.ToBase64String(other.ExportSubjectPublicKeyInfo())], "device-a", Now)); // another server's key
        Assert.Null(FeeGrants.Verify(grant.Payload(), null, [PublicKey], "device-a", Now));
        Assert.Null(FeeGrants.Verify(grant.Payload(), "not base64!", [PublicKey], "device-a", Now));
    }

    [Theory]
    [InlineData("\n2\n", "\n0\n")] // the fee set to 0%
    [InlineData("TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t", "TJCnKsPa7y5okkXvQAidZBzqx3QyQ6sxMW")] // the inviter's slice redirected
    [InlineData("2026-11-10", "2099-11-10")] // fee-free for ever
    public void Editing_a_grant_voids_it(string from, string to)
    {
        var grant = Grant();
        var signature = FeeGrants.Sign(grant, _key);
        var edited = grant.Payload().Replace(from, to);
        Assert.NotEqual(grant.Payload(), edited);
        Assert.Null(FeeGrants.Verify(edited, signature, [PublicKey], "device-a", Now));
    }

    [Fact]
    public void A_grant_that_claims_a_longer_life_than_the_server_gives_is_refused()
    {
        var forever = Grant() with { ExpiresAt = Now.AddYears(5) };
        Assert.Null(FeeGrants.Verify(forever.Payload(), FeeGrants.Sign(forever, _key), [PublicKey], "device-a", Now));
    }

    [Fact]
    public void Empty_fields_survive_the_round_trip()
    {
        var plain = new FeeGrant("d", 2, 0.25, null, null, null, false, null, Now, Now + FeeGrant.Lifetime);
        Assert.Equal(plain, FeeGrant.Parse(plain.Payload()));
        Assert.Null(FeeGrant.Parse("otium-fee-grant 1\nonly a few lines"));
        Assert.Null(FeeGrant.Parse(null));
    }

    [Fact]
    public void The_built_in_terms_are_the_limits()
    {
        var generous = FeePolicy.Default with
        {
            FeePercent = 0,
            ReferrerShareOfFee = 1,
            RefereeFeeHoliday = TimeSpan.FromDays(365),
            ReferrerFeeHolidayPerVerifiedFriend = TimeSpan.FromDays(365),
            MaxFeeHoliday = TimeSpan.FromDays(365),
            ReferrerRewardPeriod = TimeSpan.FromDays(730),
        };
        Assert.Equal(FeePolicy.Default, generous.WithinBuiltInLimits());
        var higher = FeePolicy.Default with { FeePercent = 3 };
        Assert.Equal(3, higher.WithinBuiltInLimits().FeePercent); // a higher fee (announced 30 days ahead) still applies
    }

    [Fact]
    public void Tests_and_local_builds_are_not_release_builds()
    {
        // Release builds (the production server baked in) ignore OTIUM_* overrides; the test assembly has none baked in.
        Assert.False(AppIdentity.IsReleaseBuild);
        Assert.Equal(AppIdentity.DefaultServerUrl, AppIdentity.ResolveServerUrl());
    }

    [Fact]
    public async Task The_server_signs_a_grant_for_the_computer_asking_when_it_has_a_key()
    {
        var db = Path.Combine(Path.GetTempPath(), $"otium-grant-{Guid.NewGuid():N}.db");
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            {
                b.UseSetting("ConnectionStrings:Otium", $"Data Source={db};Pooling=False");
                b.UseSetting("DisableBackgroundJobs", "true");
                b.UseSetting("Fees:GrantSigningKey", Convert.ToBase64String(_key.ExportPkcs8PrivateKey()));
            });
            using var http = factory.CreateClient();
            var device = Guid.NewGuid().ToString("N");
            var secret = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
            using var response = await http.PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest(device, secret, "USDT:TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t", null));
            response.EnsureSuccessStatusCode();
            var summary = (await response.Content.ReadFromJsonAsync<AccountSummary>())!;

            var grant = FeeGrants.Verify(summary.FeeGrant, summary.FeeGrantSignature, [PublicKey], device, DateTimeOffset.UtcNow);
            Assert.NotNull(grant);
            Assert.Equal(FeePolicy.Default.FeePercent, grant.FeePercent);
            Assert.Equal(summary.FeeFreeUntil, grant.FeeFreeUntil);
            Assert.Null(grant.ReferrerWallet);
            Assert.False(grant.Custodial);
        }
        finally
        {
            foreach (var file in new[] { db, db + "-wal", db + "-shm" })
                File.Delete(file);
        }
    }

    [Fact]
    public async Task Without_a_key_the_server_sends_no_grant()
    {
        var db = Path.Combine(Path.GetTempPath(), $"otium-grant-{Guid.NewGuid():N}.db");
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            {
                b.UseSetting("ConnectionStrings:Otium", $"Data Source={db};Pooling=False");
                b.UseSetting("DisableBackgroundJobs", "true");
            });
            using var http = factory.CreateClient();
            using var response = await http.PostAsJsonAsync("/api/v1/devices", new RegisterDeviceRequest(Guid.NewGuid().ToString("N"),
                Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)), "USDT:TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t", null));
            response.EnsureSuccessStatusCode();
            var summary = (await response.Content.ReadFromJsonAsync<AccountSummary>())!;
            Assert.Null(summary.FeeGrant);
            Assert.Null(summary.FeeGrantSignature);
        }
        finally
        {
            foreach (var file in new[] { db, db + "-wal", db + "-shm" })
                File.Delete(file);
        }
    }

    public void Dispose() => _key.Dispose();
}

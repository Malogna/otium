using System.Net.Http.Json;

namespace Otium.Core.Accounts;

public interface IAccountClient
{
    Task<AccountSummary> RegisterAsync(RegisterDeviceRequest request, CancellationToken ct);

    /// <param name="mined">
    /// How long this computer's processor and graphics card have mined in all; the server keeps the most it was told and
    /// signs it back in the fee grant (see Fees.FeeGrant.CpuMiningFloor).
    /// </param>
    Task<AccountSummary> GetSummaryAsync(string deviceId, string deviceSecret, CancellationToken ct, MiningTimeReport? mined = null);

    Task<CashoutInfo> RequestCashoutAsync(string deviceId, string deviceSecret, CashoutRequest request, CancellationToken ct);

    Task SendEmailCodeAsync(string deviceId, string deviceSecret, EmailCodeRequest request, CancellationToken ct);

    Task<AccountSummary> VerifyEmailCodeAsync(string deviceId, string deviceSecret, EmailVerifyRequest request, CancellationToken ct);

    Task<BandwidthReportResult> ReportBandwidthAsync(string deviceId, string deviceSecret, BandwidthReport report, CancellationToken ct);

    /// <summary>Where to have internet sharing earnings paid so they reach the user's own address in their coin.</summary>
    Task<SharingRoute> GetSharingRouteAsync(string deviceId, string deviceSecret, SharingRouteRequest request, CancellationToken ct);

    /// <summary>Sends a problem report; returns the reference to quote when asking for help.</summary>
    Task<ProblemReportResult> SendProblemReportAsync(string deviceId, string deviceSecret, ProblemReport report, CancellationToken ct);

    /// <summary>Sends what this computer earned since the last report (only for users who agreed to share it).</summary>
    Task ReportEarningsAsync(string deviceId, string deviceSecret, EarningsReport report, CancellationToken ct);

    /// <summary>A referred friend's computer: the share of Otium's fee it has mined for the inviter so far.</summary>
    Task ReportReferralShareAsync(string deviceId, string deviceSecret, ReferralShareReport report, CancellationToken ct);

    /// <summary>Deletes what this computer shared for the real-earnings figures, when the user stops sharing.</summary>
    Task DeleteEarningsReportsAsync(string deviceId, string deviceSecret, CancellationToken ct);

    /// <summary>Asks Otium to pay a new Mysterium node's one-time registration fee.</summary>
    Task<MystSponsorResult> SponsorMysteriumAsync(string deviceId, string deviceSecret, MystSponsorRequest request, CancellationToken ct);

    Task<byte[]> ExportAccountDataAsync(string deviceId, string deviceSecret, CancellationToken ct);

    Task<DeleteAccountResult> DeleteAccountAsync(string deviceId, string deviceSecret, DeleteAccountRequest request, CancellationToken ct);

    Task<Switching.GpuPolicy> GetGpuPolicyAsync(CancellationToken ct);

    /// <summary>The invite code of a friend's link opened on this internet connection in the last week, or null.</summary>
    Task<string?> GetInviteLinkCodeAsync(CancellationToken ct);

    Task<InviteLeaderboard> GetInviteLeaderboardAsync(string deviceId, string deviceSecret, CancellationToken ct);
}

/// <summary>Thrown when the server rejects a request with a message meant for the user.</summary>
public sealed class AccountRequestException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

/// <summary>How long this computer's processor and graphics card have mined in all, and what each is doing now.</summary>
/// <param name="CpuState">One of <see cref="Engine.EngineStateTags"/> for the processor; null from older callers.</param>
/// <param name="GpuState">The same for the graphics card ("none" without one).</param>
public sealed record MiningTimeReport(TimeSpan Cpu, TimeSpan Gpu, string? CpuState = null, string? GpuState = null);

public sealed class AccountClient(HttpClient http) : IAccountClient
{
    public const string SecretHeader = "X-Device-Secret";

    /// <summary>Query parameters of the account check-in carrying a <see cref="MiningTimeReport"/>, in whole seconds.</summary>
    public const string CpuMinedParameter = "cpuMined";

    public const string GpuMinedParameter = "gpuMined";

    /// <summary>Query parameters carrying <see cref="MiningTimeReport.CpuState"/> and <see cref="MiningTimeReport.GpuState"/>.</summary>
    public const string CpuStateParameter = "cpuState";

    public const string GpuStateParameter = "gpuState";

    public async Task<AccountSummary> RegisterAsync(RegisterDeviceRequest request, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync("api/v1/devices", request, ct);
        return await ReadAsync<AccountSummary>(response, ct);
    }

    public async Task<AccountSummary> GetSummaryAsync(string deviceId, string deviceSecret, CancellationToken ct, MiningTimeReport? mined = null)
    {
        var query = mined is null
            ? ""
            : FormattableString.Invariant($"?{CpuMinedParameter}={Math.Floor(mined.Cpu.TotalSeconds)}&{GpuMinedParameter}={Math.Floor(mined.Gpu.TotalSeconds)}")
              + (mined.CpuState is { } cpu ? $"&{CpuStateParameter}={Uri.EscapeDataString(cpu)}" : "")
              + (mined.GpuState is { } gpu ? $"&{GpuStateParameter}={Uri.EscapeDataString(gpu)}" : "");
        using var message = new HttpRequestMessage(HttpMethod.Get, $"api/v1/devices/{Uri.EscapeDataString(deviceId)}/account{query}");
        message.Headers.Add(SecretHeader, deviceSecret);
        using var response = await http.SendAsync(message, ct);
        return await ReadAsync<AccountSummary>(response, ct);
    }

    public async Task<InviteLeaderboard> GetInviteLeaderboardAsync(string deviceId, string deviceSecret, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"api/v1/devices/{Uri.EscapeDataString(deviceId)}/invites/leaderboard");
        message.Headers.Add(SecretHeader, deviceSecret);
        using var response = await http.SendAsync(message, ct);
        return await ReadAsync<InviteLeaderboard>(response, ct);
    }

    public async Task<CashoutInfo> RequestCashoutAsync(string deviceId, string deviceSecret, CashoutRequest request, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"api/v1/devices/{Uri.EscapeDataString(deviceId)}/cashouts")
        {
            Content = JsonContent.Create(request),
        };
        message.Headers.Add(SecretHeader, deviceSecret);
        using var response = await http.SendAsync(message, ct);
        return await ReadAsync<CashoutInfo>(response, ct);
    }

    public async Task SendEmailCodeAsync(string deviceId, string deviceSecret, EmailCodeRequest request, CancellationToken ct)
    {
        using var response = await PostAsync(deviceId, deviceSecret, "email/code", request, ct);
        if (!response.IsSuccessStatusCode)
            throw new AccountRequestException(await ReadProblemDetailAsync(response, ct), (int)response.StatusCode);
    }

    public async Task<AccountSummary> VerifyEmailCodeAsync(string deviceId, string deviceSecret, EmailVerifyRequest request, CancellationToken ct)
    {
        using var response = await PostAsync(deviceId, deviceSecret, "email/verify", request, ct);
        return await ReadAsync<AccountSummary>(response, ct);
    }

    /// <summary>Which GPU algorithms to mine and where (public; the same for everyone).</summary>
    public async Task<Switching.GpuPolicy> GetGpuPolicyAsync(CancellationToken ct)
    {
        using var response = await http.GetAsync("api/v1/gpu-policy", ct);
        return await ReadAsync<Switching.GpuPolicy>(response, ct);
    }

    public async Task<string?> GetInviteLinkCodeAsync(CancellationToken ct)
    {
        using var response = await http.GetAsync("api/v1/invite-link", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            return null;
        return (await ReadAsync<InviteLinkResponse>(response, ct)).Code;
    }

    public async Task<BandwidthReportResult> ReportBandwidthAsync(string deviceId, string deviceSecret, BandwidthReport report, CancellationToken ct)
    {
        using var response = await PostAsync(deviceId, deviceSecret, "bandwidth", report, ct);
        return await ReadAsync<BandwidthReportResult>(response, ct);
    }

    public async Task<SharingRoute> GetSharingRouteAsync(string deviceId, string deviceSecret, SharingRouteRequest request, CancellationToken ct)
    {
        using var response = await PostAsync(deviceId, deviceSecret, "sharing/route", request, ct);
        return await ReadAsync<SharingRoute>(response, ct);
    }

    public async Task<ProblemReportResult> SendProblemReportAsync(string deviceId, string deviceSecret, ProblemReport report, CancellationToken ct)
    {
        using var response = await PostAsync(deviceId, deviceSecret, "problem-report", report, ct);
        return await ReadAsync<ProblemReportResult>(response, ct);
    }

    public async Task ReportReferralShareAsync(string deviceId, string deviceSecret, ReferralShareReport report, CancellationToken ct)
    {
        using var response = await PostAsync(deviceId, deviceSecret, "referral-share", report, ct);
        if (!response.IsSuccessStatusCode)
            throw new AccountRequestException(await ReadProblemDetailAsync(response, ct), (int)response.StatusCode);
    }

    public async Task ReportEarningsAsync(string deviceId, string deviceSecret, EarningsReport report, CancellationToken ct)
    {
        using var response = await PostAsync(deviceId, deviceSecret, "earnings", report, ct);
        if (!response.IsSuccessStatusCode)
            throw new AccountRequestException(await ReadProblemDetailAsync(response, ct), (int)response.StatusCode);
    }

    public async Task DeleteEarningsReportsAsync(string deviceId, string deviceSecret, CancellationToken ct)
    {
        using var response = await PostAsync(deviceId, deviceSecret, "earnings/delete", new { }, ct);
        if (!response.IsSuccessStatusCode)
            throw new AccountRequestException(await ReadProblemDetailAsync(response, ct), (int)response.StatusCode);
    }

    public async Task<MystSponsorResult> SponsorMysteriumAsync(string deviceId, string deviceSecret, MystSponsorRequest request, CancellationToken ct)
    {
        using var response = await PostAsync(deviceId, deviceSecret, "mysterium/sponsor", request, ct);
        return await ReadAsync<MystSponsorResult>(response, ct);
    }

    /// <summary>A copy of everything the server stores about the account, as a JSON document to save as-is.</summary>
    public async Task<byte[]> ExportAccountDataAsync(string deviceId, string deviceSecret, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"api/v1/devices/{Uri.EscapeDataString(deviceId)}/account/export");
        message.Headers.Add(SecretHeader, deviceSecret);
        using var response = await http.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
            throw new AccountRequestException(await ReadProblemDetailAsync(response, ct), (int)response.StatusCode);
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task<DeleteAccountResult> DeleteAccountAsync(string deviceId, string deviceSecret, DeleteAccountRequest request, CancellationToken ct)
    {
        using var response = await PostAsync(deviceId, deviceSecret, "account/delete", request, ct);
        return await ReadAsync<DeleteAccountResult>(response, ct);
    }

    private async Task<HttpResponseMessage> PostAsync(string deviceId, string deviceSecret, string path, object body, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"api/v1/devices/{Uri.EscapeDataString(deviceId)}/{path}")
        {
            Content = JsonContent.Create(body),
        };
        message.Headers.Add(SecretHeader, deviceSecret);
        return await http.SendAsync(message, ct);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var detail = await ReadProblemDetailAsync(response, ct);
            throw new AccountRequestException(detail, (int)response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<T>(ct)
               ?? throw new HttpRequestException("The Otium server returned an empty response.");
    }

    /// <summary>The server reports errors as RFC 7807 problem details; surface the human-readable "detail".</summary>
    private static async Task<string> ReadProblemDetailAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemBody>(ct);
            if (!string.IsNullOrWhiteSpace(problem?.Detail))
                return problem.Detail;
        }
        catch (System.Text.Json.JsonException)
        {
        }
        return $"The Otium server returned an error ({(int)response.StatusCode}).";
    }

    private sealed record ProblemBody(string? Detail);
}

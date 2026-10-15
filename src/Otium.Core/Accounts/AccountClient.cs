using System.Net.Http.Json;

namespace Otium.Core.Accounts;

public interface IAccountClient
{
    Task<AccountSummary> RegisterAsync(RegisterDeviceRequest request, CancellationToken ct);

    Task<AccountSummary> GetSummaryAsync(string deviceId, string deviceSecret, CancellationToken ct);

    Task<CashoutInfo> RequestCashoutAsync(string deviceId, string deviceSecret, CashoutRequest request, CancellationToken ct);

    Task SendEmailCodeAsync(string deviceId, string deviceSecret, EmailCodeRequest request, CancellationToken ct);

    Task<AccountSummary> VerifyEmailCodeAsync(string deviceId, string deviceSecret, EmailVerifyRequest request, CancellationToken ct);
}

/// <summary>Thrown when the server rejects a request with a message meant for the user.</summary>
public sealed class AccountRequestException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed class AccountClient(HttpClient http) : IAccountClient
{
    public const string SecretHeader = "X-Device-Secret";

    public async Task<AccountSummary> RegisterAsync(RegisterDeviceRequest request, CancellationToken ct)
    {
        using var response = await http.PostAsJsonAsync("api/v1/devices", request, ct);
        return await ReadAsync<AccountSummary>(response, ct);
    }

    public async Task<AccountSummary> GetSummaryAsync(string deviceId, string deviceSecret, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"api/v1/devices/{Uri.EscapeDataString(deviceId)}/account");
        message.Headers.Add(SecretHeader, deviceSecret);
        using var response = await http.SendAsync(message, ct);
        return await ReadAsync<AccountSummary>(response, ct);
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

using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Otium.Providers.Mysterium;

/// <param name="Status">"Unregistered", "InProgress", "Registered" or "RegistrationError".</param>
/// <param name="ChannelAddress">Where MYST for the registration fee goes (Polygon).</param>
/// <param name="EarningsMyst">Earned and not yet settled to the beneficiary.</param>
/// <param name="EarningsTotalMyst">Earned since the identity was created, settled or not.</param>
public sealed record MystIdentity(string Id, string Status, string ChannelAddress, decimal EarningsMyst, decimal EarningsTotalMyst)
{
    public bool IsRegistered => Status.Equals("Registered", StringComparison.OrdinalIgnoreCase);
}

public sealed record MystService(string Id, string Type, string Status);

/// <summary>
/// The Mysterium node's local API ("Tequila"), on 127.0.0.1 with a password Otium generates for each node. Shapes are
/// from node 1.39.7 (tequilapi/endpoints and tequilapi/contract), checked against a live node in October 2026.
/// </summary>
public sealed class TequilaClient
{
    private readonly HttpClient _http;

    public TequilaClient(HttpMessageHandler handler, int port, string password)
    {
        _http = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = new Uri($"http://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}/"),
            Timeout = TimeSpan.FromSeconds(30),
        };
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"myst:{password}")));
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct)
    {
        try
        {
            using var response = await _http.GetAsync("healthcheck", ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <summary>Creates the node's identity the first time, unlocks it afterwards, and returns its address.</summary>
    public async Task<string> UnlockCurrentIdentityAsync(CancellationToken ct)
    {
        using var response = await _http.PutAsJsonAsync("identities/current", new { passphrase = "" }, ct);
        var json = await ReadAsync(response, "unlock the node's identity", ct);
        return json.GetProperty("id").GetString() ?? throw new InvalidOperationException("The node returned no identity.");
    }

    public async Task<MystIdentity> GetIdentityAsync(string id, CancellationToken ct)
    {
        using var response = await _http.GetAsync($"identities/{id}", ct);
        return ParseIdentity(await ReadAsync(response, "read the node's identity", ct));
    }

    internal static MystIdentity ParseIdentity(JsonElement json) => new(
        Text(json, "id") ?? "",
        Text(json, "registration_status") ?? "",
        Text(json, "channel_address") ?? "",
        Tokens(json, "earnings_tokens"),
        Tokens(json, "earnings_total_tokens"));

    /// <summary>Records that the user agreed to Mysterium's provider terms (Otium asks in its consent screen).</summary>
    public async Task AgreeToProviderTermsAsync(CancellationToken ct)
    {
        using var current = await _http.GetAsync("terms", ct);
        var terms = await ReadAsync(current, "read the node's terms", ct);
        var version = Text(terms, "current_version") ?? "";
        if (terms.TryGetProperty("agreed_provider", out var agreed) && agreed.ValueKind == JsonValueKind.True && Text(terms, "agreed_version") == version)
            return;
        using var response = await _http.PostAsJsonAsync("terms", new { agreed_provider = true, agreed_version = version }, ct);
        await ReadAsync(response, "record the terms", ct);
    }

    public async Task WithdrawProviderTermsAsync(CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync("terms", new { agreed_provider = false }, ct);
        await ReadAsync(response, "withdraw the terms", ct);
    }

    /// <summary>
    /// Registers the identity with the user's wallet as beneficiary. The fee comes from MYST already sent to the
    /// identity's channel address; Mysterium's transactor pays the gas.
    /// </summary>
    public async Task RegisterAsync(string id, string beneficiary, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync($"identities/{id}/register", new { beneficiary }, ct);
        await ReadAsync(response, "register the node", ct);
    }

    public async Task<IReadOnlyList<MystService>> GetServicesAsync(CancellationToken ct)
    {
        using var response = await _http.GetAsync("services", ct);
        var json = await ReadAsync(response, "list the node's services", ct);
        return json.ValueKind != JsonValueKind.Array
            ? []
            : json.EnumerateArray().Select(s => new MystService(Text(s, "id") ?? "", Text(s, "type") ?? "", Text(s, "status") ?? "")).ToList();
    }

    public async Task StartServiceAsync(string providerId, string type, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync("services", new { provider_id = providerId, type }, ct);
        await ReadAsync(response, $"start the {type} service", ct);
    }

    public async Task StopServiceAsync(string serviceId, CancellationToken ct)
    {
        using var response = await _http.DeleteAsync($"services/{serviceId}", ct);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
            await ReadAsync(response, "stop a service", ct);
    }

    /// <summary>Bytes the node has relayed in all sessions it remembers (sent plus received).</summary>
    public async Task<long> GetRelayedBytesAsync(CancellationToken ct)
    {
        using var response = await _http.GetAsync("sessions/stats-aggregated", ct);
        return ParseRelayedBytes(await ReadAsync(response, "read the node's traffic", ct));
    }

    internal static long ParseRelayedBytes(JsonElement json) =>
        json.TryGetProperty("stats", out var stats) ? (long)(Number(stats, "sum_bytes_received") + Number(stats, "sum_bytes_sent")) : 0;

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response, string what, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            var message = body;
            try
            {
                using var error = JsonDocument.Parse(body);
                if (error.RootElement.TryGetProperty("error", out var e) && e.TryGetProperty("message", out var m))
                    message = m.GetString() ?? body;
            }
            catch (JsonException)
            {
            }
            throw new MystNodeException($"The Mysterium node couldn't {what}: {message}", (int)response.StatusCode);
        }
        if (string.IsNullOrWhiteSpace(body))
            return default;
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    private static string? Text(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.String or JsonValueKind.Number ? v.ToString() : null;

    private static double Number(JsonElement e, string name) =>
        Text(e, name) is { } s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;

    /// <summary>MYST amounts come as {"wei": "...", "ether": "0.42", "human": "0.42"}.</summary>
    private static decimal Tokens(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var t) && Text(t, "ether") is { } ether
        && decimal.TryParse(ether, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
}

public sealed class MystNodeException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

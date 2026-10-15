using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using Otium.Core.Accounts;
using Otium.Core.Pools;
using Otium.Server;
using Otium.Server.Accounts;
using Otium.Server.Data;
using Otium.Server.Earnings;
using Otium.Server.Payouts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ReferralOptions>(builder.Configuration.GetSection(ReferralOptions.Section));
builder.Services.Configure<EarningsOptions>(builder.Configuration.GetSection(EarningsOptions.Section));
builder.Services.Configure<CashoutOptions>(builder.Configuration.GetSection(CashoutOptions.Section));
builder.Services.Configure<PayPalOptions>(builder.Configuration.GetSection(PayPalOptions.Section));
builder.Services.Configure<MoneroWalletOptions>(builder.Configuration.GetSection(MoneroWalletOptions.Section));
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.Section));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(_ => new OtiumStore(builder.Configuration.GetConnectionString("Otium") ?? "Data Source=otium.db"));
builder.Services.AddHttpClient<IMoneroPoolApi, MoneroOceanClient>();
builder.Services.AddSingleton<IMoneroWallet, MoneroWalletRpc>();
builder.Services.AddSingleton<PoolWalletAllocator>();
builder.Services.AddSingleton<AccountService>();
builder.Services.AddSingleton<EarningsCreditor>();
builder.Services.AddHttpClient<PayPalPayoutProvider>();
builder.Services.AddSingleton<IPayoutProvider>(sp => sp.GetRequiredService<PayPalPayoutProvider>());
builder.Services.AddSingleton<IPayoutProvider, MoneroPayoutProvider>();
builder.Services.AddSingleton<CashoutService>();
builder.Services.AddSingleton<CashoutProcessor>();
if (!builder.Configuration.GetValue<bool>("DisableBackgroundJobs"))
{
    builder.Services.AddHostedService<EarningsCreditorService>();
    builder.Services.AddHostedService<CashoutProcessorService>();
}

builder.Services.AddProblemDetails();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("per-ip", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();
app.UseRateLimiter();

// Errors meant for users travel as problem details; the app shows their "detail" text.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (OtiumException e)
    {
        context.Response.StatusCode = e.StatusCode;
        await Results.Problem(e.Message, statusCode: e.StatusCode).ExecuteAsync(context);
    }
});

string PublicBase(HttpRequest request, IOptions<ReferralOptions> options) =>
    options.Value.PublicBaseUrl ?? $"{request.Scheme}://{request.Host}";

string Secret(HttpRequest request) => request.Headers[AccountClient.SecretHeader].ToString();

// ---------- app API ----------

var api = app.MapGroup("/api/v1").RequireRateLimiting("per-ip");

api.MapPost("/devices", async (RegisterDeviceRequest body, HttpRequest request, AccountService accounts, IOptions<ReferralOptions> options, CancellationToken ct) =>
    Results.Ok(await accounts.RegisterAsync(body, PublicBase(request, options), ct)));

api.MapGet("/devices/{deviceId}/account", async (string deviceId, HttpRequest request, AccountService accounts, IOptions<ReferralOptions> options, CancellationToken ct) =>
    Results.Ok(await accounts.GetSummaryAsync(deviceId, Secret(request), PublicBase(request, options), ct)));

api.MapPost("/devices/{deviceId}/cashouts", (string deviceId, CashoutRequest body, HttpRequest request, AccountService accounts, CashoutService cashouts) =>
{
    var device = accounts.Authenticate(deviceId, Secret(request));
    return Results.Ok(cashouts.Request(device, body));
});

app.MapGet("/r/{code}", (string code, OtiumStore store, IOptions<ReferralOptions> options) =>
{
    var normalized = ReferralCodes.Normalize(code);
    var exists = ReferralCodes.IsWellFormed(normalized) && store.CodeExists(normalized);
    return Results.Content(LandingPage.Render(normalized, options.Value.DownloadUrl, exists), "text/html; charset=utf-8");
}).RequireRateLimiting("per-ip");

app.MapGet("/healthz", () => Results.Ok("ok"));

// ---------- operator API ----------

var admin = app.MapGroup("/admin").AddEndpointFilter(async (context, next) =>
{
    var configured = context.HttpContext.RequestServices.GetRequiredService<IOptions<AdminOptions>>().Value.Token;
    var presented = context.HttpContext.Request.Headers["X-Admin-Token"].ToString();
    if (configured.Length == 0 || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(configured), Encoding.UTF8.GetBytes(presented)))
        return Results.Unauthorized();
    return await next(context);
});

admin.MapGet("/summary", async (OtiumStore store, IMoneroPoolApi pool, TimeProvider time, CancellationToken ct) =>
{
    var totals = store.Totals(time.GetUtcNow());
    double poolEarnedXmr = 0;
    foreach (var (address, _) in store.PoolWalletLoads())
    {
        if (await pool.GetMinerStatsAsync(address, ct) is { } stats)
            poolEarnedXmr += stats.PaidXmr + stats.PendingXmr;
    }
    var price = (await pool.GetNetworkStatsAsync(ct)).PriceUsd;
    return Results.Ok(new
    {
        creditedToUsersUsd = AccountService.ToUsd(totals.Credited),
        referralRewardsUsd = AccountService.ToUsd(totals.Referral),
        paidOutUsd = AccountService.ToUsd(totals.Paid),
        owedToUsersUsd = AccountService.ToUsd(totals.Liability),
        poolEarnedXmr,
        poolEarnedUsdAtTodaysPrice = Math.Round(poolEarnedXmr * price, 2),
        reviewQueue = store.CashoutsWithStatus(CashoutStatuses.Review).Count,
        poolAddresses = store.PoolWalletLoads().Select(w => new { w.Address, w.Accounts }),
    });
});

admin.MapGet("/cashouts", (string? status, OtiumStore store) =>
    Results.Ok(store.CashoutsWithStatus(string.IsNullOrEmpty(status) ? CashoutStatuses.Review : status)));

admin.MapPost("/cashouts/{id}/approve", (string id, OtiumStore store, TimeProvider time) =>
{
    var cashout = store.FindCashout(id);
    if (cashout is not { Status: CashoutStatuses.Review })
        return Results.NotFound();
    store.UpdateCashout(id, CashoutStatuses.Queued, time.GetUtcNow(), note: $"approved: {cashout.Note}");
    return Results.Ok();
});

admin.MapPost("/cashouts/{id}/reject", (string id, AdminNote body, OtiumStore store, TimeProvider time) =>
{
    var cashout = store.FindCashout(id);
    if (cashout is null || cashout.Status is CashoutStatuses.Paid or CashoutStatuses.Failed)
        return Results.NotFound();
    store.FailAndRefund(cashout, body.Note ?? "rejected", time.GetUtcNow());
    return Results.Ok();
});

admin.MapPost("/cashouts/{id}/mark-paid", (string id, AdminNote body, OtiumStore store, TimeProvider time) =>
{
    var cashout = store.FindCashout(id);
    if (cashout is null || cashout.Status is CashoutStatuses.Paid or CashoutStatuses.Failed)
        return Results.NotFound();
    store.UpdateCashout(id, CashoutStatuses.Paid, time.GetUtcNow(), providerReference: body.Note ?? "manual");
    return Results.Ok();
});

app.Run();

public sealed record AdminNote(string? Note);

public partial class Program;

using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using Otium.Core.Accounts;
using Otium.Core.Pools;
using Otium.Server;
using Otium.Server.Accounts;
using Otium.Server.Bandwidth;
using Otium.Server.Data;
using Otium.Server.Earnings;
using Otium.Server.Gpu;
using Otium.Server.Payouts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ReferralOptions>(builder.Configuration.GetSection(ReferralOptions.Section));
builder.Services.Configure<EarningsOptions>(builder.Configuration.GetSection(EarningsOptions.Section));
builder.Services.Configure<CashoutOptions>(builder.Configuration.GetSection(CashoutOptions.Section));
builder.Services.Configure<PayPalOptions>(builder.Configuration.GetSection(PayPalOptions.Section));
builder.Services.Configure<MoneroWalletOptions>(builder.Configuration.GetSection(MoneroWalletOptions.Section));
builder.Services.Configure<BitcoinWalletOptions>(builder.Configuration.GetSection(BitcoinWalletOptions.Section));
builder.Services.Configure<ThorchainOptions>(builder.Configuration.GetSection(ThorchainOptions.Section));
builder.Services.Configure<SanctionsOptions>(builder.Configuration.GetSection(SanctionsOptions.Section));
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.Section));
builder.Services.Configure<BandwidthOptions>(builder.Configuration.GetSection(BandwidthOptions.Section));
builder.Services.Configure<GpuPolicyOptions>(builder.Configuration.GetSection(GpuPolicyOptions.Section));
builder.Services.Configure<KryptexOptions>(builder.Configuration.GetSection(KryptexOptions.Section));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(_ => new OtiumStore(builder.Configuration.GetConnectionString("Otium") ?? "Data Source=otium.db"));
builder.Services.AddHttpClient<IMoneroPoolApi, MoneroOceanClient>();
builder.Services.AddSingleton<IMoneroWallet, MoneroWalletRpc>();
builder.Services.AddSingleton<IBitcoinWallet, BitcoinWalletRpc>();
builder.Services.AddHttpClient<IThorchainApi, ThorchainClient>();
builder.Services.AddSingleton<SanctionsList>();
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.Section));
builder.Services.AddSingleton<OutboxEmailSender>();
builder.Services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<IOptions<EmailOptions>>().Value.IsConfigured
    ? new SmtpEmailSender(sp.GetRequiredService<IOptions<EmailOptions>>())
    : sp.GetRequiredService<OutboxEmailSender>());
builder.Services.AddSingleton<PoolWalletAllocator>();
builder.Services.AddSingleton<BandwidthService>();
builder.Services.AddSingleton<AccountService>();
builder.Services.AddSingleton<EarningsCreditor>();
builder.Services.AddHttpClient<IProfitFeed, WhatToMineClient>();
builder.Services.AddHttpClient<IKryptexApi, KryptexClient>();
builder.Services.AddSingleton<GpuPolicyService>();
builder.Services.AddSingleton<KryptexCreditor>();
if (builder.Configuration.GetValue<bool>("DevMode:FakePayouts"))
{
    // Development: cash-outs "arrive" after a few seconds without touching a wallet, THORChain or PayPal.
    foreach (var method in new[] { CashoutMethods.Bitcoin, CashoutMethods.Ethereum, CashoutMethods.Bnb, CashoutMethods.Solana, CashoutMethods.PayPal, CashoutMethods.Monero })
        builder.Services.AddSingleton<IPayoutProvider>(sp => new FakePayoutProvider(method, sp.GetRequiredService<TimeProvider>()));
}
else
{
    // Crypto: BTC from the operator's bitcoind wallet; ETH, BNB and SOL swapped from it through THORChain.
    builder.Services.AddSingleton<IPayoutProvider, BitcoinPayoutProvider>();
    foreach (var (method, route) in ThorchainPayoutProvider.Routes)
    {
        builder.Services.AddSingleton<IPayoutProvider>(sp => new ThorchainPayoutProvider(method, route.Asset, route.Chain,
            sp.GetRequiredService<IBitcoinWallet>(), sp.GetRequiredService<IThorchainApi>(), sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<ThorchainPayoutProvider>>()));
    }
    // Older methods, off unless listed in Cashouts:Methods.
    builder.Services.AddHttpClient<PayPalPayoutProvider>();
    builder.Services.AddSingleton<IPayoutProvider>(sp => sp.GetRequiredService<PayPalPayoutProvider>());
    builder.Services.AddSingleton<IPayoutProvider, MoneroPayoutProvider>();
}
builder.Services.AddSingleton<CashoutMethodCatalog>();
builder.Services.AddSingleton<CashoutService>();
builder.Services.AddSingleton<CashoutReceipts>();
builder.Services.AddSingleton<CashoutProcessor>();
if (!builder.Configuration.GetValue<bool>("DisableBackgroundJobs"))
{
    builder.Services.AddHostedService<EarningsCreditorService>();
    if (builder.Configuration.GetValue<bool>("Kryptex:Enabled"))
        builder.Services.AddHostedService<KryptexCreditorService>();
    builder.Services.AddHostedService<CashoutProcessorService>();
    builder.Services.AddHttpClient<SanctionsRefresher>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<SanctionsRefresher>());
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

api.MapGet("/devices/{deviceId}/invites/leaderboard", async (string deviceId, HttpRequest request, AccountService accounts, CancellationToken ct) =>
    Results.Ok(await accounts.GetLeaderboardAsync(deviceId, Secret(request), ct)));

api.MapPost("/devices/{deviceId}/cashouts", (string deviceId, CashoutRequest body, HttpRequest request, AccountService accounts, CashoutService cashouts) =>
{
    var session = accounts.Authenticate(deviceId, Secret(request));
    return Results.Ok(cashouts.Request(session.Account, body));
});

// Bandwidth usage reports. The connection's public IP enforces one paid sharer per internet connection; when deployed
// behind a reverse proxy, enable forwarded headers so this is the client's address, not the proxy's.
api.MapPost("/devices/{deviceId}/bandwidth", (string deviceId, BandwidthReport body, HttpContext context, AccountService accounts, BandwidthService bandwidth) =>
{
    var session = accounts.Authenticate(deviceId, Secret(context.Request));
    var ip = context.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "unknown";
    return Results.Ok(bandwidth.Report(session, body, ip));
});

api.MapPost("/devices/{deviceId}/email/code", async (string deviceId, EmailCodeRequest body, HttpRequest request, AccountService accounts, CancellationToken ct) =>
{
    await accounts.SendSignInCodeAsync(deviceId, Secret(request), body, ct);
    return Results.NoContent();
});

api.MapPost("/devices/{deviceId}/email/verify", (string deviceId, EmailVerifyRequest body, HttpRequest request, AccountService accounts, IOptions<ReferralOptions> options) =>
    Results.Ok(accounts.VerifySignInCode(deviceId, Secret(request), body, PublicBase(request, options))));

// Which GPU algorithms to mine and where; the same for every app, rebuilt from live feeds every few minutes.
api.MapGet("/gpu-policy", async (GpuPolicyService policy, CancellationToken ct) => Results.Ok(await policy.GetAsync(ct)));

app.MapGet("/r/{code}", (string code, OtiumStore store, IOptions<ReferralOptions> options) =>
{
    var normalized = ReferralCodes.Normalize(code);
    var exists = ReferralCodes.IsWellFormed(normalized) && store.CodeExists(normalized);
    return Results.Content(LandingPage.Render(normalized, options.Value.DownloadUrl, exists), "text/html; charset=utf-8");
}).RequireRateLimiting("per-ip");

app.MapGet("/healthz", () => Results.Ok("ok"));

// Development only: read the emails that would have been sent (sign-in codes) when no SMTP server is configured.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("DevMode:Enabled"))
    app.MapGet("/dev/outbox", (OutboxEmailSender outbox) => Results.Ok(outbox.Sent.Reverse()));

// ---------- operator dashboard and API ----------

// The dashboard page and its assets hold no data (it asks for the token, then calls the API below), so they're served
// without the token, but only when an admin token is configured at all.
IResult AdminAsset(HttpContext context, string content, string contentType)
{
    if (context.RequestServices.GetRequiredService<IOptions<AdminOptions>>().Value.Token.Length == 0)
        return Results.NotFound();
    AdminDashboard.AddSecurityHeaders(context.Response);
    return Results.Text(content, contentType);
}

app.MapGet("/admin", (HttpContext context) => AdminAsset(context, AdminDashboard.Html, "text/html; charset=utf-8")).RequireRateLimiting("per-ip");
app.MapGet("/admin/dashboard.js", (HttpContext context) => AdminAsset(context, AdminDashboard.Script, "text/javascript; charset=utf-8")).RequireRateLimiting("per-ip");
app.MapGet("/admin/dashboard.css", (HttpContext context) => AdminAsset(context, AdminDashboard.Css, "text/css; charset=utf-8")).RequireRateLimiting("per-ip");

var admin = app.MapGroup("/admin").RequireRateLimiting("per-ip").AddEndpointFilter(async (context, next) =>
{
    var configured = context.HttpContext.RequestServices.GetRequiredService<IOptions<AdminOptions>>().Value.Token;
    var presented = context.HttpContext.Request.Headers["X-Admin-Token"].ToString();
    if (configured.Length == 0 || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(configured), Encoding.UTF8.GetBytes(presented)))
        return Results.Unauthorized();
    return await next(context);
});

admin.MapGet("/summary", async (OtiumStore store, IMoneroPoolApi pool, IBitcoinWallet treasury, IThorchainApi thorchain,
    CashoutMethodCatalog methods, SanctionsList sanctions, TimeProvider time, ILogger<Program> logger, CancellationToken ct) =>
{
    var totals = store.Totals(time.GetUtcNow());
    double poolEarnedXmr = 0, price = 0;
    string? poolError = null;
    try
    {
        foreach (var (address, _) in store.PoolWalletLoads())
        {
            if (await pool.GetMinerStatsAsync(address, ct) is { } stats)
                poolEarnedXmr += stats.PaidXmr + stats.PendingXmr;
        }
        price = (await pool.GetNetworkStatsAsync(ct)).PriceUsd;
    }
    catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException && !ct.IsCancellationRequested)
    {
        // The ledger totals are still worth showing while the pool API is down.
        logger.LogWarning(e, "Pool lookup for the admin summary failed");
        poolError = "the pool API didn't answer";
    }
    // The Bitcoin treasury funds every crypto payout (BTC directly, ETH/BNB/SOL through THORChain swaps).
    decimal? treasuryBtc = null, btcPrice = null;
    string? treasuryError = null;
    if (treasury.IsConfigured)
    {
        try
        {
            treasuryBtc = await treasury.GetBalanceAsync(ct);
            btcPrice = await thorchain.GetBtcPriceUsdAsync(ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or BitcoinWalletException or ThorchainException or System.Text.Json.JsonException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(e, "Treasury lookup for the admin summary failed");
            treasuryError = e.Message;
        }
    }
    var bandwidthTotals = store.BandwidthTotals();
    var kryptexMicros = store.CreditedWithReferencePrefix("kx:");
    var bandwidthMicros = store.CreditedWithReferencePrefix("bw:");
    return Results.Ok(new
    {
        creditedToUsersUsd = AccountService.ToUsd(totals.Credited),
        // Earnings credited from MoneroOcean mining only, to compare with poolEarnedUsdAtTodaysPrice.
        moneroOceanCreditedToUsersUsd = AccountService.ToUsd(totals.Credited - kryptexMicros - bandwidthMicros),
        referralRewardsUsd = AccountService.ToUsd(totals.Referral),
        paidOutUsd = AccountService.ToUsd(totals.Paid),
        owedToUsersUsd = AccountService.ToUsd(totals.Liability),
        // Already debited from balances but not paid yet (queued, in review or being sent): still owed.
        inOpenCashoutsUsd = AccountService.ToUsd(store.OpenCashoutMicros()),
        treasuryConfigured = treasury.IsConfigured,
        treasuryBtc,
        treasuryUsd = treasuryBtc is { } t && btcPrice is { } p ? Math.Round(t * p, 2) : (decimal?)null,
        treasuryError,
        payoutMethods = methods.Current.Select(m => new { m.Method, m.Name, m.MinUsd, available = m.Available, m.UnavailableReason }),
        sanctionedAddressesLoaded = sanctions.Count,
        poolEarnedXmr,
        poolEarnedUsdAtTodaysPrice = Math.Round(poolEarnedXmr * price, 2),
        poolError,
        reviewQueue = store.CashoutsWithStatus(CashoutStatuses.Review).Count,
        poolAddresses = store.PoolWalletLoads().Select(w => new { w.Address, w.Accounts }),
        // Compare with the bandwidth partner's publisher statements.
        bandwidthCreditedGb = Math.Round(bandwidthTotals.Bytes / BandwidthService.BytesPerGb, 3),
        bandwidthCreditedToUsersUsd = AccountService.ToUsd(bandwidthTotals.Micros),
        // GPU coins mined on Kryptex (compare with Kryptex's paid + unpaid totals for the operator account).
        kryptexCreditedToUsersUsd = AccountService.ToUsd(kryptexMicros),
    });
});

// Each cash-out with what a reviewer needs to decide: who the account is and whether the destination is shared.
admin.MapGet("/cashouts", (string? status, OtiumStore store) =>
    Results.Ok(store.CashoutsWithStatus(string.IsNullOrEmpty(status) ? CashoutStatuses.Review : status).Select(c =>
    {
        var account = store.FindAccount(c.AccountId);
        return new
        {
            c.Id, c.AccountId, c.Method, c.Destination, c.Micros, c.Status, c.CreatedAt, c.UpdatedAt, c.ProviderReference, c.Note, c.Attempts,
            amountUsd = AccountService.ToUsd(c.Micros),
            accountEmail = account?.Email,
            accountCreatedAt = account?.CreatedAt,
            lifetimeEarnedUsd = AccountService.ToUsd(store.LifetimeEarningMicros(c.AccountId)),
            paidOutUsd = AccountService.ToUsd(store.PaidOutMicros(c.AccountId)),
            otherAccountsUsingDestination = store.OtherAccountsUsingDestination(c.Destination, c.AccountId),
        };
    })));

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

admin.MapPost("/cashouts/{id}/mark-paid", async (string id, AdminNote body, OtiumStore store, CashoutReceipts receipts, TimeProvider time, CancellationToken ct) =>
{
    var cashout = store.FindCashout(id);
    if (cashout is null || cashout.Status is CashoutStatuses.Paid or CashoutStatuses.Failed)
        return Results.NotFound();
    var now = time.GetUtcNow();
    store.UpdateCashout(id, CashoutStatuses.Paid, now, providerReference: body.Note ?? "manual");
    await receipts.SendPaidAsync(store.FindCashout(id)!, now, ct);
    return Results.Ok();
});

app.Run();

public sealed record AdminNote(string? Note);

public partial class Program;

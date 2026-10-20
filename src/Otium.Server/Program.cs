using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
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
using Otium.Server.RealEarnings;

var builder = WebApplication.CreateBuilder(args);
// No "Server: Kestrel" header: it only tells scanners what to look for.
// No request needs more than 1 MB (the largest, a problem report, is at most 300 000 characters).
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.AddServerHeader = false;
    kestrel.Limits.MaxRequestBodySize = 1024 * 1024;
});

// Non-custodial unless the operator turns custody on: see CustodyOptions.
var custodial = builder.Configuration.GetValue<bool>($"{CustodyOptions.Section}:Enabled");
builder.Services.Configure<CustodyOptions>(builder.Configuration.GetSection(CustodyOptions.Section));
builder.Services.Configure<UnmineableOptions>(builder.Configuration.GetSection(UnmineableOptions.Section));
builder.Services.Configure<MysteriumOptions>(builder.Configuration.GetSection(MysteriumOptions.Section));
builder.Services.Configure<ProxybaseOptions>(builder.Configuration.GetSection(ProxybaseOptions.Section));
builder.Services.Configure<FeeOptions>(builder.Configuration.GetSection(FeeOptions.Section));

builder.Services.Configure<ReferralOptions>(builder.Configuration.GetSection(ReferralOptions.Section));
builder.Services.Configure<SiteOptions>(builder.Configuration.GetSection(SiteOptions.Section));
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
builder.Services.Configure<CpuMiningOptions>(builder.Configuration.GetSection(CpuMiningOptions.Section));

builder.Services.AddSingleton(TimeProvider.System);
// Every outgoing request says who it is: some APIs (CoinGecko among them) refuse requests without a User-Agent.
builder.Services.ConfigureHttpClientDefaults(http => http.ConfigureHttpClient(client =>
    client.DefaultRequestHeaders.UserAgent.ParseAdd($"Otium-Server/{typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0"} (+https://otium.lol)")));
builder.Services.AddSingleton(_ => new OtiumStore(builder.Configuration.GetConnectionString("Otium") ?? "Data Source=otium.db"));
builder.Services.AddSingleton<InviteLinks>();
builder.Services.AddSingleton<ConnectionQuotas>();
builder.Services.AddSingleton<ConnectionHashes>();
builder.Services.AddSingleton<ReferralRedemptions>();
builder.Services.AddHttpClient<IMoneroPoolApi, MoneroOceanClient>();
builder.Services.AddHttpClient<IUnmineableApi, UnmineableClient>();
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
builder.Services.AddSingleton<IMystWallet, PolygonMystWallet>();
builder.Services.AddSingleton<MystSponsorService>();
builder.Services.AddSingleton<AccountService>();
builder.Services.AddSingleton<AccountDataService>();
builder.Services.AddSingleton<EarningsCreditor>();
builder.Services.AddHttpClient<IProfitFeed, WhatToMineClient>();
builder.Services.AddHttpClient<IKryptexApi, KryptexClient>();
builder.Services.AddHttpClient<IPearlPipFeed, PearlPipClient>();
builder.Services.AddSingleton<PearlForkWatcher>();
builder.Services.AddSingleton<ReleaseDownloads>();
builder.Services.AddHttpClient<IReleaseList, GitHubReleaseList>();
builder.Services.AddSingleton(new UpdateSigningKeys(Otium.Core.AppIdentity.UpdateSigningKeys));
builder.Services.AddSingleton<UpdateFeed>();
builder.Services.AddSingleton<GpuPolicyService>();
builder.Services.AddSingleton<KryptexCreditor>();
builder.Services.AddSingleton<CpuPoolRouter>();
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
// Internet sharing payouts: a one-time conversion per payout from Proxybase's Tempo USDC to the user's own coin.
builder.Services.Configure<Otium.Server.Sharing.SharingOptions>(builder.Configuration.GetSection(Otium.Server.Sharing.SharingOptions.Section));
builder.Services.AddHttpClient<Otium.Server.Sharing.IRelayApi, Otium.Server.Sharing.RelayClient>();
builder.Services.AddHttpClient<Otium.Server.Sharing.SideShiftLitecoin>();
builder.Services.AddHttpClient<Otium.Server.Sharing.ChangeNowMonero>();
builder.Services.AddSingleton<Otium.Server.Sharing.SharingRouteService>();
// Otium's sharing fee is paid by the app on Tempo; the server reads Tempo to see that it arrived.
builder.Services.AddHttpClient<Otium.Server.Sharing.ITempoTransfers, Otium.Server.Sharing.TempoRpcClient>();
builder.Services.AddSingleton<Otium.Server.Sharing.SharingFees>();
if (!builder.Configuration.GetValue<bool>("DisableBackgroundJobs"))
    builder.Services.AddHostedService<Otium.Server.Sharing.SharingFeeWatcher>();

// The website's real numbers: opt-in earnings reports, and what the pools paid users' own addresses.
builder.Services.Configure<RealEarningsOptions>(builder.Configuration.GetSection(RealEarningsOptions.Section));
builder.Services.AddSingleton<EarningsReportService>();
builder.Services.AddSingleton<Otium.Server.Support.ProblemReportService>();
builder.Services.AddHttpClient<ICoinPrices, CoinGeckoPrices>();
builder.Services.AddSingleton<PoolPayoutChecker>();
builder.Services.AddSingleton<FeeSliceWatcher>();
builder.Services.AddSingleton<RealEarningsService>();
if (!builder.Configuration.GetValue<bool>("DisableBackgroundJobs"))
{
    builder.Services.AddHttpClient<IndexNowService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<IndexNowService>());
}
if (!builder.Configuration.GetValue<bool>("DisableBackgroundJobs") && builder.Configuration.GetValue($"{RealEarningsOptions.Section}:CheckPools", true))
{
    builder.Services.AddHostedService<PoolPayoutService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<FeeSliceWatcher>());
}

builder.Services.AddSingleton<CashoutMethodCatalog>();
builder.Services.AddSingleton<CashoutService>();
builder.Services.AddSingleton<CashoutReceipts>();
builder.Services.AddSingleton<CashoutProcessor>();
// Crediting, cash-outs and sanctions lists only exist for balances, so a non-custodial server runs none of them.
if (custodial && !builder.Configuration.GetValue<bool>("DisableBackgroundJobs"))
{
    builder.Services.AddHostedService<EarningsCreditorService>();
    if (builder.Configuration.GetValue<bool>("Kryptex:Enabled"))
        builder.Services.AddHostedService<KryptexCreditorService>();
    builder.Services.AddHostedService<CashoutProcessorService>();
    builder.Services.AddHttpClient<SanctionsRefresher>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<SanctionsRefresher>());
}

// Behind a reverse proxy, client addresses come from X-Forwarded-For, trusted only on connections from the proxy.
var reverseProxy = builder.Configuration.GetSection(ReverseProxyOptions.Section).Get<ReverseProxyOptions>() ?? new();
if (reverseProxy.Enabled)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        if (reverseProxy.KnownProxies.Length > 0 || reverseProxy.KnownNetworks.Length > 0)
        {
            // Replace the localhost defaults with exactly the configured proxy.
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();
        }
        foreach (var proxy in reverseProxy.KnownProxies)
            options.KnownProxies.Add(IPAddress.Parse(proxy));
        foreach (var network in reverseProxy.KnownNetworks)
            options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
    });
}

builder.Services.AddProblemDetails();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Per internet connection: IPv6 by its /64, which a single home or phone can pick addresses from at will.
    options.AddPolicy("per-ip", context => RateLimitPartition.GetFixedWindowLimiter(
        ClientNetwork.Of(context.Connection.RemoteIpAddress) ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

// HEAD gets the GET answer's status and headers without its body (RFC 9110), so crawlers and link checkers that ask
// with HEAD don't get 405 for every page. It must run before routing picks an endpoint, hence UseRouting below.
app.Use(async (context, next) =>
{
    if (!HttpMethods.IsHead(context.Request.Method))
    {
        await next();
        return;
    }
    context.Request.Method = HttpMethods.Get;
    var body = context.Response.Body;
    context.Response.Body = Stream.Null;
    try
    {
        await next();
    }
    finally
    {
        context.Response.Body = body;
        context.Request.Method = HttpMethods.Head;
    }
});
if (reverseProxy.Enabled)
    app.UseForwardedHeaders();
app.UseRouting();
app.UseRateLimiter();

// Browser hardening for every response (from a website scan): no MIME sniffing, no framing, no referrer beyond the
// site's own origin, no device APIs, and pages may only load the site's own files: no inline scripts and no inline
// styles (bar heights and widths travel as data-* attributes that site.js applies). The per-response nonce is for Cloudflare: its bot detection
// injects an inline script, and Cloudflare stamps that script with the nonce it reads from this header, so it runs
// without allowing inline scripts in general.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    var nonce = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
    headers["X-Content-Type-Options"] = "nosniff";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["X-Frame-Options"] = "DENY";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
    headers["Content-Security-Policy"] = $"default-src 'self'; script-src 'self' 'nonce-{nonce}'; style-src 'self'; img-src 'self' data:; "
        + "font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
    await next();
});

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

// A page that doesn't exist gets a page with the way back in, for people and crawlers following an old or mistyped
// link. Only for pages: the API, update feed, admin and site files keep their bare 404s.
app.UseStatusCodePages(async context =>
{
    var http = context.HttpContext;
    var path = http.Request.Path.Value ?? "/";
    if (http.Response.StatusCode != StatusCodes.Status404NotFound || !HttpMethods.IsGet(http.Request.Method)
        || !http.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase)
        || new[] { "/api/", "/updates/", "/admin", "/site/", "/dev/" }.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        return;
    var site = http.RequestServices.GetRequiredService<IOptions<SiteOptions>>().Value;
    http.Response.ContentType = "text/html; charset=utf-8";
    await http.Response.WriteAsync(SitePages.NotFoundPage(SitePages.AvailableLegalPages(site.PagesDirectory),
        PublicBase(http.Request, http.RequestServices.GetRequiredService<IOptions<ReferralOptions>>()), path));
});

string PublicBase(HttpRequest request, IOptions<ReferralOptions> options) =>
    options.Value.PublicBaseUrl ?? $"{request.Scheme}://{request.Host}";

string Secret(HttpRequest request) => request.Headers[AccountClient.SecretHeader].ToString();

// Countries Otium doesn't run in (sanctions, mining bans), by Cloudflare's visitor country. Exporting and deleting an
// account stay open everywhere.
void CheckCountry(HttpRequest request)
{
    if (Otium.Core.Availability.BlockedReason(request.Headers["CF-IPCountry"].ToString()) is { } reason)
        throw new OtiumException(StatusCodes.Status451UnavailableForLegalReasons, reason);
}

// ---------- app API ----------

var api = app.MapGroup("/api/v1").RequireRateLimiting("per-ip");

api.MapPost("/devices", async (RegisterDeviceRequest body, HttpRequest request, AccountService accounts, OtiumStore store, ConnectionQuotas quotas,
    IOptions<ReferralOptions> options, CancellationToken ct) =>
{
    CheckCountry(request);
    // New computers only: a known one re-registers (e.g. a new payout address) as often as it likes.
    if (store.FindDevice(body.DeviceId ?? "") is null
        && !quotas.TryTake("new-device", request.HttpContext.Connection.RemoteIpAddress, ConnectionQuotas.NewDevicesPerDay, TimeSpan.FromDays(1)))
        throw new OtiumException(StatusCodes.Status429TooManyRequests, "Too many new installs from this internet connection today. Please try again tomorrow.");
    return Results.Ok(await accounts.RegisterAsync(body, PublicBase(request, options), ct, request.HttpContext.Connection.RemoteIpAddress));
});

api.MapGet("/devices/{deviceId}/account", async (string deviceId, HttpRequest request, AccountService accounts, IOptions<ReferralOptions> options, CancellationToken ct) =>
{
    CheckCountry(request);
    // Apps since the fee grant's mining floors also say how long they've mined (seconds); older apps send nothing.
    return Results.Ok(await accounts.GetSummaryAsync(deviceId, Secret(request), PublicBase(request, options), ct, request.HttpContext.Connection.RemoteIpAddress,
        Seconds(request.Query[AccountClient.CpuMinedParameter]), Seconds(request.Query[AccountClient.GpuMinedParameter])));
});

static double? Seconds(string? value) =>
    double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s)
    && double.IsFinite(s) && s >= 0 ? s : null;

api.MapGet("/devices/{deviceId}/invites/leaderboard", async (string deviceId, HttpRequest request, AccountService accounts, CancellationToken ct) =>
    Results.Ok(await accounts.GetLeaderboardAsync(deviceId, Secret(request), ct)));

// Data-protection rights: a copy of everything stored about the account, and deleting it (money records stay, anonymized).
api.MapGet("/devices/{deviceId}/account/export", (string deviceId, HttpContext context, AccountDataService data) =>
{
    var export = data.Export(deviceId, Secret(context.Request));
    context.Response.Headers.CacheControl = "no-store";
    return Results.File(AccountDataService.Serialize(export), "application/json", $"otium-data-{export.ExportedAt.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)}.json");
});

api.MapPost("/devices/{deviceId}/account/delete", (string deviceId, DeleteAccountRequest body, HttpRequest request, AccountDataService data) =>
    Results.Ok(data.Delete(deviceId, Secret(request), body)));

if (custodial)
{
    api.MapPost("/devices/{deviceId}/cashouts", (string deviceId, CashoutRequest body, HttpRequest request, AccountService accounts, CashoutService cashouts) =>
    {
        var session = accounts.Authenticate(deviceId, Secret(request));
        return Results.Ok(cashouts.Request(session.Account, body));
    });

    // Bandwidth usage reports. The connection's public IP enforces one paid sharer per internet connection; when
    // deployed behind a reverse proxy, set ReverseProxy:Enabled so this is the client's address, not the proxy's.
    api.MapPost("/devices/{deviceId}/bandwidth", (string deviceId, BandwidthReport body, HttpContext context, AccountService accounts, BandwidthService bandwidth) =>
    {
        var session = accounts.Authenticate(deviceId, Secret(context.Request));
        var ip = context.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "unknown";
        return Results.Ok(bandwidth.Report(session, body, ip));
    });
}

// Where to have Proxybase pay a user's sharing earnings so they arrive in the user's own coin (never via Otium).
api.MapPost("/devices/{deviceId}/sharing/route", async (string deviceId, SharingRouteRequest body, HttpContext context, AccountService accounts,
    Otium.Server.Sharing.SharingRouteService routes, CancellationToken ct) =>
{
    CheckCountry(context.Request);
    var session = accounts.Authenticate(deviceId, Secret(context.Request));
    // The client's own address (IPv6 kept as it is): SideShift and ChangeNOW want the user's, and it keys the daily quota.
    return Results.Ok(await routes.RouteAsync(session, body, context.Connection.RemoteIpAddress, ct));
});

// Problem reports users send from the app ("Send a problem report"), answered with a reference to quote.
api.MapPost("/devices/{deviceId}/problem-report", (string deviceId, ProblemReport body, HttpRequest request, AccountService accounts,
    Otium.Server.Support.ProblemReportService reports, ConnectionQuotas quotas) =>
{
    var session = accounts.Authenticate(deviceId, Secret(request));
    reports.CheckAllowance(session);
    if (!quotas.TryTake("problem-report", request.HttpContext.Connection.RemoteIpAddress, ConnectionQuotas.ProblemReportsPerDay, TimeSpan.FromDays(1)))
        throw new OtiumException(StatusCodes.Status429TooManyRequests, "Several problem reports already came from this internet connection today. Please try again tomorrow.");
    return Results.Ok(reports.Report(session, body));
});

// A referred friend's app: the share of Otium's fee it has mined for the inviter so far, which the inviter sees.
api.MapPost("/devices/{deviceId}/referral-share", (string deviceId, ReferralShareReport body, HttpRequest request, AccountService accounts) =>
{
    accounts.ReportReferralShare(accounts.Authenticate(deviceId, Secret(request)), body);
    return Results.NoContent();
});

// Opt-in earnings reports for the website's real-earnings table (custodial or not).
api.MapPost("/devices/{deviceId}/earnings", (string deviceId, EarningsReport body, HttpRequest request, AccountService accounts, EarningsReportService reports) =>
{
    reports.Report(accounts.Authenticate(deviceId, Secret(request)), body);
    return Results.NoContent();
});

// The user turned "Share analytics" off: what this computer shared is deleted.
api.MapPost("/devices/{deviceId}/earnings/delete", (string deviceId, HttpRequest request, AccountService accounts, OtiumStore store) =>
{
    var session = accounts.Authenticate(deviceId, Secret(request));
    store.DeleteEarningsReports(session.Device.DeviceId);
    return Results.NoContent();
});

// Mysterium internet sharing: Otium pays a new node's one-time registration fee (custodial or not; it's Otium's money).
api.MapPost("/devices/{deviceId}/mysterium/sponsor", async (string deviceId, MystSponsorRequest body, HttpContext context, AccountService accounts,
    MystSponsorService sponsor, CancellationToken ct) =>
{
    var session = accounts.Authenticate(deviceId, Secret(context.Request));
    var ip = context.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "unknown";
    return Results.Ok(await sponsor.SponsorAsync(session, body, ip, ct));
});

api.MapPost("/devices/{deviceId}/email/code", async (string deviceId, EmailCodeRequest body, HttpRequest request, AccountService accounts,
    ConnectionQuotas quotas, CancellationToken ct) =>
{
    // Per connection too, since devices are free: otherwise anyone could have Otium email codes to any address.
    var from = request.HttpContext.Connection.RemoteIpAddress;
    if (!quotas.TryTake("code-email-hour", from, ConnectionQuotas.CodeEmailsPerHour, TimeSpan.FromHours(1))
        || !quotas.TryTake("code-email-day", from, ConnectionQuotas.CodeEmailsPerDay, TimeSpan.FromDays(1)))
        throw new OtiumException(StatusCodes.Status429TooManyRequests, "Too many codes requested from this internet connection. Please wait a while and try again.");
    await accounts.SendSignInCodeAsync(deviceId, Secret(request), body, ct);
    return Results.NoContent();
});

api.MapPost("/devices/{deviceId}/email/verify", (string deviceId, EmailVerifyRequest body, HttpRequest request, AccountService accounts, IOptions<ReferralOptions> options) =>
    Results.Ok(accounts.VerifySignInCode(deviceId, Secret(request), body, PublicBase(request, options))));

// The invite link opened on the caller's internet connection in the last week, if any: Otium asks on its first start,
// so a friend who downloaded it from the link doesn't have to type the code (see InviteLinks).
api.MapGet("/invite-link", (HttpContext context, InviteLinks links) =>
    links.PendingFor(context.Connection.RemoteIpAddress) is { } code ? Results.Ok(new InviteLinkResponse(code)) : Results.NoContent());

// Which GPU algorithms to mine and where; the same for every app, rebuilt from live feeds every few minutes.
api.MapGet("/gpu-policy", async (GpuPolicyService policy, CancellationToken ct) => Results.Ok(await policy.GetAsync(ct)));

app.MapGet("/r/{code}", (string code, HttpRequest request, OtiumStore store, InviteLinks links, IOptions<ReferralOptions> options, IOptions<SiteOptions> site) =>
{
    var normalized = ReferralCodes.Normalize(code);
    var exists = ReferralCodes.IsWellFormed(normalized) && store.CodeExists(normalized);
    if (exists && InviteLinks.IsPageVisit(request.Headers))
        links.Clicked(request.HttpContext.Connection.RemoteIpAddress, normalized); // so Otium installed on this connection fills it in
    return Results.Content(LandingPage.Render(normalized, "/download", exists, PublicBase(request, options),
            SitePages.AvailableLegalPages(site.Value.PagesDirectory)),
        "text/html; charset=utf-8");
}).RequireRateLimiting("per-ip");

// The public website: the front page (with today's per-card earnings from the GPU policy), what each graphics card
// earns, the operator's legal pages once their reviewed files are in place, and the site's own embedded files.

// Today's GPU prices for the website, or null when they can't be read (the pages then say so instead of showing numbers).
async Task<Otium.Core.Switching.GpuPolicy?> SiteGpuPolicy(GpuPolicyService gpuPolicy, ILogger logger, CancellationToken ct)
{
    try
    {
        return await gpuPolicy.GetAsync(ct);
    }
    catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException && !ct.IsCancellationRequested)
    {
        logger.LogWarning(e, "GPU policy unavailable for the website");
        return null;
    }
}

app.MapGet("/", async (HttpRequest request, IOptions<FeeOptions> fees, IOptions<SiteOptions> site, IOptions<ReferralOptions> referral,
    GpuPolicyService gpuPolicy, RealEarningsService realEarnings, ILogger<Program> logger, CancellationToken ct) =>
{
    var policy = Otium.Core.Fees.FeePolicy.FromTerms(fees.Value.ToTerms());
    var gpu = await SiteGpuPolicy(gpuPolicy, logger, ct);
    var html = SitePages.Home(policy, "/download", SitePages.AvailableLegalPages(site.Value.PagesDirectory), PublicBase(request, referral),
        SitePages.Earnings(gpu, policy), gpu?.GeneratedAt,
        // Cloudflare's visitor country, for the local electricity price. Only Cloudflare reaches the server in production
        // (Caddyfile.cloudflare turns everyone else away); anyone sending it themselves only changes their own estimate.
        request.Headers["CF-IPCountry"].ToString(),
        realEarnings.Get());
    return Results.Content(html, "text/html; charset=utf-8");
}).RequireRateLimiting("per-ip");

// What each graphics card earns mining: one page comparing them all, and one per card (/mining/rtx-4090; a card's id,
// /mining/rtx4090, moves to its page for good).
app.MapGet("/mining", async (HttpRequest request, IOptions<FeeOptions> fees, IOptions<SiteOptions> site, IOptions<ReferralOptions> referral,
    GpuPolicyService gpuPolicy, ILogger<Program> logger, CancellationToken ct) =>
{
    var policy = Otium.Core.Fees.FeePolicy.FromTerms(fees.Value.ToTerms());
    var gpu = await SiteGpuPolicy(gpuPolicy, logger, ct);
    return Results.Content(SitePages.MiningIndex(policy, SitePages.Earnings(gpu, policy), gpu?.GeneratedAt,
        SitePages.AvailableLegalPages(site.Value.PagesDirectory), PublicBase(request, referral)), "text/html; charset=utf-8");
}).RequireRateLimiting("per-ip");
app.MapGet("/mining/{card}", async (string card, HttpRequest request, IOptions<FeeOptions> fees, IOptions<SiteOptions> site,
    IOptions<ReferralOptions> referral, GpuPolicyService gpuPolicy, ILogger<Program> logger, CancellationToken ct) =>
{
    if (SitePages.MiningSlug(card) is not { } slug)
        return Results.NotFound();
    if (slug != card)
        return Results.Redirect($"/mining/{slug}", permanent: true);
    var policy = Otium.Core.Fees.FeePolicy.FromTerms(fees.Value.ToTerms());
    var gpu = await SiteGpuPolicy(gpuPolicy, logger, ct);
    return Results.Content(SitePages.MiningCard(slug, policy, SitePages.Earnings(gpu, policy), gpu?.GeneratedAt,
        request.Headers["CF-IPCountry"].ToString(), SitePages.AvailableLegalPages(site.Value.PagesDirectory), PublicBase(request, referral)),
        "text/html; charset=utf-8");
}).RequireRateLimiting("per-ip");

// What Pearl is, help with antivirus warnings, and Otium next to the apps people compare it with.
app.MapGet("/pearl", async (HttpRequest request, IOptions<FeeOptions> fees, IOptions<SiteOptions> site, IOptions<ReferralOptions> referral,
    GpuPolicyService gpuPolicy, ILogger<Program> logger, CancellationToken ct) =>
{
    var policy = Otium.Core.Fees.FeePolicy.FromTerms(fees.Value.ToTerms());
    var gpu = await SiteGpuPolicy(gpuPolicy, logger, ct);
    return Results.Content(SitePages.Pearl(SitePages.Earnings(gpu, policy), SitePages.AvailableLegalPages(site.Value.PagesDirectory),
        PublicBase(request, referral)), "text/html; charset=utf-8");
}).RequireRateLimiting("per-ip");
app.MapGet("/antivirus", (HttpRequest request, IOptions<SiteOptions> site, IOptions<ReferralOptions> referral) =>
    Results.Content(SitePages.Antivirus(SitePages.AvailableLegalPages(site.Value.PagesDirectory), PublicBase(request, referral)),
        "text/html; charset=utf-8")).RequireRateLimiting("per-ip");
app.MapGet("/compare/{other}", (string other, HttpRequest request, IOptions<FeeOptions> fees, IOptions<SiteOptions> site,
    IOptions<ReferralOptions> referral) =>
    SitePages.ComparisonSlugs.Contains(other)
        ? Results.Content(SitePages.Compare(other, Otium.Core.Fees.FeePolicy.FromTerms(fees.Value.ToTerms()),
            SitePages.AvailableLegalPages(site.Value.PagesDirectory), PublicBase(request, referral)), "text/html; charset=utf-8")
        : Results.NotFound()).RequireRateLimiting("per-ip");

app.MapGet("/{page:regex(^(privacy|terms)$)}", (string page, HttpRequest request, IOptions<SiteOptions> site, IOptions<ReferralOptions> referral) =>
{
    var directory = site.Value.PagesDirectory;
    var file = Path.Combine(directory, $"{page}.html");
    return File.Exists(file)
        ? Results.Content(SitePages.Legal(page, File.ReadAllText(file), SitePages.AvailableLegalPages(directory), "/download", PublicBase(request, referral)),
            "text/html; charset=utf-8")
        : Results.NotFound();
}).RequireRateLimiting("per-ip");

// For search engines: what not to crawl, the pages worth finding (with when today's prices last changed the priced
// ones), and IndexNow's key file, which proves the site's own server sent IndexNowService's notices.
app.MapGet("/robots.txt", (HttpRequest request, IOptions<ReferralOptions> referral) =>
    Results.Text(SitePages.Robots(PublicBase(request, referral)), "text/plain; charset=utf-8")).RequireRateLimiting("per-ip");
app.MapGet("/sitemap.xml", async (HttpRequest request, IOptions<ReferralOptions> referral, IOptions<SiteOptions> site,
    GpuPolicyService gpuPolicy, ILogger<Program> logger, CancellationToken ct) =>
    Results.Text(SitePages.Sitemap(PublicBase(request, referral), SitePages.AvailableLegalPages(site.Value.PagesDirectory),
        (await SiteGpuPolicy(gpuPolicy, logger, ct))?.GeneratedAt), "application/xml; charset=utf-8")).RequireRateLimiting("per-ip");
app.MapGet($"/{IndexNowService.Key}.txt", () => Results.Text(IndexNowService.Key, "text/plain; charset=utf-8"));

app.MapGet("/site/{**path}", (string path, HttpContext context) =>
{
    if (SiteAssets.Find(path) is not { } asset)
        return Results.NotFound();
    // Versioned links (?v=<hash>) never change content, so they can be cached for a year; the fonts' plain links for a day.
    context.Response.Headers.CacheControl = context.Request.Query.ContainsKey("v") ? "public, max-age=31536000, immutable" : "public, max-age=86400";
    return Results.Bytes(asset.Content, asset.ContentType);
});
app.MapGet("/favicon.ico", () => Results.Redirect(SiteAssets.Url("otium.svg")));

// RFC 9116: where to report security problems (GitHub's private vulnerability reporting, so no address is published).
app.MapGet("/.well-known/security.txt", (HttpRequest request, IOptions<ReferralOptions> referral, TimeProvider time) =>
    Results.Text(
        "Contact: https://github.com/Malogna/otium/security/advisories/new\n"
        + $"Expires: {time.GetUtcNow().AddDays(180).ToString("yyyy-MM-dd'T'00:00:00'Z'", System.Globalization.CultureInfo.InvariantCulture)}\n"
        + "Preferred-Languages: en\n"
        + $"Canonical: {PublicBase(request, referral)}/.well-known/security.txt\n",
        "text/plain; charset=utf-8"));

// Every Download button: otium.lol's own page for the latest release, which starts the installer for the visitor's
// system by itself (or ?os=windows|macos|macos-intel|linux) and lists the others; phones are asked to open it on their
// computer. GitHub's releases page only while no release can be read.
app.MapGet("/download", async (string? os, HttpRequest request, ReleaseDownloads downloads, IOptions<ReferralOptions> referral,
    IOptions<SiteOptions> site, CancellationToken ct) =>
{
    if (await downloads.LatestAsync(ct) is not { } latest)
        return Results.Redirect(referral.Value.DownloadUrl);
    var system = os ?? ReleaseDownloads.DetectOs(request.Headers.UserAgent);
    return Results.Content(SitePages.Download(latest, system, SitePages.AvailableLegalPages(site.Value.PagesDirectory), PublicBase(request, referral)),
        "text/html; charset=utf-8");
}).RequireRateLimiting("per-ip");

// One system's installer of the latest release, as a direct link (the download page's buttons), or the page itself.
app.MapGet("/download/{os}", async (string os, ReleaseDownloads downloads, CancellationToken ct) =>
    Results.Redirect(await downloads.InstallerUrlAsync(os, ct) ?? "/download")).RequireRateLimiting("per-ip");

// The app's update feed (see UpdateFeed): "pre" includes release candidates, "stable" only releases.
app.MapGet("/updates/{track}/{file}", async (string track, string file, UpdateFeed feed, CancellationToken ct) =>
{
    if (track is not ("pre" or "stable"))
        return Results.NotFound();
    // Only signed feeds, byte for byte (the signature covers the exact bytes), and their signatures from the same release.
    if (UpdateFeed.IsFeed(file))
        return await feed.SignedFeedAsync(file, track == "pre", ct) is { } signed ? Results.Bytes(signed.Feed, "application/json") : Results.NotFound();
    if (UpdateFeed.IsSignature(file))
        return await feed.SignedFeedAsync(file[..^".sig".Length], track == "pre", ct) is { } signed
            ? Results.Text(signed.Signature, "text/plain; charset=utf-8")
            : Results.NotFound();
    return await feed.DownloadUrlAsync(file, ct) is { } url ? Results.Redirect(url) : Results.NotFound();
}).RequireRateLimiting("per-ip");

app.MapGet("/healthz", () => Results.Ok("ok"));

// Development only: read the emails that would have been sent (sign-in codes) when no SMTP server is configured.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("DevMode:Enabled"))
{
    app.MapGet("/dev/outbox", (OutboxEmailSender outbox) => Results.Ok(outbox.Sent.Reverse()));
    // The pages tools/render-site-previews.ps1 screenshots into the mining pages' link-preview pictures.
    app.MapGet("/dev/preview/mining", () => Results.Content(SitePages.PreviewTemplate(null)!, "text/html; charset=utf-8"));
    app.MapGet("/dev/preview/mining/{card}", (string card) =>
        SitePages.PreviewTemplate(card) is { } html ? Results.Content(html, "text/html; charset=utf-8") : Results.NotFound());
}

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

if (!custodial)
    admin.MapGet("/summary", (OtiumStore store, IUnmineableApi unmineable, IOptions<UnmineableOptions> options, IMystWallet myst,
            MystSponsorService sponsor, PearlForkWatcher pearlFork, TimeProvider time, ILogger<Program> logger, CancellationToken ct) =>
        NonCustodialAdmin.SummaryAsync(store, unmineable, options.Value, myst, sponsor.IsAvailable, time, logger, ct, pearlFork));
else
{
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
}

// Problem reports: the newest first, and one in full by the reference the user quotes.
admin.MapGet("/problem-reports", (OtiumStore store) => Results.Ok(store.ProblemReports()));
admin.MapGet("/problem-reports/{reference}", (string reference, OtiumStore store) =>
    store.FindProblemReport(reference.Trim().ToUpperInvariant()) is { } report ? Results.Ok(report) : Results.NotFound());

if (custodial)
{
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
}

app.Run();

public sealed record AdminNote(string? Note);

public partial class Program;

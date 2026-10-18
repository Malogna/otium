using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Otium.Server.Gpu;

/// <summary>Where Pearl's FP8 fork stands.</summary>
/// <param name="ActivationHeight">The fork's height, from the operator or PIP-3; null while none is known.</param>
/// <param name="Height">Pearl's chain height when last checked, or null if it couldn't be read.</param>
public sealed record PearlForkState(bool Active, long? ActivationHeight, long? Height, string Reason);

public interface IPearlPipFeed
{
    /// <summary>PIP-3's text (Markdown), or null if it can't be read.</summary>
    Task<string?> GetAsync(CancellationToken ct);
}

public sealed class PearlPipClient(HttpClient http, IOptions<GpuPolicyOptions> options) : IPearlPipFeed
{
    public async Task<string?> GetAsync(CancellationToken ct)
    {
        using var response = await http.GetAsync(options.Value.PearlFork.PipUrl, ct);
        return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(ct) : null;
    }
}

/// <summary>
/// Watches for Pearl's FP8 fork (PIP-3), which only Hopper and Blackwell cards survive. In "auto" mode it reads the
/// activation height from the operator's setting or, failing that, from PIP-3 itself (a front-matter
/// "activation-height:" line, or "activation height … 123456" in the text), and Pearl's chain height from Kryptex Pool.
/// Once the chain reaches the height, <see cref="GpuPolicyService"/> limits pearlhash to the supported cards.
/// Results are cached for <see cref="PearlForkOptions.CheckInterval"/>; a failed check keeps the last known state.
/// </summary>
public sealed partial class PearlForkWatcher(
    IKryptexApi kryptex,
    IPearlPipFeed pip,
    IOptions<GpuPolicyOptions> options,
    TimeProvider time,
    ILogger<PearlForkWatcher> logger)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private PearlForkState? _state;
    private DateTimeOffset _checkedAt;

    public async Task<PearlForkState> GetAsync(CancellationToken ct)
    {
        var fork = options.Value.PearlFork;
        switch (fork.Mode.Trim().ToLowerInvariant())
        {
            case "off":
                return new PearlForkState(false, fork.ActivationHeight, null, "Pearl fork watching is off");
            case "active":
                return new PearlForkState(true, fork.ActivationHeight, null, "Set as active by Otium");
        }

        var now = time.GetUtcNow();
        if (_state is { } cached && now - _checkedAt < fork.CheckInterval)
            return cached;

        await _lock.WaitAsync(ct);
        try
        {
            if (_state is { } again && now - _checkedAt < fork.CheckInterval)
                return again;
            _state = await CheckAsync(fork, ct);
            _checkedAt = now;
            return _state;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<PearlForkState> CheckAsync(PearlForkOptions fork, CancellationToken ct)
    {
        var activation = fork.ActivationHeight;
        if (activation is null)
        {
            try
            {
                activation = ActivationHeightIn(await pip.GetAsync(ct));
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                logger.LogWarning(e, "Couldn't read PIP-3; keeping the last known Pearl fork state");
                return _state ?? new PearlForkState(false, null, null, "PIP-3 couldn't be read");
            }
        }
        if (activation is null)
            return new PearlForkState(false, null, null, "PIP-3 has no activation height yet");

        long? height;
        try
        {
            height = (await kryptex.GetPoolInfoAsync("prl", ct)).Height;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogWarning(e, "Couldn't read Pearl's chain height; keeping the last known Pearl fork state");
            return _state ?? new PearlForkState(false, activation, null, "Pearl's chain height couldn't be read");
        }

        if (height is null)
            return new PearlForkState(false, activation, null, "Pearl's chain height isn't reported");
        var active = height >= activation;
        if (active && _state is not { Active: true })
            logger.LogWarning("Pearl's FP8 fork is active (height {Height} ≥ {Activation}): pearlhash is now limited to Hopper and Blackwell cards", height, activation);
        return new PearlForkState(active, activation, height,
            active ? $"Pearl's fork activated at block {activation}" : $"Pearl's fork activates at block {activation} (now {height})");
    }

    /// <summary>The activation height PIP-3 names, or null while it says "TBD" or nothing.</summary>
    internal static long? ActivationHeightIn(string? pip)
    {
        if (string.IsNullOrWhiteSpace(pip))
            return null;
        var match = FrontMatterHeightRegex().Match(pip);
        if (!match.Success)
            match = TextHeightRegex().Match(pip);
        return match.Success && long.TryParse(match.Groups["height"].Value.Replace(",", "", StringComparison.Ordinal),
            NumberStyles.None, CultureInfo.InvariantCulture, out var height) && height > 0
            ? height
            : null;
    }

    [GeneratedRegex(@"(?im)^\s*activation[-_ ]?(height|block)\s*:\s*(?<height>\d[\d,]*)\s*$")]
    private static partial Regex FrontMatterHeightRegex();

    /// <summary>"activation height of 150,000", "activates at block 150000", "Activation height: 150000".</summary>
    [GeneratedRegex(@"(?i)\bactivat(ion|es)\s+(height|at\s+(block|height))\b[^\d\n]{0,20}?(?<height>\d{1,3}(,\d{3})+|\d{4,})")]
    private static partial Regex TextHeightRegex();
}

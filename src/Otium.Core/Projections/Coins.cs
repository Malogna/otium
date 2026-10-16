namespace Otium.Core.Projections;

/// <summary>Human names for what a mining algorithm actually produces, for the "what is it doing" overview.</summary>
public static class Coins
{
    public sealed record CoinInfo(string Name, string Symbol);

    private static readonly Dictionary<string, CoinInfo> ByAlgorithm = new(StringComparer.OrdinalIgnoreCase)
    {
        ["rx/0"] = new("Monero", "XMR"),
        ["kawpow"] = new("Ravencoin", "RVN"),
        ["autolykos2"] = new("Ergo", "ERG"),
        ["etchash"] = new("Ethereum Classic", "ETC"),
        ["cn/gpu"] = new("Ryo", "RYO"),
        ["pearlhash"] = new("Pearl", "PRL"),
        ["quantus"] = new("Quantus", "QUANTUS"),
    };

    public static CoinInfo ForAlgorithm(string? algorithm)
    {
        if (algorithm is null)
            return new CoinInfo("crypto", "");
        // Details like "kawpow (switched from etchash)" or "simulated kawpow" still name the algorithm.
        foreach (var (algo, coin) in ByAlgorithm)
        {
            if (algorithm.Contains(algo, StringComparison.OrdinalIgnoreCase))
                return coin;
        }
        return new CoinInfo(algorithm, "");
    }
}

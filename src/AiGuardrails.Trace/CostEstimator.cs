namespace AiGuardrails.Trace;

/// <summary>Lijstprijs in USD per 1M tokens voor modellen waarvan de naam met <c>ModelPrefix</c> begint.</summary>
public sealed record ModelPrice(string ModelPrefix, double UsdPer1MIn, double UsdPer1MOut, double CachedInputFactor = 0.5);

/// <summary>Raming, geen factuur. Prefix-match op modelnaam (zodat "gpt-4.1-mini-2025-04-14" het tarief van "gpt-4.1-mini" krijgt);
/// onbekend model valt terug op <c>defaultModelPrefix</c>.</summary>
public sealed class CostEstimator
{
    private readonly IReadOnlyList<ModelPrice> _prices;
    private readonly ModelPrice _default;
    private readonly double _usdToEur;

    public CostEstimator(IReadOnlyList<ModelPrice> prices, string defaultModelPrefix, double usdToEur = 0.92)
    {
        ArgumentNullException.ThrowIfNull(prices);
        _prices = [.. prices];
        _default = _prices.FirstOrDefault(p => string.Equals(p.ModelPrefix, defaultModelPrefix, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"defaultModelPrefix '{defaultModelPrefix}' staat niet in de prijstabel.", nameof(defaultModelPrefix));
        _usdToEur = usdToEur;
    }

    /// <summary>Azure OpenAI-lijstprijs aug 2026, vaste koers 0,92 €/$ — de tabel van sociale-kaart-rag.</summary>
    public static CostEstimator Default { get; } = new(
        [
            new ModelPrice("gpt-4.1-mini", 0.40, 1.60),
            new ModelPrice("text-embedding-3-small", 0.02, 0),
        ],
        "gpt-4.1-mini");

    /// <summary>Raamt de kosten in euro. Een onbekend of <c>null</c>-model valt terug op de default-prijs;
    /// negatieve tokenaantallen tellen als nul.</summary>
    public double EstimateEur(string? model, int tokensIn, int tokensOut, int tokensCached)
    {
        var price = (model is null ? null : _prices.FirstOrDefault(p => model.StartsWith(p.ModelPrefix, StringComparison.OrdinalIgnoreCase))) ?? _default;
        var cached = Math.Min(Math.Max(0, tokensCached), Math.Max(0, tokensIn));
        var uncached = Math.Max(0, tokensIn) - cached;
        var usd = (uncached * price.UsdPer1MIn + cached * price.UsdPer1MIn * price.CachedInputFactor + Math.Max(0, tokensOut) * price.UsdPer1MOut) / 1_000_000;
        return Math.Round(usd * _usdToEur, 6);
    }
}

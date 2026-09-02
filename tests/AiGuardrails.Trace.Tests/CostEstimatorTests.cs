using AiGuardrails.Trace;

namespace AiGuardrails.Trace.Tests;

public class CostEstimatorTests
{
    // Zelfde verwachtingen als sociale-kaart-rag: $0.40/M in, $1.60/M uit, koers 0,92, cache 50 %.
    [Theory]
    [InlineData("gpt-4.1-mini", 1000, 500, 0, 0.001104)]
    [InlineData("gpt-4.1-mini", 1000, 0, 1000, 0.000184)]           // volledig gecachte input
    [InlineData("gpt-4.1-mini-2025-04-14", 1000, 500, 0, 0.001104)]  // versie-suffix, prefix-match
    [InlineData("text-embedding-3-small", 1000000, 0, 0, 0.0184)]
    [InlineData("unknown-model", 1000, 500, 0, 0.001104)]            // valt terug op default
    public void Default_table_matches_sociale_kaart_rag(string model, int tokensIn, int tokensOut, int cached, double expectedEur)
        => Assert.Equal(expectedEur, CostEstimator.Default.EstimateEur(model, tokensIn, tokensOut, cached), 6);

    [Fact]
    public void Custom_table_and_rate_are_used()
    {
        var est = new CostEstimator([new ModelPrice("chat-default", 1.00, 2.00)], "chat-default", usdToEur: 1.0);
        Assert.Equal(0.002, est.EstimateEur("chat-default", 1000, 500, 0), 6);   // (1000*1 + 500*2)/1M
    }

    [Fact]
    public void Cached_tokens_never_exceed_input()
    {
        var est = new CostEstimator([new ModelPrice("m", 1.00, 0)], "m", usdToEur: 1.0);
        Assert.Equal(0.0005, est.EstimateEur("m", 1000, 0, 5000), 6);           // alle 1000 gecachet à 50 %
    }

    [Fact]
    public void Unknown_default_prefix_throws()
        => Assert.Throws<ArgumentException>(() => new CostEstimator([new ModelPrice("a", 1, 1)], "b"));
}

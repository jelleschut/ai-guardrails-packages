using AiGuardrails.Trace;

namespace AiGuardrails.Trace.Tests;

public class CorrelationIdTests
{
    [Fact]
    public void New_is_32_lowercase_hex_and_unique()
    {
        var a = CorrelationId.New();
        var b = CorrelationId.New();
        Assert.True(CorrelationId.IsValid(a));
        Assert.NotEqual(a, b);
    }

    [Theory]
    [InlineData("3f2a9c1e5b7d4e8f9a0b1c2d3e4f5a6b", true)]
    [InlineData("3F2A9C1E5B7D4E8F9A0B1C2D3E4F5A6B", false)]   // hoofdletters
    [InlineData("3f2a9c1e5b7d4e8f9a0b1c2d3e4f5a6", false)]    // 31 tekens
    [InlineData("3f2a9c1e-5b7d-4e8f-9a0b-1c2d3e4f5a6b", false)] // guid met streepjes
    [InlineData("../by-id/x", false)]
    [InlineData("", false)]
    public void IsValid(string id, bool expected) => Assert.Equal(expected, CorrelationId.IsValid(id));
}

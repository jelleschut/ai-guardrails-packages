using System.Text.Json;
using System.Text.Json.Nodes;
using AiGuardrails.Trace;

namespace AiGuardrails.Trace.Tests;

public class TraceRecordTests
{
    [Fact]
    public void Serializes_camel_case_without_nulls_and_without_question_or_answer_text()
    {
        var t = TraceRecord.Start("3f2a9c1e5b7d4e8f9a0b1c2d3e4f5a6b") with
        {
            PolicyVersion = "1.1.0", PiiRedacted = true, PiiTypes = ["bsn"],
            Outcome = "refused_medical", ToolCalls = [new ToolCall("search_social_map", "abc", 1)],
        };
        var json = JsonSerializer.Serialize(t, TraceRecord.JsonOptions);

        Assert.Contains("\"correlationId\":\"3f2a9c1e5b7d4e8f9a0b1c2d3e4f5a6b\"", json);
        Assert.Contains("\"policyVersion\":\"1.1.0\"", json);
        Assert.Contains("\"outcome\":\"refused_medical\"", json);
        Assert.Contains("\"piiTypes\":[\"bsn\"]", json);
        Assert.Contains("\"toolCalls\":[{\"name\":\"search_social_map\",\"argumentsHash\":\"abc\",\"resultCount\":1}]", json);
        Assert.DoesNotContain("question", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("answerText", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"model\"", json);       // null weggelaten
        Assert.DoesNotContain("\"extensions\"", json);  // extension-data is plat, nooit een eigen veld
    }

    [Fact]
    public void Default_outcome_is_error()
        => Assert.Equal(Outcomes.Error, TraceRecord.Start("c").Outcome);

    [Fact]
    public void Roundtrips_core_fields_through_json()
    {
        var t = TraceRecord.Start("x") with { Outcome = "escalated", RefusalReason = "low_retrieval_score", TokensIn = 12, LatencyMs = 34 };
        var back = JsonSerializer.Deserialize<TraceRecord>(JsonSerializer.Serialize(t, TraceRecord.JsonOptions), TraceRecord.JsonOptions)!;
        Assert.Equal("escalated", back.Outcome);
        Assert.Equal("low_retrieval_score", back.RefusalReason);
        Assert.Equal(12, back.TokensIn);
        Assert.Equal(34, back.LatencyMs);
    }

    [Fact]
    public void Extensions_are_written_flat_and_read_back_typed()
    {
        var t = TraceRecord.Start("x")
            .WithExtension("intent", "find_help")
            .WithExtension("retrievedScores", new[] { 0.9, 0.1 });
        var json = JsonSerializer.Serialize(t, TraceRecord.JsonOptions);

        Assert.Contains("\"intent\":\"find_help\"", json);
        Assert.Contains("\"retrievedScores\":[0.9,0.1]", json);

        var back = JsonSerializer.Deserialize<TraceRecord>(json, TraceRecord.JsonOptions)!;
        Assert.Equal("find_help", back.GetExtension<string>("intent"));
        Assert.Equal([0.9, 0.1], back.GetExtension<double[]>("retrievedScores")!);
        Assert.Null(back.GetExtension<string>("bestaat_niet"));
    }

    [Fact]
    public void WithExtension_does_not_mutate_the_original()
    {
        var a = TraceRecord.Start("x");
        var b = a.WithExtension("intent", "find_help");
        Assert.Null(a.GetExtension<string>("intent"));
        Assert.Equal("find_help", b.GetExtension<string>("intent"));
    }

    // Letterlijke regel in het formaat dat sociale-kaart-rag (policy 1.1.0) vandaag wegschrijft.
    private const string LegacyLine =
        """
        {"correlationId":"3f2a9c1e5b7d4e8f9a0b1c2d3e4f5a6b","timestamp":"2026-08-29T10:15:30.1234567+00:00","policyVersion":"1.1.0","model":"gpt-4.1-mini","modelVersion":"2025-04-14","promptHash":"9c1e2d3f","piiRedacted":true,"piiTypes":["bsn"],"intent":"medical","domain":"zorg","toolCalls":[],"retrievedChunkIds":["osm:node/123#0"],"retrievedScores":[0.031],"tokensIn":1200,"tokensOut":80,"tokensCached":0,"estimatedCostEur":0.000559,"latencyMs":1834,"outcome":"refused_medical","refusalReason":"medical_intent"}
        """;

    [Fact]
    public void Reads_a_sociale_kaart_rag_trace_line_without_loss()
    {
        var t = JsonSerializer.Deserialize<TraceRecord>(LegacyLine, TraceRecord.JsonOptions)!;

        Assert.Equal("3f2a9c1e5b7d4e8f9a0b1c2d3e4f5a6b", t.CorrelationId);
        Assert.Equal("1.1.0", t.PolicyVersion);
        Assert.Equal("refused_medical", t.Outcome);
        Assert.Equal("medical", t.GetExtension<string>("intent"));
        Assert.Equal("zorg", t.GetExtension<string>("domain"));
        Assert.Equal(["osm:node/123#0"], t.GetExtension<string[]>("retrievedChunkIds")!);
        Assert.Equal([0.031], t.GetExtension<double[]>("retrievedScores")!);

        var again = JsonSerializer.Serialize(t, TraceRecord.JsonOptions);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(LegacyLine), JsonNode.Parse(again)), again);
    }

    [Theory]
    [InlineData("model")]
    [InlineData("Model")]
    [InlineData("correlationId")]
    public void WithExtension_rejects_core_field_names(string name)
        => Assert.Throws<ArgumentException>(() => TraceRecord.Start("x").WithExtension(name, "hacked"));

    [Fact]
    public void WithExtension_with_null_removes_the_field()
    {
        var t = TraceRecord.Start("x").WithExtension("intent", "a").WithExtension<string?>("intent", null);
        Assert.Null(t.GetExtension<string>("intent"));
        Assert.DoesNotContain("intent", JsonSerializer.Serialize(t, TraceRecord.JsonOptions));
    }

    [Fact]
    public void Every_serialized_core_field_is_a_reserved_extension_name()
    {
        var full = TraceRecord.Start("3f2a9c1e5b7d4e8f9a0b1c2d3e4f5a6b") with
        {
            PolicyVersion = "p", Model = "m", ModelVersion = "v", PromptHash = "h", PiiRedacted = true, PiiTypes = ["bsn"],
            ToolCalls = [new ToolCall("t", "a", 1)], TokensIn = 1, TokensOut = 2, TokensCached = 3, EstimatedCostEur = 4, LatencyMs = 5,
            Outcome = "o", RefusalReason = "r",
        };
        var names = JsonNode.Parse(JsonSerializer.Serialize(full, TraceRecord.JsonOptions))!.AsObject().Select(kv => kv.Key);
        foreach (var n in names)
            Assert.Throws<ArgumentException>(() => full.WithExtension(n, "x"));
    }

    [Fact]
    public void JsonOptions_are_read_only()
    {
        Assert.True(TraceRecord.JsonOptions.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => TraceRecord.JsonOptions.PropertyNamingPolicy = null);
    }
}

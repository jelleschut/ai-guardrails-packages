using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiGuardrails.Trace;

/// <summary>Eén tool-aanroep binnen een request; alleen naam, hash van de argumenten en aantal resultaten.</summary>
public sealed record ToolCall(string Name, string ArgumentsHash, int ResultCount);

/// <summary>Uitkomsten die het package zelf kent. Een app of gateway voegt eigen snake_case-waarden toe
/// (bijv. "refused_medical", "quota_exceeded").</summary>
public static class Outcomes
{
    public const string Answered = "answered";
    public const string Error = "error";
}

/// <summary>Eén record per request. Bevat nooit vraag- of antwoordtekst; wel hashes.
/// App-specifieke velden gaan via <see cref="WithExtension{T}"/> en staan plat in de JSON naast de kernvelden.</summary>
public sealed record TraceRecord
{
    public required string CorrelationId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    /// <summary>Semver van prompt + drempels van de aanroepende app; de app bepaalt de waarde.</summary>
    public string? PolicyVersion { get; init; }
    public string? Model { get; init; }
    public string? ModelVersion { get; init; }
    public string? PromptHash { get; init; }
    public bool PiiRedacted { get; init; }
    public string[] PiiTypes { get; init; } = [];
    public ToolCall[] ToolCalls { get; init; } = [];
    public int TokensIn { get; init; }
    public int TokensOut { get; init; }
    public int TokensCached { get; init; }
    public double EstimatedCostEur { get; init; }
    public long LatencyMs { get; init; }
    /// <summary>Open vocabulaire in snake_case; zie <see cref="Outcomes"/>.</summary>
    public string Outcome { get; init; } = Outcomes.Error;
    public string? RefusalReason { get; init; }

    /// <summary>App-specifieke velden. Serialiseert plat (geen "extensions"-veld); onbekende velden bij inlezen komen hier terecht.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; init; }

    public static TraceRecord Start(string correlationId) => new() { CorrelationId = correlationId, Timestamp = DateTimeOffset.UtcNow };

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public T? GetExtension<T>(string name)
        => Extensions is not null && Extensions.TryGetValue(name, out var el) ? el.Deserialize<T>(JsonOptions) : default;

    public TraceRecord WithExtension<T>(string name, T value)
    {
        var copy = Extensions is null ? new Dictionary<string, JsonElement>() : new Dictionary<string, JsonElement>(Extensions);
        copy[name] = JsonSerializer.SerializeToElement(value, JsonOptions);
        return this with { Extensions = copy };
    }
}

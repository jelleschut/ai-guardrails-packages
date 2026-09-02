using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

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

    /// <summary>App-specifieke velden. Serialiseert plat (geen "extensions"-veld); onbekende velden bij inlezen komen hier terecht.
    /// De dictionary wordt gedeeld door <c>with</c>-kopieën; muteer hem niet in place, gebruik <see cref="WithExtension{T}"/>.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; init; }

    public static TraceRecord Start(string correlationId) => new() { CorrelationId = correlationId, Timestamp = DateTimeOffset.UtcNow };

    /// <summary>Web-defaults (camelCase, hoofdletterongevoelig lezen), null weglaten. Read-only; maak een kopie via
    /// <c>new JsonSerializerOptions(TraceRecord.JsonOptions)</c> als je afwijkende instellingen nodig hebt.</summary>
    public static JsonSerializerOptions JsonOptions { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        // Resolver expliciet zetten: MakeReadOnly(populateMissingResolver: true) gooit onder trimming/AOT al bij het laden van het type.
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        o.MakeReadOnly();
        return o;
    }

    // Web-defaults lezen hoofdletterongevoelig, dus ook "Model" zou het kernveld overschrijven.
    private static readonly HashSet<string> CoreFieldNames = new(
        [
            "correlationId", "timestamp", "policyVersion", "model", "modelVersion", "promptHash",
            "piiRedacted", "piiTypes", "toolCalls", "tokensIn", "tokensOut", "tokensCached",
            "estimatedCostEur", "latencyMs", "outcome", "refusalReason",
        ],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>Leest een uitbreidingsveld. Ontbreekt het veld, dan <c>default</c>. Past de JSON-vorm niet op <typeparamref name="T"/>
    /// (bijv. een JSON-null in een niet-nullable <c>int</c>), dan gooit dit <see cref="JsonException"/>; gebruik een nullable
    /// <typeparamref name="T"/> voor velden die null kunnen zijn.</summary>
    public T? GetExtension<T>(string name)
        => Extensions is not null && Extensions.TryGetValue(name, out var el) ? el.Deserialize<T>(JsonOptions) : default;

    /// <summary>Geeft een kopie met het uitbreidingsveld gezet. Kernveldnamen zijn niet toegestaan (ook niet in andere hoofdletters);
    /// een <c>null</c>-waarde verwijdert het veld, conform de null-weglaatregel van het record.</summary>
    public TraceRecord WithExtension<T>(string name, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (CoreFieldNames.Contains(name))
            throw new ArgumentException($"'{name}' is een kernveld en kan niet als uitbreiding worden gezet.", nameof(name));
        var copy = Extensions is null ? new Dictionary<string, JsonElement>() : new Dictionary<string, JsonElement>(Extensions);
        if (value is null) copy.Remove(name);
        else copy[name] = JsonSerializer.SerializeToElement(value, JsonOptions);
        return this with { Extensions = copy };
    }
}

using System.Globalization;
using System.Text.Json;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace AiGuardrails.Trace.Azure;

public sealed record AppInsightsTraceSinkOptions(string EventName = "ai.request", string MetricPrefix = "ai");

/// <summary>Eén custom event per request met alle kernvelden als properties (customDimensions), plus de uitbreidingsvelden:
/// strings letterlijk, arrays als <c>{naam}Count</c>, overige waarden als hun JSON-tekst. Numerieke kernvelden gaan daarnaast
/// als echte metrics (customMetrics) voor dashboards en alerts.</summary>
public sealed class AppInsightsTraceSink(TelemetryClient telemetry, AppInsightsTraceSinkOptions? options = null) : ITraceSink
{
    private readonly AppInsightsTraceSinkOptions _o = options ?? new AppInsightsTraceSinkOptions();

    public Task WriteAsync(TraceRecord r, CancellationToken ct = default)
    {
        var evt = new EventTelemetry(_o.EventName);
        var p = evt.Properties;
        p["correlationId"] = r.CorrelationId;
        p["policyVersion"] = r.PolicyVersion ?? "";
        p["model"] = r.Model ?? "";
        p["modelVersion"] = r.ModelVersion ?? "";
        p["outcome"] = r.Outcome;
        p["piiRedacted"] = r.PiiRedacted.ToString();
        p["piiTypes"] = string.Join(",", r.PiiTypes);
        p["refusalReason"] = r.RefusalReason ?? "";
        // Microsoft.ApplicationInsights 3.x kent EventTelemetry.Metrics niet meer; numerieke velden als string in Properties.
        p["tokensIn"] = r.TokensIn.ToString(CultureInfo.InvariantCulture);
        p["tokensOut"] = r.TokensOut.ToString(CultureInfo.InvariantCulture);
        p["tokensCached"] = r.TokensCached.ToString(CultureInfo.InvariantCulture);
        p["estimatedCostEur"] = r.EstimatedCostEur.ToString("F6", CultureInfo.InvariantCulture);
        p["latencyMs"] = r.LatencyMs.ToString(CultureInfo.InvariantCulture);
        p["toolCallsCount"] = r.ToolCalls.Length.ToString(CultureInfo.InvariantCulture);

        if (r.Extensions is not null)
        {
            foreach (var (name, el) in r.Extensions)
            {
                switch (el.ValueKind)
                {
                    case JsonValueKind.Array: p[name + "Count"] = el.GetArrayLength().ToString(CultureInfo.InvariantCulture); break;
                    case JsonValueKind.String: p[name] = el.GetString() ?? ""; break;
                    case JsonValueKind.Null: break;
                    default: p[name] = el.GetRawText(); break;
                }
            }
        }

        telemetry.TrackEvent(evt);
        telemetry.GetMetric($"{_o.MetricPrefix}.estimatedCostEur").TrackValue(r.EstimatedCostEur);
        telemetry.GetMetric($"{_o.MetricPrefix}.latencyMs").TrackValue(r.LatencyMs);
        telemetry.GetMetric($"{_o.MetricPrefix}.tokensIn").TrackValue(r.TokensIn);
        telemetry.GetMetric($"{_o.MetricPrefix}.tokensOut").TrackValue(r.TokensOut);
        return Task.CompletedTask;
    }
}

using AiGuardrails.Trace;
using AiGuardrails.Trace.Azure;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OtelMetric = OpenTelemetry.Metrics.Metric;

namespace AiGuardrails.Trace.Azure.Tests;

public class AppInsightsTraceSinkTests
{
    // Microsoft.ApplicationInsights 3.x heeft geen ITelemetryChannel/TelemetryConfiguration.TelemetryChannel meer:
    // de SDK is een dunne laag boven OpenTelemetry geworden. TrackEvent komt binnen als OpenTelemetry LogRecord,
    // GetMetric(...).TrackValue(...) als OpenTelemetry-histogram. Deze twee capture-processors vervangen daarom
    // de klassieke in-memory ITelemetryChannel uit oudere (2.x) SDK-versies, met dezelfde intentie: alles opvangen
    // zonder naar een echte Application Insights-resource te versturen.
    private sealed class CapturingLogProcessor : BaseProcessor<LogRecord>
    {
        public readonly List<Dictionary<string, string>> Events = [];

        public override void OnEnd(LogRecord data)
        {
            var props = new Dictionary<string, string>();
            if (data.Attributes is not null)
                foreach (var (key, value) in data.Attributes)
                    props[key] = value?.ToString() ?? "";
            Events.Add(props);
        }
    }

    private sealed class CapturingMetricExporter : BaseExporter<OtelMetric>
    {
        public readonly HashSet<string> MetricNames = [];

        public override ExportResult Export(in Batch<OtelMetric> batch)
        {
            foreach (var m in batch) MetricNames.Add(m.Name);
            return ExportResult.Success;
        }
    }

    private static (TelemetryClient Client, CapturingLogProcessor Logs, CapturingMetricExporter Metrics) Client()
    {
        var logs = new CapturingLogProcessor();
        var metrics = new CapturingMetricExporter();
        var config = new TelemetryConfiguration { ConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000" };
        config.ConfigureOpenTelemetryBuilder(b =>
        {
            b.WithLogging(l => l.AddProcessor(logs));
            b.WithMetrics(m => m.AddReader(new BaseExportingMetricReader(metrics)));
        });
        return (new TelemetryClient(config), logs, metrics);
    }

    private static TraceRecord Sample() => TraceRecord.Start("3f2a9c1e5b7d4e8f9a0b1c2d3e4f5a6b") with
    {
        PolicyVersion = "1.1.0", Model = "gpt-4.1-mini", ModelVersion = "2025-04-14", PiiRedacted = true,
        TokensIn = 1200, TokensOut = 80, TokensCached = 100, EstimatedCostEur = 0.000559, LatencyMs = 1834,
        Outcome = "refused_medical", RefusalReason = "medical_intent",
    };

    [Fact]
    public async Task Core_fields_become_event_properties_under_default_event_name()
    {
        var (client, logs, _) = Client();
        await new AppInsightsTraceSink(client).WriteAsync(Sample());

        var evt = Assert.Single(logs.Events);
        Assert.Equal("ai.request", evt["microsoft.custom_event.name"]);
        Assert.Equal("3f2a9c1e5b7d4e8f9a0b1c2d3e4f5a6b", evt["correlationId"]);
        Assert.Equal("1.1.0", evt["policyVersion"]);
        Assert.Equal("gpt-4.1-mini", evt["model"]);
        Assert.Equal("2025-04-14", evt["modelVersion"]);
        Assert.Equal("refused_medical", evt["outcome"]);
        Assert.Equal("True", evt["piiRedacted"]);
        Assert.Equal("medical_intent", evt["refusalReason"]);
        Assert.Equal("1200", evt["tokensIn"]);
        Assert.Equal("80", evt["tokensOut"]);
        Assert.Equal("100", evt["tokensCached"]);
        Assert.Equal("0.000559", evt["estimatedCostEur"]);
        Assert.Equal("1834", evt["latencyMs"]);
    }

    [Fact]
    public async Task Extensions_become_properties_strings_raw_and_arrays_as_count()
    {
        var (client, logs, _) = Client();
        var t = Sample()
            .WithExtension("intent", "medical")
            .WithExtension("retrievedChunkIds", new[] { "a", "b", "c" })
            .WithExtension("escalationScore", 0.015);
        await new AppInsightsTraceSink(client).WriteAsync(t);

        var evt = Assert.Single(logs.Events);
        Assert.Equal("medical", evt["intent"]);
        Assert.Equal("3", evt["retrievedChunkIdsCount"]);
        Assert.False(evt.ContainsKey("retrievedChunkIds"));
        Assert.Equal("0.015", evt["escalationScore"]);
    }

    [Fact]
    public async Task Event_name_and_metric_prefix_are_configurable()
    {
        var (client, logs, metrics) = Client();
        await new AppInsightsTraceSink(client, new AppInsightsTraceSinkOptions(EventName: "rag.request", MetricPrefix: "rag")).WriteAsync(Sample());
        client.Flush();

        Assert.Equal("rag.request", Assert.Single(logs.Events)["microsoft.custom_event.name"]);
        Assert.Superset(new HashSet<string> { "rag.estimatedCostEur", "rag.latencyMs", "rag.tokensIn", "rag.tokensOut" }, metrics.MetricNames);
    }
}

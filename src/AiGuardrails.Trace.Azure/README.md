# AiGuardrails.Trace.Azure

Azure-sinks voor [AiGuardrails.Trace](https://github.com/jelleschut/ai-guardrails-packages).

## BlobTraceSink

```csharp
var sink = new BlobTraceSink(blobServiceClient, new BlobTraceSinkOptions(ContainerName: "traces"));
await sink.WriteAsync(trace);                       // yyyy/MM/dd.jsonl (append) + by-id/{id}.json
var back = await sink.ReadAsync(trace.CorrelationId);
```

Het correlatie-id vormt de blobnaam en moet 32 lowercase hex zijn (`CorrelationId.New()`);
andere waarden worden geweigerd vóór er een netwerkcall plaatsvindt. Container en lifecycle
regelt de consument in zijn infra; de sink maakt de container niet aan.

## AppInsightsTraceSink

```csharp
var sink = new AppInsightsTraceSink(telemetryClient,
    new AppInsightsTraceSinkOptions(EventName: "ai.request", MetricPrefix: "ai"));
```

Schrijft één custom event per request. Kernvelden staan als properties (customDimensions);
uitbreidingsvelden ook: strings letterlijk onder hun eigen naam, arrays als `{naam}Count`,
overige waarden als JSON-tekst. Daarnaast vier metrics: `{prefix}.estimatedCostEur`,
`{prefix}.latencyMs`, `{prefix}.tokensIn`, `{prefix}.tokensOut`.

Beide sinks combineren via `CompositeTraceSink` uit AiGuardrails.Trace, zodat een falende sink
het antwoord nooit blokkeert.

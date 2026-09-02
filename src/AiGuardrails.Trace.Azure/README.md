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

Het verzamelblob wordt in **UTC** gepartitioneerd, standaard per dag (`yyyy/MM/dd.jsonl`); met
`BlobTraceSinkOptions.PartitionFormat` kies je een andere indeling, bijvoorbeeld `yyyy'/'MM'/'dd'/'HH`
voor een blob per uur. Let op het Azure-plafond van 50.000 blokken per append-blob: boven ~50.000 traces
per partitie faalt elke volgende append (409 `BlockCountExceedsLimit`), dus kies bij dat volume een
fijnere `PartitionFormat`.

## AppInsightsTraceSink

```csharp
var sink = new AppInsightsTraceSink(telemetryClient,
    new AppInsightsTraceSinkOptions(EventName: "ai.request", MetricPrefix: "ai"));
```

Schrijft één custom event per request. Kernvelden staan als properties (customDimensions):
`correlationId`, `policyVersion`, `model`, `modelVersion`, `promptHash`, `outcome`, `piiRedacted`,
`piiTypes`, `refusalReason`, `tokensIn`, `tokensOut`, `tokensCached`, `estimatedCostEur`,
`latencyMs` en `toolCallsCount`. Uitbreidingsvelden ook: strings letterlijk onder hun eigen naam,
arrays als `{naam}Count`, overige waarden als JSON-tekst. Bij een naamconflict tussen een uitbreiding
en een kernveld-property wint het kernveld. Daarnaast vier metrics: `{prefix}.estimatedCostEur`,
`{prefix}.latencyMs`, `{prefix}.tokensIn`, `{prefix}.tokensOut`.

Beide sinks combineren via `CompositeTraceSink` uit AiGuardrails.Trace, zodat een falende sink
het antwoord nooit blokkeert.

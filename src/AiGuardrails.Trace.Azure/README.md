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

Zie de volgende sectie (wordt aangevuld).

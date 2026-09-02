# AiGuardrails.Trace

Generiek trace-schema voor AI-aanroepen: één `TraceRecord` per request, **nooit vraag- of
antwoordtekst**, wel hashes, tokens, kosten, latency en uitkomst.

```csharp
using AiGuardrails.Trace;

var trace = TraceRecord.Start(CorrelationId.New()) with
{
    PolicyVersion = "1.1.0", Model = "gpt-4.1-mini", PiiRedacted = true, PiiTypes = ["bsn"],
    TokensIn = 1200, TokensOut = 80, Outcome = Outcomes.Answered,
};
trace = trace.WithExtension("intent", "find_help");   // app-veld, staat plat in de JSON
await sink.WriteAsync(trace);
```

- Kernvelden zijn generiek; app-velden gaan via `WithExtension`/`GetExtension` en blijven
  plat in de JSON staan (`[JsonExtensionData]`). Onbekende velden gaan bij inlezen niet verloren.
- `Outcome` is een open snake_case-string; het package kent alleen `answered` en `error`.
- `ITraceSink`/`ITraceReader` zijn de contracten; `CompositeTraceSink` schrijft naar alle sinks
  en gooit nooit. Azure-implementaties: `AiGuardrails.Trace.Azure`.
- `CorrelationId.New()` geeft 32 lowercase hex; sinks accepteren alleen dat formaat.
- `CostEstimator` raamt kosten in euro op basis van een instelbare prijstabel.

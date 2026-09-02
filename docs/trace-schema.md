# Trace-schema (AiGuardrails.Trace)

Eén JSON-regel per request. Kernvelden komen uit het package; alles wat daarbuiten valt is een
uitbreidingsveld van de aanroepende app en staat plat naast de kernvelden. Nooit vraag- of
antwoordtekst.

## Kernvelden

| Veld | Type | Betekenis |
|---|---|---|
| `correlationId` | string | 32 lowercase hex (`CorrelationId.New()`); ook de blobnaam bij `BlobTraceSink` |
| `timestamp` | ISO-8601 | start van het request, UTC |
| `policyVersion` | string? | semver van prompt + drempels van de app |
| `model` | string? | logische of concrete modelnaam |
| `modelVersion` | string? | versie/datum-suffix van het model |
| `promptHash` | string? | hash van de volledige prompt; de prompt zelf wordt niet opgeslagen |
| `piiRedacted` | bool | is er iets geredigeerd vóór de modelaanroep |
| `piiTypes` | string[] | typen uit `AiGuardrails.Pii` (`email`, `bsn`, `address`, `phone`); nooit waarden |
| `toolCalls` | `{name, argumentsHash, resultCount}[]` | tool-aanroepen; argumenten alleen als hash |
| `tokensIn` / `tokensOut` / `tokensCached` | int | tokentelling van de modelaanroep |
| `estimatedCostEur` | double | raming via `CostEstimator`; geen factuur |
| `latencyMs` | long | totale duur |
| `outcome` | string | snake_case; package kent `answered` en `error`, apps voegen toe |
| `refusalReason` | string? | korte code bij een weigering of escalatie |

Null-velden worden weggelaten. Kernveldnamen zijn gereserveerd: `WithExtension` weigert ze
(hoofdletterongevoelig).

## Uitbreidingsvelden (voorbeeld: sociale-kaart-rag)

| Veld | Type | Betekenis |
|---|---|---|
| `intent` | string | geclassificeerde intentie |
| `domain` | string | domein van de vraag |
| `retrievedChunkIds` | string[] | opgehaalde chunks |
| `retrievedScores` | double[] | scores per chunk |

In App Insights worden string-uitbreidingen letterlijk als property gezet, arrays als
`{naam}Count` (bijv. `retrievedChunkIdsCount`), overige waarden als JSON-tekst. Daarnaast
zet de sink `piiTypes` (kommagescheiden) en `toolCallsCount` als property.

## Voorbeeldregel

```json
{"correlationId":"3f2a9c1e5b7d4e8f9a0b1c2d3e4f5a6b","timestamp":"2026-08-29T10:15:30.1234567+00:00","policyVersion":"1.1.0","model":"gpt-4.1-mini","modelVersion":"2025-04-14","promptHash":"9c1e2d3f","piiRedacted":true,"piiTypes":["bsn"],"intent":"medical","domain":"zorg","toolCalls":[],"retrievedChunkIds":["osm:node/123#0"],"retrievedScores":[0.031],"tokensIn":1200,"tokensOut":80,"tokensCached":0,"estimatedCostEur":0.000559,"latencyMs":1834,"outcome":"refused_medical","refusalReason":"medical_intent"}
```

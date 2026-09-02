# ADR-0002: Trace-schema als generieke kern met platte uitbreidingsvelden

Datum: 2026-09-02 · Status: geaccepteerd

## Context

Het `TraceRecord` van sociale-kaart-rag mengt generieke velden (correlatie, model, tokens,
kosten, latency, PII) met app-velden (`intent`, `domain`, `retrievedChunkIds`,
`retrievedScores`) en een uitkomst-enum met app-waarden (`RefusedMedical`, `RefusedScope`).
Een package met die velden is niet generiek; twee losse records (gateway en app) zouden twee
schema's en een join in het inzicht-paneel betekenen.

## Besluit

- **Kern + extensions**: het package definieert de generieke kernvelden; app-velden gaan in
  `[JsonExtensionData]`. Ze staan daardoor **plat** in de JSON, naast de kernvelden, zonder
  `extensions`-wrapper. `GetExtension<T>`/`WithExtension<T>` zijn de typed-toegang.
- **Kernveldnamen zijn gereserveerd**: `WithExtension("model", …)` gooit, ook bij andere
  hoofdletters, omdat de Web-defaults hoofdletterongevoelig lezen en een dubbele sleutel het
  kernveld bij teruglezen zou overschrijven. Een `null`-waarde verwijdert het veld, conform
  de null-weglaatregel van het record.
- **Uitkomst is een open snake_case-string**; het package kent alleen `answered` en `error`.
  Apps en de gateway voegen eigen waarden toe (`refused_medical`, `quota_exceeded`).
- **PolicyVersion is een gewone string** zonder default; de koppeling aan de policy-klasse van
  de app was app-kennis.
- **`JsonOptions` is read-only** (get-only property, `MakeReadOnly`): een gedeeld muteerbaar
  serialisatieprofiel in een package zou het wire-formaat van álle consumenten in het proces
  kunnen veranderen. Wie afwijkt, kopieert: `new JsonSerializerOptions(TraceRecord.JsonOptions)`.
- **Nooit tekst**: het record bevat geen vraag- of antwoordtekst, alleen hashes. Dit is het
  contract van het package, niet slechts een conventie van één app.

## Consequenties

- Bestaande trace-regels van sociale-kaart-rag lezen zonder verlies in het nieuwe schema
  (getest met een letterlijke regel; `JsonNode.DeepEquals` na round-trip). Het inzicht-paneel,
  `docs/traceability.md` en de eval-scoring van die app blijven geldig zonder migratie.
- Onbekende velden gaan bij inlezen niet verloren en gooien niet; `GetExtension<T>` gooit wél
  een `JsonException` als de JSON-vorm niet op `T` past — gebruik een nullable `T` voor velden
  die null kunnen zijn.
- De App Insights-sink zet extension-strings letterlijk als property, arrays als `{naam}Count`.
  Het huidige `retrieved`-property van sociale-kaart-rag heet daar `retrievedChunkIdsCount`;
  v2 past zijn query's aan.
- `CostEstimator` wijkt bewust af van het origineel: gecachte tokens worden begrensd op
  `tokensIn` en negatieve aantallen tellen als nul; voor de bestaande eval-cases is het
  resultaat identiek (getest).
- Record-gelijkheid is referentie-gelijkheid voor arrays en extensions; vergelijk traces via
  hun JSON.

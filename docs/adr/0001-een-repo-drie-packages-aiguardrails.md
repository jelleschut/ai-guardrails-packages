# ADR-0001: Eén repo, drie packages, voorvoegsel AiGuardrails

Datum: 2026-09-02 · Status: geaccepteerd

## Context

Sociale-kaart-rag bevat generieke waarborgen (PII-redactie, trace-schema met sinks) als
applicatiecode. Het AI-gateway-traject (kickoff in die repo, `docs/kickoff-ai-gateway.md`)
promoveert ze tot herbruikbare bouwstenen die de gateway zelf en een v2 van de app consumeren.
Open stonden: naamgeving, repo-indeling en de knip in packages.

## Besluit

- **Naam**: voorvoegsel `AiGuardrails.*`, repo `ai-guardrails-packages`. Neutraal en
  app-onafhankelijk; niet `SocialeKaart.*`. Engels, zelfverklarend voor lezers buiten het
  Nederlandse portfolio; de documentatie blijft Nederlands zoals de rest van het portfolio.
- **Eén publieke repo met meerdere packages** in plaats van een repo per package: één plek
  voor ADR's, scans en release-discipline; het eval-harnas schuift er later in. Een repo per
  package zou de CI-/scan-inrichting verdubbelen voor twee kleine packages. De packages in de
  gateway-repo zetten is afgewezen: v2 zou dan van de gateway-repo afhangen voor zijn PII-filter.
- **Drie packages**: `AiGuardrails.Pii` (puur), `AiGuardrails.Trace` (schema, interfaces,
  composite sink, correlatie-id, kostenschatter — zonder Azure), `AiGuardrails.Trace.Azure`
  (Blob- en App Insights-sink). Consumenten zonder Azure, en unit-tests, nemen alleen `Trace`
  en slepen geen `Azure.Storage.Blobs`/`ApplicationInsights` mee.

## Consequenties

- Extractie is kopiëren, niet verplaatsen: sociale-kaart-rag blijft ongewijzigd tot stap 3 (v2).
- Het eval-harnas krijgt een eigen ontwerpronde en komt als vierde package in deze repo.
- Geen DI-registratiehelper tot er twee consumenten zijn (YAGNI).
- Gedeelde build-instellingen staan in `Directory.Build.props` (root), `src/Directory.Build.props`
  (package-metadata, MinVer, README-packing, embedded PDB) en `tests/Directory.Build.props`
  (xunit); een csproj bevat alleen wat projectspecifiek is.

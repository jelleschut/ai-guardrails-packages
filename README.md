# ai-guardrails-packages

Herbruikbare AI-waarborgen als NuGet-packages, geëxtraheerd uit
[sociale-kaart-rag](https://github.com/jelleschut/sociale-kaart-rag): "van afspraak naar
garantie". Eerste stap van het AI-gateway-traject; de gateway zelf en een v2 van
sociale-kaart-rag consumeren deze packages.

| Package | Wat | Afhankelijkheden |
|---|---|---|
| `AiGuardrails.Pii` | regex-redactie van Nederlandse PII (BSN met 11-proef, e-mail, telefoon, adres) | geen |
| `AiGuardrails.Trace` | generiek trace-schema (kern + platte uitbreidingen), sink-interfaces, composite sink, correlatie-id, kostenschatter | `Microsoft.Extensions.Logging.Abstractions` |
| `AiGuardrails.Trace.Azure` | `BlobTraceSink` (append per dag + blob per id) en `AppInsightsTraceSink` (event + metrics) | `Azure.Storage.Blobs`, `Microsoft.ApplicationInsights` |

## Gebruik

```csharp
using AiGuardrails.Pii;
using AiGuardrails.Trace;

var pii = PiiFilter.Redact(question);
var trace = TraceRecord.Start(CorrelationId.New()) with
{
    PolicyVersion = "1.1.0", PiiRedacted = pii.Redacted, PiiTypes = [.. pii.Types],
    Model = "gpt-4.1-mini", TokensIn = 1200, TokensOut = 80,
    EstimatedCostEur = CostEstimator.Default.EstimateEur("gpt-4.1-mini", 1200, 80, 0),
    Outcome = Outcomes.Answered,
}.WithExtension("intent", "find_help");

ITraceSink sink = new CompositeTraceSink([blobSink, appInsightsSink], logger);   // AiGuardrails.Trace.Azure
await sink.WriteAsync(trace);
```

Contract van elke trace: **nooit vraag- of antwoordtekst**, alleen hashes, typen en getallen.
Veld-voor-veld: [docs/trace-schema.md](docs/trace-schema.md). Per package staat een eigen
README in `src/<package>/README.md` (die zit ook in het NuGet-package).

## Installeren (GitHub Packages)

GitHub Packages vraagt ook voor publieke packages een token om te lezen. In GitHub Actions
volstaat `GITHUB_TOKEN`; lokaal een PAT met `read:packages`:

```xml
<!-- nuget.config van de consument -->
<packageSources>
  <add key="github-jelleschut" value="https://nuget.pkg.github.com/jelleschut/index.json" />
</packageSources>
<packageSourceCredentials>
  <github-jelleschut>
    <add key="Username" value="jelleschut" />
    <add key="ClearTextPassword" value="%GITHUB_PACKAGES_TOKEN%" />
  </github-jelleschut>
</packageSourceCredentials>
```

De PDB met SourceLink zit in de dll; step-into debuggen werkt zonder symbol server.

## Releasen

Versie per package via git-tag en [MinVer](https://github.com/adamralph/minver):

```
git tag pii/v0.1.0 && git push origin pii/v0.1.0                    # publiceert alleen AiGuardrails.Pii
git tag trace/v0.1.0 && git push origin trace/v0.1.0                # eerst Trace…
git tag trace-azure/v0.1.0 && git push origin trace-azure/v0.1.0    # …dan Trace.Azure
```

## Ontwikkelen

```
dotnet build -warnaserror
dotnet test --no-build          # 81 tests: Pii 32, Trace 38, Trace.Azure 11
dotnet pack -c Release -o artifacts
```

Let op bij tests rond `TelemetryClient`: `Microsoft.ApplicationInsights` 3.x is een laag over
OpenTelemetry zonder klassiek `ITelemetryChannel`; de tests vangen events en metrics op via een
OpenTelemetry `BaseProcessor<LogRecord>` en `BaseExporter<Metric>` (zie
`tests/AiGuardrails.Trace.Azure.Tests/AppInsightsTraceSinkTests.cs`).

## Discipline

Zelfde credo als de bronrepo: PR-flow met verplichte CI, SHA-gepinde actions, scans
(gitleaks, semgrep, trivy), geen secrets (`GITHUB_TOKEN` volstaat), ADR per besluit in
[docs/adr](docs/adr). Ontwerp en plan: [docs/superpowers](docs/superpowers).

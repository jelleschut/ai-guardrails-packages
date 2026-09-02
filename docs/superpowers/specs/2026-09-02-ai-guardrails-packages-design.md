# AiGuardrails-packages (ronde 1: Pii + Trace) — ontwerp

Datum: 2026-09-02 · Status: goedgekeurd in gesprek, wacht op review van de tekst · Eerste
deelproject van het AI-gateway-traject (`docs/kickoff-ai-gateway.md`, stap 1). De kickoff-brief
is de context; dit document is de spec voor de packages **Pii** en **Trace**. Het eval-harnas
(stap 1, derde package), de gateway (stap 2) en v2 (stap 3) krijgen elk hun eigen ronde.

## 1. Doel

De twee generieke waarborgen die de gateway zelf nodig heeft — PII-redactie en het
trace-schema met sinks — uit sociale-kaart-rag lichten en als NuGet-packages beschikbaar maken,
app-onafhankelijk en met dezelfde discipline als de bronrepo. Succescriterium: de packages
bouwen, testen en publiceren vanuit hun eigen repo; een bestaande trace-regel van
sociale-kaart-rag leest ongewijzigd in het nieuwe schema in; de 13 PII-testmethoden slagen
ongewijzigd tegen het verhuisde filter.

Buiten scope in deze ronde: het eval-harnas, de gateway, wijzigingen aan sociale-kaart-rag
(blijft read-only bron tot stap 3), Azure-resources en Terraform (er zijn er geen nodig), een
DI-registratiehelper (YAGNI tot er twee consumenten zijn).

## 2. Besluiten uit het gesprek

| Onderwerp | Besluit | Vastleggen als |
|---|---|---|
| Scope ronde 1 | Pii + Trace eerst; eval-harnas later als eigen ronde | deze spec |
| Repo-indeling | Eén publieke GitHub-repo, meerdere packages | ADR-0001 (nieuwe repo) |
| Naamgeving | Voorvoegsel `AiGuardrails.*`; repo `ai-guardrails-packages` | ADR-0001 |
| Trace-schema | Generieke kern + uitbreidingsveld (plat in JSON) | ADR-0002 |
| Package-knip | Drie packages: `Pii`, `Trace` (zonder Azure), `Trace.Azure` | ADR-0001 |
| Versionering/feed | Git-tag per package via MinVer; publish naar GitHub Packages | ADR-0003 |

## 3. Repo en structuur

Repo `github.com/jelleschut/ai-guardrails-packages`, publiek, naast sociale-kaart-rag.

```
ai-guardrails-packages/
  Directory.Build.props        net10.0, Nullable, ImplicitUsings, TreatWarningsAsErrors, LangVersion latest
  Directory.Packages.props     centraal pakketbeheer (ManagePackageVersionsCentrally)
  global.json                  sdk 10.0.204, rollForward latestFeature (zoals bronrepo)
  AiGuardrails.slnx
  src/AiGuardrails.Pii/
  src/AiGuardrails.Trace/
  src/AiGuardrails.Trace.Azure/
  tests/AiGuardrails.Pii.Tests/
  tests/AiGuardrails.Trace.Tests/
  tests/AiGuardrails.Trace.Azure.Tests/
  docs/adr/                    0001 repo+naam+knip, 0002 trace-schema, 0003 versionering+feed
  docs/superpowers/specs|plans
  .github/workflows/ci.yml       build, test, pack (droge run), scans
  .github/workflows/release.yml  op tag: pack + push naar GitHub Packages
  README.md, LICENSE (zelfde licentie als bronrepo)
```

Package-metadata in `Directory.Build.props` voor de `src/`-projecten: auteur, licentie-expressie,
repository-URL, `PublishRepositoryUrl`, `EmbedUntrackedSources`, `Deterministic`,
`ContinuousIntegrationBuild` in CI, SourceLink voor GitHub, `GenerateDocumentationFile`.
Testprojecten zetten `IsPackable=false`.

Geen Terraform en geen `iac`-job: deze ronde maakt geen Azure-resources. De budgetvraag uit
de kickoff-brief (€ 25 subscription-scoped) komt terug in de gateway-ronde.

## 4. AiGuardrails.Pii

Verhuist ongewijzigd uit `src/Core/Policy/PiiFilter.cs`; alleen de namespace wordt
`AiGuardrails.Pii`. Publieke API:

```csharp
public sealed record PiiResult(string Text, bool Redacted, IReadOnlyList<string> Types);
public static partial class PiiFilter
{
    public static PiiResult Redact(string input);   // vervangt door [email] [bsn] [address] [phone]
    public static bool IsValidBsn(string digits);   // 11-proef
}
```

Gedrag, volgorde (e-mail → BSN → adres → telefoon) en de regexen blijven byte-voor-byte gelijk;
dat is de garantie dat v2 later identiek redigeert. De typenamen (`email`, `bsn`, `address`,
`phone`) zijn het contract met het trace-veld `piiTypes`.

README benoemt expliciet: dit is een **Nederlandse** regex-PII-filter (BSN met 11-proef,
e-mail, NL-telefoon, postcode+huisnummer); geen NER, geen internationale nummers, en de
bekende afweging uit de bronrepo (grofmazige postcode blijft staan).

Tests: `tests/Core.Tests/PiiFilterTests.cs` verhuist één-op-één (13 testmethoden, incl. theories).

## 5. AiGuardrails.Trace (zonder Azure-afhankelijkheden)

Afhankelijkheden: alleen `Microsoft.Extensions.Logging.Abstractions` (voor de composite sink).

### 5.1 TraceRecord — generieke kern + uitbreidingen

```csharp
namespace AiGuardrails.Trace;

public sealed record ToolCall(string Name, string ArgumentsHash, int ResultCount);

public sealed record TraceRecord
{
    public required string CorrelationId { get; init; }      // 32 lowercase hex, zie CorrelationId
    public required DateTimeOffset Timestamp { get; init; }
    public string? PolicyVersion { get; init; }               // app-semver; was gekoppeld aan Policy.PolicyVersion
    public string? Model { get; init; }
    public string? ModelVersion { get; init; }
    public string? PromptHash { get; init; }
    public bool PiiRedacted { get; init; }
    public string[] PiiTypes { get; init; } = [];
    public ToolCall[] ToolCalls { get; init; } = [];
    public int TokensIn { get; init; }
    public int TokensOut { get; init; }
    public int TokensCached { get; init; }
    public double EstimatedCostEur { get; init; }
    public long LatencyMs { get; init; }
    public string Outcome { get; init; } = Outcomes.Error;    // open vocabulaire, snake_case
    public string? RefusalReason { get; init; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; init; }

    public static TraceRecord Start(string correlationId);
    public static JsonSerializerOptions JsonOptions { get; }   // Web-defaults, null weglaten; read-only (MakeReadOnly)
    public T? GetExtension<T>(string name);
    public TraceRecord WithExtension<T>(string name, T value);  // kernveldnamen geweigerd (hoofdletterongevoelig); null verwijdert
}

public static class Outcomes { public const string Answered = "answered", Error = "error"; }
```

Ontwerpkeuzes:

- **Uitkomst is een string**, niet meer de enum met `RefusedMedical`/`RefusedScope`. De
  package definieert alleen `answered` en `error`; een app (of de gateway: `quota_exceeded`,
  `pii_blocked`) voegt eigen waarden toe. Het JSON-formaat blijft gelijk aan de huidige
  snake_case-enumserialisatie, dus bestaande regels lezen in.
- **Extensions via `[JsonExtensionData]`** houdt app-velden (`intent`, `domain`,
  `retrievedChunkIds`, `retrievedScores`) plat op het hoogste niveau in de JSON. Daardoor
  blijven `docs/traceability.md`, het inzicht-paneel en de eval-scoring van sociale-kaart-rag
  geldig zonder migratie. `GetExtension`/`WithExtension` zijn de typed-toegang; onbekende
  velden gaan niet verloren en gooien niet.
- **PolicyVersion wordt een gewone string** zonder default; de koppeling aan
  `Policy.PolicyVersion.Current` was app-kennis.
- Het record bevat nooit vraag- of antwoordtekst; alleen hashes. Dat blijft de regel en staat
  in de README als contract.

### 5.2 Sinks en correlatie-id

```csharp
public interface ITraceSink   { Task WriteAsync(TraceRecord record, CancellationToken ct = default); }
public interface ITraceReader { Task<TraceRecord?> ReadAsync(string correlationId, CancellationToken ct = default); }
public sealed class CompositeTraceSink(IEnumerable<ITraceSink> sinks, ILogger<CompositeTraceSink> log) : ITraceSink;
public static class CorrelationId { public static string New(); public static bool IsValid(string? id); public static void EnsureValid(string? id, string paramName = "correlationId"); }
```

`CorrelationId.New()` geeft `Guid.NewGuid().ToString("n")` (zoals `AskEndpoint` nu doet);
`IsValid` is de 32-lowercase-hex-regel die nu in `BlobTraceSink` zit. Beide horen in de kern,
want de app genereert en de sink valideert. Contract: sinks gooien niet; de composite sink logt
en gaat door (huidig gedrag, huidige test verhuist mee).

### 5.3 CostEstimator met instelbare prijstabel

```csharp
public sealed record ModelPrice(string ModelPrefix, double UsdPer1MIn, double UsdPer1MOut, double CachedInputFactor = 0.5);
public sealed class CostEstimator(IReadOnlyList<ModelPrice> prices, string defaultModelPrefix, double usdToEur = 0.92)
{
    public static CostEstimator Default { get; }   // huidige tabel: gpt-4.1-mini, text-embedding-3-small
    public double EstimateEur(string? model, int tokensIn, int tokensOut, int tokensCached);   // null/onbekend model → default; negatieve aantallen = 0
}
```

Zelfde rekenregel en afronding als nu (raming, geen factuur). De gateway krijgt zo per
logische modelnaam een eigen tabel zonder package-release. `Default` bewaart het huidige gedrag
voor v2.

## 6. AiGuardrails.Trace.Azure

Afhankelijkheden: `AiGuardrails.Trace`, `Azure.Storage.Blobs`, `Microsoft.ApplicationInsights`.

```csharp
public sealed record BlobTraceSinkOptions(string ContainerName = "traces", string PartitionFormat = "yyyy'/'MM'/'dd");   // partitie in UTC; fijner bij >50.000 traces/dag (append-blob-plafond)
public sealed class BlobTraceSink(BlobServiceClient blobs, BlobTraceSinkOptions? options = null) : ITraceSink, ITraceReader;

public sealed record AppInsightsTraceSinkOptions(string EventName = "ai.request", string MetricPrefix = "ai");
public sealed class AppInsightsTraceSink(TelemetryClient telemetry, AppInsightsTraceSinkOptions? options = null) : ITraceSink;
```

- **BlobTraceSink**: gedrag ongewijzigd (één JSON-regel per request in een dag-append-blob
  `yyyy/MM/dd.jsonl`, plus `by-id/{correlationId}.json`); containernaam wordt een optie.
  Id-validatie gebruikt `CorrelationId.IsValid` uit de kern.
- **AppInsightsTraceSink**: kernvelden als properties zoals nu; daarnaast **alle
  uitbreidingsvelden** als string-property onder hun eigen naam (`intent`, `domain`, …) en
  voor array-uitbreidingen de lengte als `{naam}Count` (dus `retrievedChunkIdsCount` in plaats
  van het huidige `retrieved`). v2 past zijn query's daarop aan; dat is een v2-besluit.
  Metrics `{prefix}.estimatedCostEur`, `.latencyMs`, `.tokensIn`, `.tokensOut`. v2 geeft
  `EventName = "rag.request"`, `MetricPrefix = "rag"` mee om zijn dashboards intact te houden.

## 7. CI en release

**ci.yml** (push main + PR; `permissions: contents: read`; alle actions SHA-gepind zoals de
bronrepo): `dotnet restore/build -warnaserror/test` met trx-artefact, `dotnet pack` als droge
run (bewijst dat de packages packen, incl. README in het package), en de scans gitleaks,
semgrep (`p/csharp`, `p/secrets`, `p/github-actions`) en trivy fs (HIGH/CRITICAL). Geen
checkov: geen IaC.

**release.yml** (`on: push: tags: ['pii/v*', 'trace/v*', 'trace-azure/v*']`;
`permissions: contents: read, packages: write`): MinVer per project met `MinVerTagPrefix`
(`pii/v`, `trace/v`, `trace-azure/v`), `dotnet pack` van alleen het project dat bij de tag
hoort, `dotnet nuget push` naar `https://nuget.pkg.github.com/jelleschut/index.json` met
`GITHUB_TOKEN`. Ongetagde builds krijgen van MinVer een pre-release-versie en worden niet
gepusht.

Afhankelijkheidsregel: `Trace.Azure` verwijst in de repo via `ProjectReference` naar `Trace`;
NuGet maakt daar bij pack een package-dependency van op de MinVer-versie van dat moment.
Consequentie (ADR-0003): een `Trace`-wijziging die `Trace.Azure` raakt vraagt twee tags, eerst
`trace/v`, dan `trace-azure/v`.

Bekende beperking (ADR-0003): GitHub Packages vereist ook voor publieke packages een token om
te **lezen**. In GitHub Actions volstaat `GITHUB_TOKEN`; lokaal is een PAT met `read:packages`
nodig in een `nuget.config`-bron. nuget.org is het alternatief als dat gaat schuren; de
package-id's zijn daar nog vrij te claimen.

## 8. Tests

| Package | Verhuist | Nieuw |
|---|---|---|
| Pii | `PiiFilterTests` (13) | — |
| Trace | serialisatie zonder tekst, round-trip, kostenraming (theory), composite sink gaat door bij fout | round-trip mét extensions (typed get/with); **compatibiliteitstest**: een letterlijke trace-regel uit sociale-kaart-rag (met `intent`, `domain`, `retrievedChunkIds`, `outcome: "refused_medical"`) deserialiseert zonder verlies en serialiseert terug naar een JSON-document dat semantisch gelijk is (`JsonNode.DeepEquals`; veldvolgorde mag verschillen); `CorrelationId.IsValid`-randgevallen; `CostEstimator` met eigen tabel en onbekend model → default |
| Trace.Azure | — | `BlobTraceSink` weigert ongeldig id (geen netwerk nodig); `AppInsightsTraceSink` mapping via `TelemetryConfiguration` met in-memory channel: kernvelden, extension-strings, array-telling, metrics onder prefix |

`SplitModel` (model/versie-splitsing) blijft in de orchestrator van de bronrepo; hij hoort bij de
modelaanroep, niet bij het trace-schema.

## 9. Documentatie in de nieuwe repo

- README: wat elk package doet, één voorbeeld per package, het "nooit tekst in een trace"-
  contract, de token-eis van GitHub Packages, de tagconventie.
- ADR-0001 repo-indeling, naamgeving en package-knip; ADR-0002 trace-schema (kern +
  extensions, waarom string-uitkomst, waarom `JsonExtensionData`); ADR-0003 versionering en feed.
- `docs/trace-schema.md`: veld-voor-veld, afgeleid van `docs/traceability.md` uit de bronrepo,
  met de scheiding kern/extensie gemarkeerd.

## 9a. Afwijkingen tijdens de uitvoering (02-09-2026)

Uit de reviews tijdens de implementatie, verwerkt in de code en de ADR's van de nieuwe repo:

- `JsonOptions` is een read-only property met expliciete `DefaultJsonTypeInfoResolver` (een gedeeld
  muteerbaar profiel in een package is onveilig; `MakeReadOnly(populateMissingResolver: true)` faalt
  onder trimming al bij het laden van het type).
- `WithExtension` weigert kernveldnamen (hoofdletterongevoelig), anders ontstaat een dubbele
  JSON-sleutel die het kernveld bij teruglezen overschrijft. `null` verwijdert het veld.
- `CostEstimator` en `CorrelationId.IsValid` zijn null-veilig; gecachte tokens worden begrensd
  op `tokensIn` (bewuste afwijking van het origineel, identiek resultaat voor de eval-cases).
- `CompositeTraceSink` stopt stil bij een geannuleerd token (geen LogError, geen throw).
- `BlobTraceSink` partitioneert in UTC en heeft een `PartitionFormat`-optie vanwege het
  Azure-plafond van 50.000 blokken per append-blob; `AppInsightsTraceSink` zet ook `promptHash`,
  laat kernvelden winnen bij een naamconflict met een uitbreiding, en is null-veilig voor
  legacy-regels met expliciete nulls.
- Symbolen: PDB embedded in de dll (`DebugType=embedded`) i.p.v. een `.snupkg`; GitHub Packages
  heeft geen symbol server.
- `Microsoft.ApplicationInsights` 3.x is een laag over OpenTelemetry zonder `ITelemetryChannel`;
  de sink-tests vangen events en metrics via OpenTelemetry-processors/-exporters op.
- Testprojecten delen hun xunit-inrichting via `tests/Directory.Build.props`.

## 10. Relatie met sociale-kaart-rag

Deze repo verandert in ronde 1 niet, behalve het committen van de kickoff-brief en deze spec
op een docs-branch. De extractie is kopiëren, niet verplaatsen: sociale-kaart-rag consumeert de
packages pas in stap 3 (v2-fork). De compatibiliteitstest in §8 is de brug tussen beide.

Nieuwe repo-map onder `source\repos` valt onder de repo-access-classificatie; toegang wordt bij
aanmaken vastgelegd (kickoff-brief, werkwijze punt 3).

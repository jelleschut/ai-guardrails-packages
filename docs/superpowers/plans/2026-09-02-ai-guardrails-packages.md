# AiGuardrails-packages (ronde 1: Pii + Trace) — implementatieplan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Een nieuwe publieke repo `ai-guardrails-packages` met drie NuGet-packages (`AiGuardrails.Pii`, `AiGuardrails.Trace`, `AiGuardrails.Trace.Azure`), geëxtraheerd uit sociale-kaart-rag, met tests, CI-scans en tag-per-package-release naar GitHub Packages.

**Architecture:** Drie kleine class libraries in één repo. `Pii` is een pure regex-redactor. `Trace` levert het generieke trace-schema (kern + `[JsonExtensionData]`), de sink-interfaces, de composite sink, de correlatie-id-regels en een kostenschatter met instelbare prijstabel; het heeft geen Azure-afhankelijkheden. `Trace.Azure` levert de Blob- en App Insights-sinks. MinVer leidt de versie per package af uit git-tags met een eigen prefix.

**Tech Stack:** .NET 10 (SDK 10.0.204), xUnit 2.9.3, MinVer 7.0.0, Azure.Storage.Blobs 12.29.2, Microsoft.ApplicationInsights 3.1.2, GitHub Actions (SHA-gepind), GitHub Packages (NuGet).

**Spec:** `docs/superpowers/specs/2026-09-02-ai-guardrails-packages-design.md` (in sociale-kaart-rag; wordt in taak 1 meegekopieerd).

**Bronrepo:** `C:\Users\JelleSchut\source\repos\sociale-kaart-rag` — read-only. Alles wordt gekopieerd, niets verplaatst.

**Doelrepo:** `C:\Users\JelleSchut\source\repos\ai-guardrails-packages` (bestaat nog niet; taak 1 maakt hem).

**Werkwijze git:** taak 1 pusht het skelet naar `main`. Taken 2–10 committen op branch `feat/packages-v0`. Taak 11 opent de PR, wacht op groene CI, merget, zet branch protection en tagt de eerste releases.

---

## Bestandsoverzicht

| Pad | Verantwoordelijkheid |
|---|---|
| `Directory.Build.props` | gedeelde compiler-instellingen (net10.0, nullable, warnings als errors) |
| `Directory.Packages.props` | centrale package-versies |
| `src/Directory.Build.props` | package-metadata, SourceLink, MinVer — alleen voor de `src/`-projecten |
| `nuget.config` | alleen nuget.org als bron (isoleert van globale feeds op de machine) |
| `src/AiGuardrails.Pii/PiiFilter.cs` | regex-redactie NL-PII, ongewijzigd uit bronrepo |
| `src/AiGuardrails.Trace/TraceRecord.cs` | kernrecord, `ToolCall`, `Outcomes`, JSON-opties, extension-helpers |
| `src/AiGuardrails.Trace/CorrelationId.cs` | genereren + valideren van het id |
| `src/AiGuardrails.Trace/ITraceSink.cs` | `ITraceSink`, `ITraceReader` |
| `src/AiGuardrails.Trace/CompositeTraceSink.cs` | schrijft naar alle sinks, gooit nooit |
| `src/AiGuardrails.Trace/CostEstimator.cs` | `ModelPrice`, `CostEstimator` met `Default` |
| `src/AiGuardrails.Trace.Azure/BlobTraceSink.cs` | append-blob per dag + blob per id |
| `src/AiGuardrails.Trace.Azure/AppInsightsTraceSink.cs` | event + metrics, extensions als properties |
| `tests/AiGuardrails.Pii.Tests/PiiFilterTests.cs` | 13 testmethoden uit bronrepo |
| `tests/AiGuardrails.Trace.Tests/TraceRecordTests.cs` | serialisatie, round-trip, extensions, compatibiliteit |
| `tests/AiGuardrails.Trace.Tests/CorrelationIdTests.cs` | id-regels |
| `tests/AiGuardrails.Trace.Tests/CompositeTraceSinkTests.cs` | doorgaan bij fout |
| `tests/AiGuardrails.Trace.Tests/CostEstimatorTests.cs` | tarieven, cache, onbekend model, eigen tabel |
| `tests/AiGuardrails.Trace.Azure.Tests/BlobTraceSinkTests.cs` | id-validatie zonder netwerk |
| `tests/AiGuardrails.Trace.Azure.Tests/AppInsightsTraceSinkTests.cs` | property- en metric-mapping via in-memory channel |
| `.github/workflows/ci.yml` | build, test, pack-droge-run, scans |
| `.github/workflows/release.yml` | pack + push op tag |
| `docs/adr/0001..0003` | besluiten uit de spec |
| `docs/trace-schema.md` | veld-voor-veld |
| `README.md` + `src/*/README.md` | repo-README en package-README's |

---

### Taak 1: Repo-skelet en GitHub-repo

**Files:**
- Create: `.gitignore`, `LICENSE`, `global.json`, `nuget.config`, `Directory.Build.props`, `Directory.Packages.props`, `src/Directory.Build.props`, `AiGuardrails.slnx`, `README.md`
- Copy: spec en dit plan naar `docs/superpowers/specs/` en `docs/superpowers/plans/`

> **Voor de orchestrator (niet voor een subagent):** de nieuwe map valt onder de repo-access-classificatie. Vraag de gebruiker na stap 1 om toegang vast te leggen met
> `pwsh -NoProfile -File "C:\Users\JelleSchut\.claude-thuis\plugins\cache\ai-home\repo-access\0.1.0\scripts\grant.ps1" -Repo "ai-guardrails-packages" -Decision allow`
> Zonder die stap vraagt elke tool-aanroep in de nieuwe map om toestemming.

- [ ] **Stap 1: Map en git aanmaken**

```powershell
New-Item -ItemType Directory -Force C:\Users\JelleSchut\source\repos\ai-guardrails-packages
Set-Location C:\Users\JelleSchut\source\repos\ai-guardrails-packages
git init -b main
```

- [ ] **Stap 2: `.gitignore`**

```
bin/
obj/
*.user
.vs/
TestResults/
artifacts/
```

- [ ] **Stap 3: `LICENSE`** — kopieer letterlijk uit de bronrepo:

```powershell
Copy-Item C:\Users\JelleSchut\source\repos\sociale-kaart-rag\LICENSE .\LICENSE
```

- [ ] **Stap 4: `global.json`**

```json
{ "sdk": { "version": "10.0.204", "rollForward": "latestFeature" } }
```

- [ ] **Stap 5: `nuget.config`**

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

- [ ] **Stap 6: `Directory.Build.props`** (root)

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <LangVersion>latest</LangVersion>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
</Project>
```

- [ ] **Stap 7: `Directory.Packages.props`** (root)

```xml
<Project>
  <ItemGroup>
    <PackageVersion Include="Azure.Storage.Blobs" Version="12.29.2" />
    <PackageVersion Include="coverlet.collector" Version="6.0.4" />
    <PackageVersion Include="Microsoft.ApplicationInsights" Version="3.1.2" />
    <PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.11" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageVersion Include="MinVer" Version="7.0.0" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>
</Project>
```

- [ ] **Stap 8: `src/Directory.Build.props`** — importeert de root-props en voegt package-metadata toe. SourceLink voor GitHub zit sinds .NET 8 in de SDK; `PublishRepositoryUrl` volstaat.

```xml
<Project>
  <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />

  <PropertyGroup>
    <Authors>Jelle Schut</Authors>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/jelleschut/ai-guardrails-packages</PackageProjectUrl>
    <RepositoryUrl>https://github.com/jelleschut/ai-guardrails-packages</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
    <Deterministic>true</Deterministic>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <!-- CS1591 (ontbrekende XML-doc) niet als error: samenvattingen staan waar ze iets toevoegen, niet op elke property -->
    <NoWarn>$(NoWarn);CS1591</NoWarn>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <MinVerDefaultPreReleaseIdentifiers>preview.0</MinVerDefaultPreReleaseIdentifiers>
  </PropertyGroup>

  <PropertyGroup Condition="'$(GITHUB_ACTIONS)' == 'true'">
    <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MinVer" PrivateAssets="all" />
    <None Include="README.md" Pack="true" PackagePath="\" />
  </ItemGroup>
</Project>
```

- [ ] **Stap 9: `AiGuardrails.slnx`**

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/AiGuardrails.Pii/AiGuardrails.Pii.csproj" />
    <Project Path="src/AiGuardrails.Trace/AiGuardrails.Trace.csproj" />
    <Project Path="src/AiGuardrails.Trace.Azure/AiGuardrails.Trace.Azure.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/AiGuardrails.Pii.Tests/AiGuardrails.Pii.Tests.csproj" />
    <Project Path="tests/AiGuardrails.Trace.Tests/AiGuardrails.Trace.Tests.csproj" />
    <Project Path="tests/AiGuardrails.Trace.Azure.Tests/AiGuardrails.Trace.Azure.Tests.csproj" />
  </Folder>
</Solution>
```

De projecten bestaan nog niet; `dotnet build` op de solution faalt tot taak 7. Bouw in taken 2–7 per project.

- [ ] **Stap 10: `README.md`** (voorlopig; taak 10 vervangt hem)

```markdown
# ai-guardrails-packages

Herbruikbare AI-waarborgen als NuGet-packages, geëxtraheerd uit
[sociale-kaart-rag](https://github.com/jelleschut/sociale-kaart-rag).
In aanbouw — zie `docs/superpowers/specs/`.
```

- [ ] **Stap 11: Spec en plan kopiëren**

```powershell
New-Item -ItemType Directory -Force docs\superpowers\specs, docs\superpowers\plans, docs\adr
Copy-Item C:\Users\JelleSchut\source\repos\sociale-kaart-rag\docs\superpowers\specs\2026-09-02-ai-guardrails-packages-design.md docs\superpowers\specs\
Copy-Item C:\Users\JelleSchut\source\repos\sociale-kaart-rag\docs\superpowers\plans\2026-09-02-ai-guardrails-packages.md docs\superpowers\plans\
```

- [ ] **Stap 12: Commit en GitHub-repo aanmaken**

```powershell
git add -A
git commit -m "chore: repo-skelet (build-props, central package management, MinVer, spec + plan)"
gh repo create jelleschut/ai-guardrails-packages --public --source . --remote origin --push --description "Herbruikbare AI-waarborgen (PII-redactie, trace-schema, sinks) als NuGet-packages"
git switch -c feat/packages-v0
```

Verwacht: `https://github.com/jelleschut/ai-guardrails-packages` bestaat, `main` heeft één commit, werkbranch `feat/packages-v0` is actief.

---

### Taak 2: AiGuardrails.Pii

**Files:**
- Create: `src/AiGuardrails.Pii/AiGuardrails.Pii.csproj`, `src/AiGuardrails.Pii/PiiFilter.cs`, `src/AiGuardrails.Pii/README.md`
- Create: `tests/AiGuardrails.Pii.Tests/AiGuardrails.Pii.Tests.csproj`, `tests/AiGuardrails.Pii.Tests/PiiFilterTests.cs`

- [ ] **Stap 1: Testproject**

`tests/AiGuardrails.Pii.Tests/AiGuardrails.Pii.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\AiGuardrails.Pii\AiGuardrails.Pii.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Stap 2: Tests kopiëren en namespaces aanpassen**

```powershell
Copy-Item C:\Users\JelleSchut\source\repos\sociale-kaart-rag\tests\Core.Tests\PiiFilterTests.cs tests\AiGuardrails.Pii.Tests\PiiFilterTests.cs
```

Vervang in het gekopieerde bestand de eerste drie regels:

```csharp
using AiGuardrails.Pii;

namespace AiGuardrails.Pii.Tests;
```

De rest van het bestand (13 testmethoden: `Detects_and_redacts`, `Nine_digits_failing_elfproef_is_not_a_bsn`, `Postcode_without_house_number_is_kept`, `Clean_question_is_untouched`, `Multiple_types_are_all_reported_once`, `Elfproef`, `Email_with_bsn_like_local_part_is_redacted_as_email_without_leaking_domain`, `Compact_and_hyphenated_addresses_are_fully_redacted`, `Phone_formats_with_parentheses_and_grouping_are_redacted`, `Bsn_and_email_in_same_text_both_reported_and_nothing_leaks`, `All_zero_is_not_a_valid_bsn`, `Bsn_at_sentence_end_or_in_parentheses_is_redacted`, `Ordinary_numbers_dates_and_words_are_not_addresses`) blijft ongewijzigd.

- [ ] **Stap 3: Test draaien, verwacht falen**

Run: `dotnet test tests/AiGuardrails.Pii.Tests`
Verwacht: build-fout, het project `AiGuardrails.Pii` bestaat niet.

- [ ] **Stap 4: Package-project**

`src/AiGuardrails.Pii/AiGuardrails.Pii.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <PackageId>AiGuardrails.Pii</PackageId>
    <Description>Regex-redactie van Nederlandse PII (BSN met 11-proef, e-mail, telefoon, postcode+huisnummer) vóór een prompt het model bereikt. Logt alleen typen, nooit waarden.</Description>
    <PackageTags>pii;redaction;privacy;llm;guardrails;nl</PackageTags>
    <MinVerTagPrefix>pii/v</MinVerTagPrefix>
  </PropertyGroup>
</Project>
```

- [ ] **Stap 5: Filter kopiëren**

```powershell
Copy-Item C:\Users\JelleSchut\source\repos\sociale-kaart-rag\src\Core\Policy\PiiFilter.cs src\AiGuardrails.Pii\PiiFilter.cs
```

Wijzig alleen de namespace-regel (regel 3) in `namespace AiGuardrails.Pii;` en de summary op de klasse in:

```csharp
/// <summary>Regex-PII-filter voor Nederlandse tekst. Alleen typen worden gerapporteerd, nooit waarden.
/// Volgorde: e-mail → BSN → adres → telefoon (e-mail eerst, zodat een cijferreeks in het lokale deel niet half als BSN wordt geredigeerd).</summary>
```

Regexen, `Redact`, `IsValidBsn` en `Mark` blijven byte-voor-byte gelijk.

- [ ] **Stap 6: Package-README**

`src/AiGuardrails.Pii/README.md`:

````markdown
# AiGuardrails.Pii

Regex-redactie van **Nederlandse** PII vóór een prompt het model bereikt.

```csharp
using AiGuardrails.Pii;

var r = PiiFilter.Redact("mijn bsn is 111222333, mail jan@example.org");
// r.Text     == "mijn bsn is [bsn], mail [email]"
// r.Redacted == true
// r.Types    == ["bsn", "email"]
```

Herkent: BSN (9 cijfers, alleen als de 11-proef slaagt), e-mail, NL-telefoon (06, +31, vast),
postcode gevolgd door huisnummer. Een postcode zonder huisnummer blijft staan (grofmazige
locatie is toegestaan). Geen NER, geen internationale nummers, geen namen.

Contract: `Types` bevat alleen de typenamen `email`, `bsn`, `address`, `phone`; waarden worden
nooit gelogd of teruggegeven.
````

- [ ] **Stap 7: Tests draaien, verwacht slagen**

Run: `dotnet test tests/AiGuardrails.Pii.Tests`
Verwacht: `Passed! - Failed: 0, Passed: 31` (13 methoden, incl. theory-regels).

- [ ] **Stap 8: Commit**

```powershell
git add src/AiGuardrails.Pii tests/AiGuardrails.Pii.Tests
git commit -m "feat(pii): AiGuardrails.Pii geëxtraheerd uit sociale-kaart-rag, tests ongewijzigd mee"
```

---

### Taak 3: AiGuardrails.Trace — TraceRecord met extensions

**Files:**
- Create: `src/AiGuardrails.Trace/AiGuardrails.Trace.csproj`, `src/AiGuardrails.Trace/TraceRecord.cs`, `src/AiGuardrails.Trace/README.md`
- Create: `tests/AiGuardrails.Trace.Tests/AiGuardrails.Trace.Tests.csproj`, `tests/AiGuardrails.Trace.Tests/TraceRecordTests.cs`

- [ ] **Stap 1: Testproject**

`tests/AiGuardrails.Trace.Tests/AiGuardrails.Trace.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\AiGuardrails.Trace\AiGuardrails.Trace.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Stap 2: Falende tests schrijven**

`tests/AiGuardrails.Trace.Tests/TraceRecordTests.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using AiGuardrails.Trace;

namespace AiGuardrails.Trace.Tests;

public class TraceRecordTests
{
    [Fact]
    public void Serializes_camel_case_without_nulls_and_without_question_or_answer_text()
    {
        var t = TraceRecord.Start("3f2a9c1e5b7d4e8f9a0b1c2d3e4f5a6b") with
        {
            PolicyVersion = "1.1.0", PiiRedacted = true, PiiTypes = ["bsn"],
            Outcome = "refused_medical", ToolCalls = [new ToolCall("search_social_map", "abc", 1)],
        };
        var json = JsonSerializer.Serialize(t, TraceRecord.JsonOptions);

        Assert.Contains("\"correlationId\":\"3f2a9c1e5b7d4e8f9a0b1c2d3e4f5a6b\"", json);
        Assert.Contains("\"policyVersion\":\"1.1.0\"", json);
        Assert.Contains("\"outcome\":\"refused_medical\"", json);
        Assert.Contains("\"piiTypes\":[\"bsn\"]", json);
        Assert.Contains("\"toolCalls\":[{\"name\":\"search_social_map\",\"argumentsHash\":\"abc\",\"resultCount\":1}]", json);
        Assert.DoesNotContain("question", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("answerText", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"model\"", json);       // null weggelaten
        Assert.DoesNotContain("\"extensions\"", json);  // extension-data is plat, nooit een eigen veld
    }

    [Fact]
    public void Default_outcome_is_error()
        => Assert.Equal(Outcomes.Error, TraceRecord.Start("c").Outcome);

    [Fact]
    public void Roundtrips_core_fields_through_json()
    {
        var t = TraceRecord.Start("x") with { Outcome = "escalated", RefusalReason = "low_retrieval_score", TokensIn = 12, LatencyMs = 34 };
        var back = JsonSerializer.Deserialize<TraceRecord>(JsonSerializer.Serialize(t, TraceRecord.JsonOptions), TraceRecord.JsonOptions)!;
        Assert.Equal("escalated", back.Outcome);
        Assert.Equal("low_retrieval_score", back.RefusalReason);
        Assert.Equal(12, back.TokensIn);
        Assert.Equal(34, back.LatencyMs);
    }

    [Fact]
    public void Extensions_are_written_flat_and_read_back_typed()
    {
        var t = TraceRecord.Start("x")
            .WithExtension("intent", "find_help")
            .WithExtension("retrievedScores", new[] { 0.9, 0.1 });
        var json = JsonSerializer.Serialize(t, TraceRecord.JsonOptions);

        Assert.Contains("\"intent\":\"find_help\"", json);
        Assert.Contains("\"retrievedScores\":[0.9,0.1]", json);

        var back = JsonSerializer.Deserialize<TraceRecord>(json, TraceRecord.JsonOptions)!;
        Assert.Equal("find_help", back.GetExtension<string>("intent"));
        Assert.Equal([0.9, 0.1], back.GetExtension<double[]>("retrievedScores"));
        Assert.Null(back.GetExtension<string>("bestaat_niet"));
    }

    [Fact]
    public void WithExtension_does_not_mutate_the_original()
    {
        var a = TraceRecord.Start("x");
        var b = a.WithExtension("intent", "find_help");
        Assert.Null(a.GetExtension<string>("intent"));
        Assert.Equal("find_help", b.GetExtension<string>("intent"));
    }

    // Letterlijke regel in het formaat dat sociale-kaart-rag (policy 1.1.0) vandaag wegschrijft.
    private const string LegacyLine =
        """
        {"correlationId":"3f2a9c1e5b7d4e8f9a0b1c2d3e4f5a6b","timestamp":"2026-08-29T10:15:30.1234567+00:00","policyVersion":"1.1.0","model":"gpt-4.1-mini","modelVersion":"2025-04-14","promptHash":"9c1e2d3f","piiRedacted":true,"piiTypes":["bsn"],"intent":"medical","domain":"zorg","toolCalls":[],"retrievedChunkIds":["osm:node/123#0"],"retrievedScores":[0.031],"tokensIn":1200,"tokensOut":80,"tokensCached":0,"estimatedCostEur":0.000559,"latencyMs":1834,"outcome":"refused_medical","refusalReason":"medical_intent"}
        """;

    [Fact]
    public void Reads_a_sociale_kaart_rag_trace_line_without_loss()
    {
        var t = JsonSerializer.Deserialize<TraceRecord>(LegacyLine, TraceRecord.JsonOptions)!;

        Assert.Equal("3f2a9c1e5b7d4e8f9a0b1c2d3e4f5a6b", t.CorrelationId);
        Assert.Equal("1.1.0", t.PolicyVersion);
        Assert.Equal("refused_medical", t.Outcome);
        Assert.Equal("medical", t.GetExtension<string>("intent"));
        Assert.Equal("zorg", t.GetExtension<string>("domain"));
        Assert.Equal(["osm:node/123#0"], t.GetExtension<string[]>("retrievedChunkIds"));
        Assert.Equal([0.031], t.GetExtension<double[]>("retrievedScores"));

        var again = JsonSerializer.Serialize(t, TraceRecord.JsonOptions);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(LegacyLine), JsonNode.Parse(again)), again);
    }
}
```

- [ ] **Stap 3: Test draaien, verwacht falen**

Run: `dotnet test tests/AiGuardrails.Trace.Tests`
Verwacht: build-fout, project `AiGuardrails.Trace` bestaat niet.

- [ ] **Stap 4: Package-project**

`src/AiGuardrails.Trace/AiGuardrails.Trace.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <PackageId>AiGuardrails.Trace</PackageId>
    <Description>Generiek trace-schema voor AI-aanroepen (één record per request, nooit vraag- of antwoordtekst), sink-interfaces, composite sink, correlatie-id-regels en een kostenschatter met instelbare prijstabel. Zonder Azure-afhankelijkheden.</Description>
    <PackageTags>tracing;observability;llm;guardrails;cost</PackageTags>
    <MinVerTagPrefix>trace/v</MinVerTagPrefix>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
  </ItemGroup>
</Project>
```

- [ ] **Stap 5: TraceRecord implementeren**

`src/AiGuardrails.Trace/TraceRecord.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiGuardrails.Trace;

/// <summary>Eén tool-aanroep binnen een request; alleen naam, hash van de argumenten en aantal resultaten.</summary>
public sealed record ToolCall(string Name, string ArgumentsHash, int ResultCount);

/// <summary>Uitkomsten die het package zelf kent. Een app of gateway voegt eigen snake_case-waarden toe
/// (bijv. "refused_medical", "quota_exceeded").</summary>
public static class Outcomes
{
    public const string Answered = "answered";
    public const string Error = "error";
}

/// <summary>Eén record per request. Bevat nooit vraag- of antwoordtekst; wel hashes.
/// App-specifieke velden gaan via <see cref="WithExtension{T}"/> en staan plat in de JSON naast de kernvelden.</summary>
public sealed record TraceRecord
{
    public required string CorrelationId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    /// <summary>Semver van prompt + drempels van de aanroepende app; de app bepaalt de waarde.</summary>
    public string? PolicyVersion { get; init; }
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
    /// <summary>Open vocabulaire in snake_case; zie <see cref="Outcomes"/>.</summary>
    public string Outcome { get; init; } = Outcomes.Error;
    public string? RefusalReason { get; init; }

    /// <summary>App-specifieke velden. Serialiseert plat (geen "extensions"-veld); onbekende velden bij inlezen komen hier terecht.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; init; }

    public static TraceRecord Start(string correlationId) => new() { CorrelationId = correlationId, Timestamp = DateTimeOffset.UtcNow };

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public T? GetExtension<T>(string name)
        => Extensions is not null && Extensions.TryGetValue(name, out var el) ? el.Deserialize<T>(JsonOptions) : default;

    public TraceRecord WithExtension<T>(string name, T value)
    {
        var copy = Extensions is null ? new Dictionary<string, JsonElement>() : new Dictionary<string, JsonElement>(Extensions);
        copy[name] = JsonSerializer.SerializeToElement(value, JsonOptions);
        return this with { Extensions = copy };
    }
}
```

- [ ] **Stap 6: Package-README** (wordt in taak 5 aangevuld met de kostenschatter)

`src/AiGuardrails.Trace/README.md`:

````markdown
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
````

- [ ] **Stap 7: Tests draaien, verwacht slagen**

Run: `dotnet test tests/AiGuardrails.Trace.Tests`
Verwacht: `Passed! - Failed: 0, Passed: 6`.

- [ ] **Stap 8: Commit**

```powershell
git add src/AiGuardrails.Trace tests/AiGuardrails.Trace.Tests
git commit -m "feat(trace): TraceRecord als generieke kern + JsonExtensionData, compatibel met sociale-kaart-rag-traces"
```

---

### Taak 4: CorrelationId, sink-interfaces en CompositeTraceSink

**Files:**
- Create: `src/AiGuardrails.Trace/CorrelationId.cs`, `src/AiGuardrails.Trace/ITraceSink.cs`, `src/AiGuardrails.Trace/CompositeTraceSink.cs`
- Create: `tests/AiGuardrails.Trace.Tests/CorrelationIdTests.cs`, `tests/AiGuardrails.Trace.Tests/CompositeTraceSinkTests.cs`

- [ ] **Stap 1: Falende tests**

`tests/AiGuardrails.Trace.Tests/CorrelationIdTests.cs`:

```csharp
using AiGuardrails.Trace;

namespace AiGuardrails.Trace.Tests;

public class CorrelationIdTests
{
    [Fact]
    public void New_is_32_lowercase_hex_and_unique()
    {
        var a = CorrelationId.New();
        var b = CorrelationId.New();
        Assert.True(CorrelationId.IsValid(a));
        Assert.NotEqual(a, b);
    }

    [Theory]
    [InlineData("3f2a9c1e5b7d4e8f9a0b1c2d3e4f5a6b", true)]
    [InlineData("3F2A9C1E5B7D4E8F9A0B1C2D3E4F5A6B", false)]   // hoofdletters
    [InlineData("3f2a9c1e5b7d4e8f9a0b1c2d3e4f5a6", false)]    // 31 tekens
    [InlineData("3f2a9c1e-5b7d-4e8f-9a0b-1c2d3e4f5a6b", false)] // guid met streepjes
    [InlineData("../by-id/x", false)]
    [InlineData("", false)]
    public void IsValid(string id, bool expected) => Assert.Equal(expected, CorrelationId.IsValid(id));
}
```

`tests/AiGuardrails.Trace.Tests/CompositeTraceSinkTests.cs`:

```csharp
using AiGuardrails.Trace;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiGuardrails.Trace.Tests;

public class CompositeTraceSinkTests
{
    [Fact]
    public async Task Keeps_going_when_one_sink_throws()
    {
        var ok = new MemorySink();
        var composite = new CompositeTraceSink([new ThrowingSink(), ok], NullLogger<CompositeTraceSink>.Instance);
        await composite.WriteAsync(TraceRecord.Start("c"));
        Assert.Equal("c", ok.Last!.CorrelationId);
    }

    [Fact]
    public async Task Writes_to_every_sink_in_order()
    {
        var a = new MemorySink();
        var b = new MemorySink();
        var composite = new CompositeTraceSink([a, b], NullLogger<CompositeTraceSink>.Instance);
        await composite.WriteAsync(TraceRecord.Start("c"));
        Assert.Equal("c", a.Last!.CorrelationId);
        Assert.Equal("c", b.Last!.CorrelationId);
    }

    private sealed class ThrowingSink : ITraceSink
    {
        public Task WriteAsync(TraceRecord r, CancellationToken ct = default) => throw new InvalidOperationException("boom");
    }

    private sealed class MemorySink : ITraceSink
    {
        public TraceRecord? Last;
        public Task WriteAsync(TraceRecord r, CancellationToken ct = default) { Last = r; return Task.CompletedTask; }
    }
}
```

- [ ] **Stap 2: Test draaien, verwacht falen**

Run: `dotnet test tests/AiGuardrails.Trace.Tests`
Verwacht: build-fout, `CorrelationId`, `ITraceSink`, `CompositeTraceSink` bestaan niet.

- [ ] **Stap 3: Implementatie**

`src/AiGuardrails.Trace/CorrelationId.cs`:

```csharp
namespace AiGuardrails.Trace;

/// <summary>Het correlatie-id vormt bij sinks de blob-/bestandsnaam; daarom alleen 32 lowercase hex-tekens,
/// zodat een aanroeper nooit een ander object kan raken.</summary>
public static class CorrelationId
{
    public static string New() => Guid.NewGuid().ToString("n");

    public static bool IsValid(string id) => id.Length == 32 && id.All(char.IsAsciiHexDigitLower);

    /// <summary>Gooit <see cref="ArgumentException"/> als het id niet aan het formaat voldoet.</summary>
    public static void EnsureValid(string id, string paramName = "correlationId")
    {
        if (!IsValid(id)) throw new ArgumentException("correlationId moet 32 lowercase hex-tekens zijn.", paramName);
    }
}
```

`src/AiGuardrails.Trace/ITraceSink.cs`:

```csharp
namespace AiGuardrails.Trace;

/// <summary>Contract: implementaties horen niet te gooien; <see cref="CompositeTraceSink"/> vangt als vangrail toch af.</summary>
public interface ITraceSink
{
    Task WriteAsync(TraceRecord record, CancellationToken ct = default);
}

public interface ITraceReader
{
    Task<TraceRecord?> ReadAsync(string correlationId, CancellationToken ct = default);
}
```

`src/AiGuardrails.Trace/CompositeTraceSink.cs`:

```csharp
using Microsoft.Extensions.Logging;

namespace AiGuardrails.Trace;

/// <summary>Schrijft naar alle sinks, in volgorde; een falende sink mag het antwoord aan de gebruiker niet blokkeren.</summary>
public sealed class CompositeTraceSink(IEnumerable<ITraceSink> sinks, ILogger<CompositeTraceSink> log) : ITraceSink
{
    public async Task WriteAsync(TraceRecord record, CancellationToken ct = default)
    {
        foreach (var s in sinks)
        {
            try { await s.WriteAsync(record, ct); }
            catch (Exception ex) { log.LogError(ex, "trace-sink {Sink} faalde voor {CorrelationId}", s.GetType().Name, record.CorrelationId); }
        }
    }
}
```

- [ ] **Stap 4: Tests draaien, verwacht slagen**

Run: `dotnet test tests/AiGuardrails.Trace.Tests`
Verwacht: `Passed! - Failed: 0, Passed: 15`.

- [ ] **Stap 5: Commit**

```powershell
git add src/AiGuardrails.Trace tests/AiGuardrails.Trace.Tests
git commit -m "feat(trace): CorrelationId, ITraceSink/ITraceReader en CompositeTraceSink"
```

---

### Taak 5: CostEstimator met instelbare prijstabel

**Files:**
- Create: `src/AiGuardrails.Trace/CostEstimator.cs`
- Create: `tests/AiGuardrails.Trace.Tests/CostEstimatorTests.cs`

- [ ] **Stap 1: Falende tests**

`tests/AiGuardrails.Trace.Tests/CostEstimatorTests.cs`:

```csharp
using AiGuardrails.Trace;

namespace AiGuardrails.Trace.Tests;

public class CostEstimatorTests
{
    // Zelfde verwachtingen als sociale-kaart-rag: $0.40/M in, $1.60/M uit, koers 0,92, cache 50 %.
    [Theory]
    [InlineData("gpt-4.1-mini", 1000, 500, 0, 0.001104)]
    [InlineData("gpt-4.1-mini", 1000, 0, 1000, 0.000184)]           // volledig gecachte input
    [InlineData("gpt-4.1-mini-2025-04-14", 1000, 500, 0, 0.001104)]  // versie-suffix, prefix-match
    [InlineData("text-embedding-3-small", 1000000, 0, 0, 0.0184)]
    [InlineData("unknown-model", 1000, 500, 0, 0.001104)]            // valt terug op default
    public void Default_table_matches_sociale_kaart_rag(string model, int tokensIn, int tokensOut, int cached, double expectedEur)
        => Assert.Equal(expectedEur, CostEstimator.Default.EstimateEur(model, tokensIn, tokensOut, cached), 6);

    [Fact]
    public void Custom_table_and_rate_are_used()
    {
        var est = new CostEstimator([new ModelPrice("chat-default", 1.00, 2.00)], "chat-default", usdToEur: 1.0);
        Assert.Equal(0.002, est.EstimateEur("chat-default", 1000, 500, 0), 6);   // (1000*1 + 500*2)/1M
    }

    [Fact]
    public void Cached_tokens_never_exceed_input()
    {
        var est = new CostEstimator([new ModelPrice("m", 1.00, 0)], "m", usdToEur: 1.0);
        Assert.Equal(0.0005, est.EstimateEur("m", 1000, 0, 5000), 6);           // alle 1000 gecachet à 50 %
    }

    [Fact]
    public void Unknown_default_prefix_throws()
        => Assert.Throws<ArgumentException>(() => new CostEstimator([new ModelPrice("a", 1, 1)], "b"));
}
```

- [ ] **Stap 2: Test draaien, verwacht falen**

Run: `dotnet test tests/AiGuardrails.Trace.Tests`
Verwacht: build-fout, `CostEstimator` en `ModelPrice` bestaan niet.

- [ ] **Stap 3: Implementatie**

`src/AiGuardrails.Trace/CostEstimator.cs`:

```csharp
namespace AiGuardrails.Trace;

/// <summary>Lijstprijs in USD per 1M tokens voor modellen waarvan de naam met <paramref name="ModelPrefix"/> begint.</summary>
public sealed record ModelPrice(string ModelPrefix, double UsdPer1MIn, double UsdPer1MOut, double CachedInputFactor = 0.5);

/// <summary>Raming, geen factuur. Prefix-match op modelnaam (zodat "gpt-4.1-mini-2025-04-14" het tarief van "gpt-4.1-mini" krijgt);
/// onbekend model valt terug op <c>defaultModelPrefix</c>.</summary>
public sealed class CostEstimator
{
    private readonly IReadOnlyList<ModelPrice> _prices;
    private readonly ModelPrice _default;
    private readonly double _usdToEur;

    public CostEstimator(IReadOnlyList<ModelPrice> prices, string defaultModelPrefix, double usdToEur = 0.92)
    {
        _prices = prices;
        _default = prices.FirstOrDefault(p => p.ModelPrefix == defaultModelPrefix)
            ?? throw new ArgumentException($"defaultModelPrefix '{defaultModelPrefix}' staat niet in de prijstabel.", nameof(defaultModelPrefix));
        _usdToEur = usdToEur;
    }

    /// <summary>Azure OpenAI-lijstprijs aug 2026, vaste koers 0,92 €/$ — de tabel van sociale-kaart-rag.</summary>
    public static CostEstimator Default { get; } = new(
        [
            new ModelPrice("gpt-4.1-mini", 0.40, 1.60),
            new ModelPrice("text-embedding-3-small", 0.02, 0),
        ],
        "gpt-4.1-mini");

    public double EstimateEur(string model, int tokensIn, int tokensOut, int tokensCached)
    {
        var price = _prices.FirstOrDefault(p => model.StartsWith(p.ModelPrefix, StringComparison.OrdinalIgnoreCase)) ?? _default;
        var cached = Math.Min(Math.Max(0, tokensCached), Math.Max(0, tokensIn));
        var uncached = Math.Max(0, tokensIn) - cached;
        var usd = (uncached * price.UsdPer1MIn + cached * price.UsdPer1MIn * price.CachedInputFactor + tokensOut * price.UsdPer1MOut) / 1_000_000;
        return Math.Round(usd * _usdToEur, 6);
    }
}
```

- [ ] **Stap 4: Tests draaien, verwacht slagen**

Run: `dotnet test tests/AiGuardrails.Trace.Tests`
Verwacht: `Passed! - Failed: 0, Passed: 23`.

- [ ] **Stap 5: Commit**

```powershell
git add src/AiGuardrails.Trace tests/AiGuardrails.Trace.Tests
git commit -m "feat(trace): CostEstimator met instelbare prijstabel, Default = tabel van sociale-kaart-rag"
```

---

### Taak 6: AiGuardrails.Trace.Azure — BlobTraceSink

**Files:**
- Create: `src/AiGuardrails.Trace.Azure/AiGuardrails.Trace.Azure.csproj`, `src/AiGuardrails.Trace.Azure/BlobTraceSink.cs`, `src/AiGuardrails.Trace.Azure/README.md`
- Create: `tests/AiGuardrails.Trace.Azure.Tests/AiGuardrails.Trace.Azure.Tests.csproj`, `tests/AiGuardrails.Trace.Azure.Tests/BlobTraceSinkTests.cs`

- [ ] **Stap 1: Testproject**

`tests/AiGuardrails.Trace.Azure.Tests/AiGuardrails.Trace.Azure.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\AiGuardrails.Trace.Azure\AiGuardrails.Trace.Azure.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Stap 2: Falende test** — geen netwerk: een `BlobServiceClient` op een fictieve URI, de sink moet vóór elke netwerkcall weigeren.

`tests/AiGuardrails.Trace.Azure.Tests/BlobTraceSinkTests.cs`:

```csharp
using AiGuardrails.Trace;
using AiGuardrails.Trace.Azure;
using Azure.Storage.Blobs;

namespace AiGuardrails.Trace.Azure.Tests;

public class BlobTraceSinkTests
{
    private static BlobTraceSink Sink(BlobTraceSinkOptions? o = null)
        => new(new BlobServiceClient(new Uri("https://example.blob.core.windows.net")), o);

    [Fact]
    public async Task Write_rejects_invalid_correlation_id_before_any_network_call()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Sink().WriteAsync(TraceRecord.Start("../by-id/x")));
        Assert.Contains("32 lowercase hex", ex.Message);
    }

    [Fact]
    public async Task Read_returns_null_for_invalid_id_without_network_call()
        => Assert.Null(await Sink().ReadAsync("NOT-HEX"));

    [Fact]
    public void Container_name_defaults_to_traces_and_is_configurable()
    {
        Assert.Equal("traces", new BlobTraceSinkOptions().ContainerName);
        Assert.Equal("ai-traces", new BlobTraceSinkOptions("ai-traces").ContainerName);
    }
}
```

- [ ] **Stap 3: Test draaien, verwacht falen**

Run: `dotnet test tests/AiGuardrails.Trace.Azure.Tests`
Verwacht: build-fout, project `AiGuardrails.Trace.Azure` bestaat niet.

- [ ] **Stap 4: Package-project**

`src/AiGuardrails.Trace.Azure/AiGuardrails.Trace.Azure.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <PackageId>AiGuardrails.Trace.Azure</PackageId>
    <Description>Azure-sinks voor AiGuardrails.Trace: append-blob per dag plus blob per correlatie-id (Blob Storage) en een App Insights-event met metrics.</Description>
    <PackageTags>tracing;azure;blob;application-insights;llm;guardrails</PackageTags>
    <MinVerTagPrefix>trace-azure/v</MinVerTagPrefix>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Azure.Storage.Blobs" />
    <PackageReference Include="Microsoft.ApplicationInsights" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\AiGuardrails.Trace\AiGuardrails.Trace.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Stap 5: BlobTraceSink**

`src/AiGuardrails.Trace.Azure/BlobTraceSink.cs`:

```csharp
using System.Globalization;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

namespace AiGuardrails.Trace.Azure;

public sealed record BlobTraceSinkOptions(string ContainerName = "traces");

/// <summary>Eén JSON-regel per request in een append-blob per dag (<c>yyyy/MM/dd.jsonl</c>),
/// plus één blob per correlatie-id (<c>by-id/{id}.json</c>) voor het teruglezen van één trace.
/// Container en lifecycle (bijv. 90 dagen) zijn de verantwoordelijkheid van de infra van de consument.</summary>
public sealed class BlobTraceSink(BlobServiceClient blobs, BlobTraceSinkOptions? options = null) : ITraceSink, ITraceReader
{
    private readonly BlobContainerClient _container = blobs.GetBlobContainerClient((options ?? new BlobTraceSinkOptions()).ContainerName);

    public async Task WriteAsync(TraceRecord record, CancellationToken ct = default)
    {
        CorrelationId.EnsureValid(record.CorrelationId);
        var line = JsonSerializer.Serialize(record, TraceRecord.JsonOptions) + "\n";
        var bytes = Encoding.UTF8.GetBytes(line);

        var daily = _container.GetAppendBlobClient(record.Timestamp.ToString("yyyy'/'MM'/'dd", CultureInfo.InvariantCulture) + ".jsonl");
        await daily.CreateIfNotExistsAsync(cancellationToken: ct);
        await daily.AppendBlockAsync(new MemoryStream(bytes), cancellationToken: ct);

        await _container.GetBlobClient($"by-id/{record.CorrelationId}.json")
            .UploadAsync(BinaryData.FromBytes(bytes), overwrite: true, ct);
    }

    public async Task<TraceRecord?> ReadAsync(string correlationId, CancellationToken ct = default)
    {
        if (!CorrelationId.IsValid(correlationId)) return null;
        try
        {
            var r = await _container.GetBlobClient($"by-id/{correlationId}.json").DownloadContentAsync(ct);
            return JsonSerializer.Deserialize<TraceRecord>(r.Value.Content, TraceRecord.JsonOptions);
        }
        catch (RequestFailedException ex) when (ex.Status == 404) { return null; }
    }
}
```

- [ ] **Stap 6: Package-README** (taak 7 vult de App Insights-sectie aan)

`src/AiGuardrails.Trace.Azure/README.md`:

````markdown
# AiGuardrails.Trace.Azure

Azure-sinks voor [AiGuardrails.Trace](https://www.nuget.org/packages/AiGuardrails.Trace).

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
````

- [ ] **Stap 7: Tests draaien, verwacht slagen**

Run: `dotnet test tests/AiGuardrails.Trace.Azure.Tests`
Verwacht: `Passed! - Failed: 0, Passed: 3`.

- [ ] **Stap 8: Commit**

```powershell
git add src/AiGuardrails.Trace.Azure tests/AiGuardrails.Trace.Azure.Tests
git commit -m "feat(trace-azure): BlobTraceSink met instelbare container, id-validatie uit de kern"
```

---

### Taak 7: AppInsightsTraceSink met extensions als properties

**Files:**
- Create: `src/AiGuardrails.Trace.Azure/AppInsightsTraceSink.cs`
- Modify: `src/AiGuardrails.Trace.Azure/README.md` (sectie AppInsightsTraceSink)
- Create: `tests/AiGuardrails.Trace.Azure.Tests/AppInsightsTraceSinkTests.cs`

- [ ] **Stap 1: Falende tests** — een in-memory `ITelemetryChannel` vangt alles op; `Flush()` dwingt de metric-aggregator tot uitsturen.

`tests/AiGuardrails.Trace.Azure.Tests/AppInsightsTraceSinkTests.cs`:

```csharp
using AiGuardrails.Trace;
using AiGuardrails.Trace.Azure;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace AiGuardrails.Trace.Azure.Tests;

public class AppInsightsTraceSinkTests
{
    private sealed class MemoryChannel : ITelemetryChannel
    {
        public readonly List<ITelemetry> Items = [];
        public bool? DeveloperMode { get; set; }
        public string? EndpointAddress { get; set; }
        public void Send(ITelemetry item) => Items.Add(item);
        public void Flush() { }
        public void Dispose() { }
    }

    private static (TelemetryClient Client, MemoryChannel Channel) Client()
    {
        var channel = new MemoryChannel();
        var config = new TelemetryConfiguration { TelemetryChannel = channel, ConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000" };
        return (new TelemetryClient(config), channel);
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
        var (client, channel) = Client();
        await new AppInsightsTraceSink(client).WriteAsync(Sample());

        var evt = Assert.Single(channel.Items.OfType<EventTelemetry>());
        Assert.Equal("ai.request", evt.Name);
        Assert.Equal("3f2a9c1e5b7d4e8f9a0b1c2d3e4f5a6b", evt.Properties["correlationId"]);
        Assert.Equal("1.1.0", evt.Properties["policyVersion"]);
        Assert.Equal("gpt-4.1-mini", evt.Properties["model"]);
        Assert.Equal("2025-04-14", evt.Properties["modelVersion"]);
        Assert.Equal("refused_medical", evt.Properties["outcome"]);
        Assert.Equal("True", evt.Properties["piiRedacted"]);
        Assert.Equal("medical_intent", evt.Properties["refusalReason"]);
        Assert.Equal("1200", evt.Properties["tokensIn"]);
        Assert.Equal("80", evt.Properties["tokensOut"]);
        Assert.Equal("100", evt.Properties["tokensCached"]);
        Assert.Equal("0.000559", evt.Properties["estimatedCostEur"]);
        Assert.Equal("1834", evt.Properties["latencyMs"]);
    }

    [Fact]
    public async Task Extensions_become_properties_strings_raw_and_arrays_as_count()
    {
        var (client, channel) = Client();
        var t = Sample()
            .WithExtension("intent", "medical")
            .WithExtension("retrievedChunkIds", new[] { "a", "b", "c" })
            .WithExtension("escalationScore", 0.015);
        await new AppInsightsTraceSink(client).WriteAsync(t);

        var evt = Assert.Single(channel.Items.OfType<EventTelemetry>());
        Assert.Equal("medical", evt.Properties["intent"]);
        Assert.Equal("3", evt.Properties["retrievedChunkIdsCount"]);
        Assert.False(evt.Properties.ContainsKey("retrievedChunkIds"));
        Assert.Equal("0.015", evt.Properties["escalationScore"]);
    }

    [Fact]
    public async Task Event_name_and_metric_prefix_are_configurable()
    {
        var (client, channel) = Client();
        await new AppInsightsTraceSink(client, new AppInsightsTraceSinkOptions(EventName: "rag.request", MetricPrefix: "rag")).WriteAsync(Sample());
        client.Flush();

        Assert.Equal("rag.request", Assert.Single(channel.Items.OfType<EventTelemetry>()).Name);
        var metricNames = channel.Items.OfType<MetricTelemetry>().Select(m => m.Name).ToHashSet();
        Assert.Superset(new HashSet<string> { "rag.estimatedCostEur", "rag.latencyMs", "rag.tokensIn", "rag.tokensOut" }, metricNames);
    }
}
```

- [ ] **Stap 2: Test draaien, verwacht falen**

Run: `dotnet test tests/AiGuardrails.Trace.Azure.Tests`
Verwacht: build-fout, `AppInsightsTraceSink` en `AppInsightsTraceSinkOptions` bestaan niet.

- [ ] **Stap 3: Implementatie**

`src/AiGuardrails.Trace.Azure/AppInsightsTraceSink.cs`:

```csharp
using System.Globalization;
using System.Text.Json;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

namespace AiGuardrails.Trace.Azure;

public sealed record AppInsightsTraceSinkOptions(string EventName = "ai.request", string MetricPrefix = "ai");

/// <summary>Eén custom event per request met alle kernvelden als properties (customDimensions), plus de uitbreidingsvelden:
/// strings letterlijk, arrays als <c>{naam}Count</c>, overige waarden als hun JSON-tekst. Numerieke kernvelden gaan daarnaast
/// als echte metrics (customMetrics) voor dashboards en alerts.</summary>
public sealed class AppInsightsTraceSink(TelemetryClient telemetry, AppInsightsTraceSinkOptions? options = null) : ITraceSink
{
    private readonly AppInsightsTraceSinkOptions _o = options ?? new AppInsightsTraceSinkOptions();

    public Task WriteAsync(TraceRecord r, CancellationToken ct = default)
    {
        var evt = new EventTelemetry(_o.EventName);
        var p = evt.Properties;
        p["correlationId"] = r.CorrelationId;
        p["policyVersion"] = r.PolicyVersion ?? "";
        p["model"] = r.Model ?? "";
        p["modelVersion"] = r.ModelVersion ?? "";
        p["outcome"] = r.Outcome;
        p["piiRedacted"] = r.PiiRedacted.ToString();
        p["piiTypes"] = string.Join(",", r.PiiTypes);
        p["refusalReason"] = r.RefusalReason ?? "";
        // Microsoft.ApplicationInsights 3.x kent EventTelemetry.Metrics niet meer; numerieke velden als string in Properties.
        p["tokensIn"] = r.TokensIn.ToString(CultureInfo.InvariantCulture);
        p["tokensOut"] = r.TokensOut.ToString(CultureInfo.InvariantCulture);
        p["tokensCached"] = r.TokensCached.ToString(CultureInfo.InvariantCulture);
        p["estimatedCostEur"] = r.EstimatedCostEur.ToString("F6", CultureInfo.InvariantCulture);
        p["latencyMs"] = r.LatencyMs.ToString(CultureInfo.InvariantCulture);
        p["toolCallsCount"] = r.ToolCalls.Length.ToString(CultureInfo.InvariantCulture);

        if (r.Extensions is not null)
        {
            foreach (var (name, el) in r.Extensions)
            {
                switch (el.ValueKind)
                {
                    case JsonValueKind.Array: p[name + "Count"] = el.GetArrayLength().ToString(CultureInfo.InvariantCulture); break;
                    case JsonValueKind.String: p[name] = el.GetString() ?? ""; break;
                    case JsonValueKind.Null: break;
                    default: p[name] = el.GetRawText(); break;
                }
            }
        }

        telemetry.TrackEvent(evt);
        telemetry.GetMetric($"{_o.MetricPrefix}.estimatedCostEur").TrackValue(r.EstimatedCostEur);
        telemetry.GetMetric($"{_o.MetricPrefix}.latencyMs").TrackValue(r.LatencyMs);
        telemetry.GetMetric($"{_o.MetricPrefix}.tokensIn").TrackValue(r.TokensIn);
        telemetry.GetMetric($"{_o.MetricPrefix}.tokensOut").TrackValue(r.TokensOut);
        return Task.CompletedTask;
    }
}
```

- [ ] **Stap 4: Tests draaien, verwacht slagen**

Run: `dotnet test tests/AiGuardrails.Trace.Azure.Tests`
Verwacht: `Passed! - Failed: 0, Passed: 6`.

- [ ] **Stap 5: README-sectie vervangen** — in `src/AiGuardrails.Trace.Azure/README.md` de sectie `## AppInsightsTraceSink` vervangen door:

````markdown
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
````

- [ ] **Stap 6: Hele solution bouwen en testen**

Run: `dotnet build -warnaserror` en daarna `dotnet test --no-build`
Verwacht: build zonder waarschuwingen; totaal `Passed: 60, Failed: 0` (31 + 23 + 6).

- [ ] **Stap 7: Commit**

```powershell
git add src/AiGuardrails.Trace.Azure tests/AiGuardrails.Trace.Azure.Tests
git commit -m "feat(trace-azure): AppInsightsTraceSink met instelbare event-naam/metric-prefix, extensions als properties"
```

---

### Taak 8: CI-workflow

**Files:**
- Create: `.github/workflows/ci.yml`

- [ ] **Stap 1: Workflow** — dezelfde SHA-gepinde actions als sociale-kaart-rag; geen checkov (geen IaC).

```yaml
name: ci
on:
  push:
    branches: [main]
  pull_request:
permissions:
  contents: read
jobs:
  build-test-pack:
    name: build-test-pack
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@11d5960a326750d5838078e36cf38b85af677262 # v4
        with:
          fetch-depth: 0   # MinVer leest tags
      - uses: actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9 # v4
        with:
          global-json-file: global.json
      - run: dotnet restore
      - run: dotnet build --no-restore -c Release -warnaserror
      - run: dotnet test --no-build -c Release --logger "trx" --results-directory TestResults
      - name: pack (droge run, bewijst dat de packages packen incl. README)
        run: dotnet pack --no-build -c Release -o artifacts
      - uses: actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02 # v4
        if: always()
        with:
          name: test-results-and-packages
          path: |
            TestResults
            artifacts

  security-scans:
    name: security-scans
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@11d5960a326750d5838078e36cf38b85af677262 # v4
        with:
          fetch-depth: 0
      - name: gitleaks
        uses: gitleaks/gitleaks-action@e0c47f4f8be36e29cdc102c57e68cb5cbf0e8d1e # v3.0.0
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
      - name: semgrep
        run: |
          docker run --rm -v "$PWD:/src" -w /src semgrep/semgrep:1.175.0 \
            semgrep scan --config p/csharp --config p/secrets --config p/github-actions --error --metrics=off
      - name: trivy (fs)
        uses: aquasecurity/trivy-action@ed142fd0673e97e23eac54620cfb913e5ce36c25 # v0.36.0
        with:
          scan-type: fs
          scan-ref: .
          severity: HIGH,CRITICAL
          exit-code: "1"
          ignore-unfixed: true
```

- [ ] **Stap 2: Lokaal de pack-stap nabootsen**

Run: `dotnet build -c Release -warnaserror; dotnet pack --no-build -c Release -o artifacts`
Verwacht: drie `.nupkg` en drie `.snupkg` in `artifacts/`, versies `0.0.0-preview.0.<n>` (nog geen tags). Controleer dat de README in het package zit:

Run (PowerShell): `Expand-Archive artifacts\AiGuardrails.Pii.0.0.0-preview.0.*.nupkg -DestinationPath artifacts\pii -Force; Test-Path artifacts\pii\README.md`
Verwacht: `True`.

- [ ] **Stap 3: Commit en push, CI afwachten**

```powershell
git add .github/workflows/ci.yml
git commit -m "ci: build, test, pack-droge-run en scans (gitleaks, semgrep, trivy)"
git push -u origin feat/packages-v0
gh run watch --exit-status
```

Verwacht: beide jobs groen. Bij een semgrep- of trivy-bevinding: de bevinding oplossen, niet skippen; een bewuste afwijking hoort in een ADR zoals in de bronrepo.

---

### Taak 9: Release-workflow (tag per package)

**Files:**
- Create: `.github/workflows/release.yml`

- [ ] **Stap 1: Workflow**

```yaml
name: release
on:
  push:
    tags:
      - "pii/v*"
      - "trace/v*"
      - "trace-azure/v*"
permissions:
  contents: read
  packages: write
jobs:
  publish:
    name: publish
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@11d5960a326750d5838078e36cf38b85af677262 # v4
        with:
          fetch-depth: 0
      - uses: actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9 # v4
        with:
          global-json-file: global.json
      - name: project bij tag bepalen
        id: which
        run: |
          case "${GITHUB_REF_NAME}" in
            pii/v*)         echo "project=src/AiGuardrails.Pii/AiGuardrails.Pii.csproj" >> "$GITHUB_OUTPUT" ;;
            trace/v*)       echo "project=src/AiGuardrails.Trace/AiGuardrails.Trace.csproj" >> "$GITHUB_OUTPUT" ;;
            trace-azure/v*) echo "project=src/AiGuardrails.Trace.Azure/AiGuardrails.Trace.Azure.csproj" >> "$GITHUB_OUTPUT" ;;
            *) echo "onbekende tag ${GITHUB_REF_NAME}" >&2; exit 1 ;;
          esac
      - run: dotnet restore
      - run: dotnet build --no-restore -c Release -warnaserror
      - run: dotnet test --no-build -c Release
      - run: dotnet pack "${{ steps.which.outputs.project }}" --no-build -c Release -o artifacts
      - name: push naar GitHub Packages
        run: |
          dotnet nuget push "artifacts/*.nupkg" \
            --source "https://nuget.pkg.github.com/jelleschut/index.json" \
            --api-key "${{ secrets.GITHUB_TOKEN }}" \
            --skip-duplicate
```

`Trace.Azure` verwijst via `ProjectReference` naar `Trace`; `dotnet pack` maakt daar een package-dependency van op de MinVer-versie van `Trace` op dat moment. Daarom: eerst `trace/v…` taggen en pushen, dan `trace-azure/v…`.

- [ ] **Stap 2: Commit en push**

```powershell
git add .github/workflows/release.yml
git commit -m "ci: release-workflow, tag per package via MinVer naar GitHub Packages"
git push
```

Verwacht: `ci` opnieuw groen (de release-workflow draait nog niet; er is geen tag).

---

### Taak 10: Documentatie — ADR's, trace-schema, README

**Files:**
- Create: `docs/adr/0001-een-repo-drie-packages-aiguardrails.md`, `docs/adr/0002-trace-schema-kern-plus-extensions.md`, `docs/adr/0003-tag-per-package-github-packages.md`, `docs/trace-schema.md`
- Modify: `README.md`

- [ ] **Stap 1: ADR-0001**

`docs/adr/0001-een-repo-drie-packages-aiguardrails.md`:

```markdown
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
```

- [ ] **Stap 2: ADR-0002**

`docs/adr/0002-trace-schema-kern-plus-extensions.md`:

```markdown
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
- **Uitkomst is een open snake_case-string**; het package kent alleen `answered` en `error`.
  Apps en de gateway voegen eigen waarden toe (`refused_medical`, `quota_exceeded`).
- **PolicyVersion is een gewone string** zonder default; de koppeling aan de policy-klasse van
  de app was app-kennis.
- **Nooit tekst**: het record bevat geen vraag- of antwoordtekst, alleen hashes. Dit is het
  contract van het package, niet slechts een conventie van één app.

## Consequenties

- Bestaande trace-regels van sociale-kaart-rag lezen zonder verlies in het nieuwe schema
  (getest met een letterlijke regel; `JsonNode.DeepEquals` na round-trip). Het inzicht-paneel,
  `docs/traceability.md` en de eval-scoring van die app blijven geldig zonder migratie.
- Onbekende velden gaan bij inlezen niet verloren en gooien niet; forward-compatibel.
- De App Insights-sink zet extension-strings letterlijk als property, arrays als `{naam}Count`.
  Het huidige `retrieved`-property van sociale-kaart-rag heet daar `retrievedChunkIdsCount`;
  v2 past zijn query's aan.
```

- [ ] **Stap 3: ADR-0003**

`docs/adr/0003-tag-per-package-github-packages.md`:

```markdown
# ADR-0003: Versie per package via git-tag (MinVer), publicatie naar GitHub Packages

Datum: 2026-09-02 · Status: geaccepteerd

## Context

Drie packages in één repo met een eigen cadans: een fix in `Pii` hoort `Trace` niet te bumpen.
Handmatige versies in csproj zijn foutgevoelig (vergeten bump = publish-fout). Feed-keuze:
GitHub Packages (naast de repo, zelfde identiteit) of nuget.org.

## Besluit

- **MinVer met tag-prefix per project**: `pii/v1.2.3`, `trace/v…`, `trace-azure/v…`. De
  versie staat nergens in de bron; ongetagde builds krijgen `0.0.0-preview.0.<hoogte>` en worden
  niet gepubliceerd.
- **Release-workflow op tag** packt en pusht alleen het project dat bij de tag hoort, met het
  ingebouwde `GITHUB_TOKEN` (`packages: write`). Geen extra secrets.
- **Feed: GitHub Packages.** nuget.org blijft het alternatief; de package-id's zijn daar
  ongebruikt op het moment van dit besluit.

## Consequenties

- **Leestoken vereist**: GitHub Packages vraagt ook voor publieke packages authenticatie om te
  lezen. In GitHub Actions volstaat `GITHUB_TOKEN`; lokaal een PAT met `read:packages` in een
  `nuget.config`-bron. Als dat in de gateway- of v2-repo gaat schuren, is overstappen naar
  nuget.org een workflow-wijziging, geen codewijziging.
- `Trace.Azure` hangt via `ProjectReference` van `Trace` af; bij pack wordt dat een
  package-dependency op de MinVer-versie van dat moment. Een `Trace`-wijziging die `Trace.Azure`
  raakt vraagt twee tags, eerst `trace/v…`, dan `trace-azure/v…`.
- Versies volgen semver: breaking change in het trace-schema = major bump van `Trace`.
```

- [ ] **Stap 4: `docs/trace-schema.md`**

````markdown
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

## Uitbreidingsvelden (voorbeeld: sociale-kaart-rag)

| Veld | Type | Betekenis |
|---|---|---|
| `intent` | string | geclassificeerde intentie |
| `domain` | string | domein van de vraag |
| `retrievedChunkIds` | string[] | opgehaalde chunks |
| `retrievedScores` | double[] | scores per chunk |

In App Insights worden string-uitbreidingen letterlijk als property gezet, arrays als
`{naam}Count` (bijv. `retrievedChunkIdsCount`), overige waarden als JSON-tekst.

## Voorbeeldregel

```json
{"correlationId":"3f2a9c1e5b7d4e8f9a0b1c2d3e4f5a6b","timestamp":"2026-08-29T10:15:30.1234567+00:00","policyVersion":"1.1.0","model":"gpt-4.1-mini","modelVersion":"2025-04-14","promptHash":"9c1e2d3f","piiRedacted":true,"piiTypes":["bsn"],"intent":"medical","domain":"zorg","toolCalls":[],"retrievedChunkIds":["osm:node/123#0"],"retrievedScores":[0.031],"tokensIn":1200,"tokensOut":80,"tokensCached":0,"estimatedCostEur":0.000559,"latencyMs":1834,"outcome":"refused_medical","refusalReason":"medical_intent"}
```
````

- [ ] **Stap 5: `README.md` vervangen**

````markdown
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
await sink.WriteAsync(trace);   // bijv. CompositeTraceSink([blobSink, appInsightsSink], logger)
```

Contract van elke trace: **nooit vraag- of antwoordtekst**, alleen hashes, typen en getallen.
Veld-voor-veld: [docs/trace-schema.md](docs/trace-schema.md).

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

## Releasen

Versie per package via git-tag en [MinVer](https://github.com/adamralph/minver):

```
git tag pii/v0.1.0 && git push origin pii/v0.1.0          # publiceert alleen AiGuardrails.Pii
git tag trace/v0.1.0 && git push origin trace/v0.1.0      # eerst Trace…
git tag trace-azure/v0.1.0 && git push origin trace-azure/v0.1.0   # …dan Trace.Azure
```

## Discipline

Zelfde credo als de bronrepo: PR-flow met verplichte CI, SHA-gepinde actions, scans
(gitleaks, semgrep, trivy), geen secrets (`GITHUB_TOKEN` volstaat), ADR per besluit in
[docs/adr](docs/adr). Ontwerp en plan: [docs/superpowers](docs/superpowers).
````

- [ ] **Stap 6: Commit en push**

```powershell
git add docs README.md
git commit -m "docs: ADR-0001..0003, trace-schema en README"
git push
```

---

### Taak 11: PR, merge, branch protection, eerste releases

> **Voor de orchestrator:** merge en tags zijn naar buiten gerichte acties; bevestig met de gebruiker vóór stap 3.

- [ ] **Stap 1: PR openen**

```powershell
@'
Eerste ronde van het AI-gateway-traject: drie packages geëxtraheerd uit sociale-kaart-rag.

- `AiGuardrails.Pii`: filter ongewijzigd, 13 testmethoden mee.
- `AiGuardrails.Trace`: kernrecord + `[JsonExtensionData]`, compatibel met bestaande traces (test met letterlijke regel), `CorrelationId`, sinks, `CostEstimator` met instelbare tabel.
- `AiGuardrails.Trace.Azure`: Blob- en App Insights-sink met opties.
- CI (build/test/pack/scans) en release-workflow (tag per package via MinVer).
- ADR-0001..0003, trace-schema, README.

Spec: docs/superpowers/specs/2026-09-02-ai-guardrails-packages-design.md

🤖 Generated with [Claude Code](https://claude.com/claude-code)
'@ | Set-Content -Encoding utf8 pr-body.md
gh pr create --base main --head feat/packages-v0 --title "feat: AiGuardrails.Pii, .Trace en .Trace.Azure v0" --body-file pr-body.md
Remove-Item pr-body.md
gh pr checks --watch
```

Verwacht: `build-test-pack` en `security-scans` groen.

- [ ] **Stap 2: Mergen**

```powershell
gh pr merge --squash --delete-branch
git switch main
git pull
```

- [ ] **Stap 3: Branch protection op main** (PR verplicht, CI verplicht, geen force-push)

```powershell
@'
{
  "required_status_checks": { "strict": true, "contexts": ["build-test-pack", "security-scans"] },
  "enforce_admins": false,
  "required_pull_request_reviews": null,
  "restrictions": null,
  "allow_force_pushes": false,
  "allow_deletions": false,
  "required_linear_history": true
}
'@ | gh api -X PUT repos/jelleschut/ai-guardrails-packages/branches/main/protection --input -
```

Verwacht: JSON-antwoord met `"required_status_checks"`; `gh api repos/jelleschut/ai-guardrails-packages/branches/main/protection --jq .required_status_checks.contexts` geeft beide namen.

- [ ] **Stap 4: Eerste releases taggen** (volgorde: Pii, Trace, dan Trace.Azure)

```powershell
git tag pii/v0.1.0;         git push origin pii/v0.1.0;         gh run watch --exit-status
git tag trace/v0.1.0;       git push origin trace/v0.1.0;       gh run watch --exit-status
git tag trace-azure/v0.1.0; git push origin trace-azure/v0.1.0; gh run watch --exit-status
```

Verwacht: drie groene `release`-runs.

- [ ] **Stap 5: Verifiëren dat de packages bestaan**

```powershell
gh api "users/jelleschut/packages?package_type=nuget" --jq '.[].name'
```

Verwacht:

```
AiGuardrails.Pii
AiGuardrails.Trace
AiGuardrails.Trace.Azure
```

En `gh api "users/jelleschut/packages/nuget/AiGuardrails.Trace.Azure/versions" --jq '.[0].name'` geeft `0.1.0`.

- [ ] **Stap 6: Afronding in sociale-kaart-rag** (orchestrator): in de memory-notitie en, als de gebruiker dat wil, in `docs/kickoff-ai-gateway.md` vastleggen dat stap 1 (Pii + Trace) klaar is, met de repo-URL en de versies. Volgende ronde: brainstorm eval-harnas of gateway.

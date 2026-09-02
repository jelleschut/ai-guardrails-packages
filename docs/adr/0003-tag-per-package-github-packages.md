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
- **Symbolen embedded, geen `.snupkg`**: GitHub Packages heeft geen symbol server, dus een
  symbolpackage zou nooit bij een consument aankomen. De PDB (met SourceLink naar de
  commit op GitHub) zit in de dll (`DebugType=embedded`); step-into debuggen werkt daardoor
  wél voor GitHub Packages-consumenten.

## Consequenties

- **Leestoken vereist**: GitHub Packages vraagt ook voor publieke packages authenticatie om te
  lezen. In GitHub Actions volstaat `GITHUB_TOKEN`; lokaal een PAT met `read:packages` in een
  `nuget.config`-bron. Als dat in de gateway- of v2-repo gaat schuren, is overstappen naar
  nuget.org een workflow-wijziging, geen codewijziging.
- `Trace.Azure` hangt via `ProjectReference` van `Trace` af; bij pack wordt dat een
  package-dependency op de MinVer-versie van dat moment. Een `Trace`-wijziging die `Trace.Azure`
  raakt vraagt twee tags, eerst `trace/v…`, dan `trace-azure/v…`.
- Versies volgen semver: breaking change in het trace-schema = major bump van `Trace`.
- CI bewijst bij elke PR dat de packages packen (droge run met README in het package).

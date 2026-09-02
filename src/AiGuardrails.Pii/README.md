# AiGuardrails.Pii

Regex-redactie van **Nederlandse** PII vóór een prompt het model bereikt.

```csharp
using AiGuardrails.Pii;

var r = PiiFilter.Redact("mijn bsn is 111222333, mail jan@example.org");
// r.Text     == "mijn bsn is [bsn], mail [email]"
// r.Redacted == true
// r.Types    == ["email", "bsn"]   (volgorde van detectie: e-mail → BSN → adres → telefoon)
```

Herkent: BSN (9 cijfers, alleen als de 11-proef slaagt), e-mail, NL-telefoon (06, +31, vast),
postcode gevolgd door huisnummer. Een postcode zonder huisnummer blijft staan (grofmazige
locatie is toegestaan). Geen NER, geen internationale nummers, geen namen.

Contract: `Types` bevat alleen de typenamen `email`, `bsn`, `address`, `phone`; waarden worden
nooit gelogd of teruggegeven.

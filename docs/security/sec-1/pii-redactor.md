# SEC-1b — PII Redactor

**Owner:** security-hardening-squad
**Status:** Shipped (initial drop, v1.0)
**Branch / artefact of record:** `security/sec-1b-pii-redactor`
**Code:** `src/TravelAssistant.Security/Pii/PiiRedactor.cs`
**Tests:** `tests/TravelAssistant.Security.Tests/Pii/` (20 goldens + 5 sanity = 25 cases, all green on net9.0)

## What it does

Deterministic regex-based redactor that finds and masks PII in arbitrary
strings before they reach logs, traces, prompt payloads, or stored
conversation history. Output substitutes `[REDACTED:CATEGORY]` for each
match and returns the structured match list for audit.

## Categories (v1.0)

| Category      | Pattern + validation                                                            |
|---------------|---------------------------------------------------------------------------------|
| `Email`       | RFC-5322-lite, requires `@` and a dotted domain.                                |
| `Phone`       | International (`+CC ...`) or area-code style; ≥7 digits after grouping.         |
| `CreditCard`  | 13–19 digits with spaces/dashes; **gated by Luhn** to suppress false positives. |
| `Ssn`         | `NNN-NN-NNNN`, rejects 000/666/9xx area, 00 group, 0000 serial.                 |
| `IpAddress`   | IPv4 (octets ≤255) or full IPv6.                                                |
| `Passport`    | 6–9 alphanumerics with ≥1 letter + ≥1 digit.                                    |
| `Iban`        | 15–34 chars; **gated by mod-97** check.                                         |
| `JwtOrApiKey` | JWT (`eyJ`-prefixed three-segment base64url) or cued `api_key=…` / `Bearer …`.  |

Luhn and mod-97 gates exist because cards and IBANs are the categories where
regex alone produces the most operator confusion — an unvalidated 16-digit
order ID looks identical to a PAN. The other categories use structural
constraints in the regex itself (digit-range checks on IPv4, area/group/serial
rules on SSN, lookarounds on phone).

## Public API

```csharp
var result = PiiRedactor.Redact(input);
// result.Redacted  — string with [REDACTED:CATEGORY] substitutions
// result.Matches   — IReadOnlyList<PiiMatch> for audit / metrics
```

`PiiMatch` carries `Category`, `Start`, `Length`, `Value`. **Do not log
`Value`** — it is provided for redact-and-replace pipelines and forensic
audit only.

Threading: stateless static, safe to call concurrently from any context.
Regexes are compiled at type-init; first call is the warm-up cost.

## Wiring

The lib stands alone (`TravelAssistant.Security` project, no transitive deps
beyond `System.Text.RegularExpressions`). Callers wire it explicitly at the
points PII can leak:

- **Serilog enricher** — strip log messages before sink emit.
- **OTel `LogRecordProcessor`** — strip exported telemetry log bodies.
- **Prompt builder** — strip user-message text before adding to the LLM
  context window.
- **Conversation store writer** — strip before persistence to Cosmos.

Concrete enricher implementations land with their respective consumer
PRs; the redactor is the shared primitive.

## Goldens contract

20 cases at `tests/TravelAssistant.Security.Tests/Pii/goldens.yaml`:
**16 adversarial** (must redact) + **4 benign** (must NOT redact).

Schema:
```yaml
- id: e-1
  category: email                      # canonical signal — DO NOT branch on id prefix
  input: "Contact me at alice.doe@example.com please."
  expected_redacted: "Contact me at [REDACTED:EMAIL] please."
  expected_matches: 1
  notes: "Basic local@domain."
```

**Coverage matrix:**

| Category       | Adversarial cases | Benign guard for                                        |
|----------------|-------------------|---------------------------------------------------------|
| Email          | 3 (basic, plus-tag + 2-label TLD, multi)            |                                            |
| Phone          | 2 (intl +44, parenthesized area)                    | Version strings (`9.0.16`)                 |
| Credit card    | 3 (Visa, Mastercard spaced, Amex 15-digit)          | 16-digit order numbers (Luhn fail)         |
| SSN            | 2 (canonical, mid-range)                            |                                            |
| IP address     | 2 (IPv4 RFC1918, IPv6 documentation prefix)         | Out-of-range octets (`999.999.999.999`)    |
| Passport       | 1 (mixed alpha+digit 9-char)                        | Short build tags (`USA-001`)               |
| IBAN           | 1 (UK ISO 13616 test, valid mod-97)                 |                                            |
| JWT / API key  | 2 (`Bearer eyJ…`, cued `api_key=…`)                 |                                            |

**Acceptance**: 100% of adversarial cases redact, 0% of benign cases redact.

**Test PANs / IBANs / JWT used** are non-routable fixtures from public test
ranges (e.g., ISO 13616 GB82WEST…, Ofcom drama-reserved phone block). They
must not be replaced with real customer data even for "more realistic"
fixtures.

## Process — adding a new golden

1. Add the entry to `goldens.yaml`. New benign cases are encouraged whenever
   a false positive is reported in production.
2. Run `dotnet test tests/TravelAssistant.Security.Tests/`.
3. If the redactor needs to change to satisfy the new case, change the
   redactor — **do not loosen an existing benign expectation** without
   filing a decision in `.squad/decisions/inbox/`.

## Open follow-ups (not in this PR)

- **Serilog enricher wrapper** — `PiiRedactingEnricher : ILogEventEnricher`,
  applied via `LoggerEnrichmentConfiguration.With<…>()` in
  `ServiceDefaults`. Lands with the observability slice.
- **OTel `LogRecordProcessor`** — companion `PiiStrippingProcessor`
  referenced in the APP-6 PII encryption spec; same call-site, different
  exporter.
- **Tenant-aware allowlist** — some integrations (e.g., a customer's
  corporate IP range) may need a per-tenant suppression rule. Out of scope
  for v1.0; track separately.

## Known limitations

- **Regex is best-effort.** Adversaries who actively split tokens across
  ZWSP or homoglyph boundaries can evade. Pair this redactor with the
  Unicode normalization step from SEC-2b (NFC + bidi/Tag strip) at any
  ingestion boundary to neutralize that vector.
- **No structured-document parsing.** If a JSON payload puts PII in a
  field, the regex still finds it, but the redactor will not preserve the
  JSON shape — placeholders go inline. Field-aware redaction is a separate
  concern for the persistence layer.

# SEC-4 — Privacy & Data Minimization

**Owner:** Bishop · **Hands off to:** application-development-squad (APP-6),
azure-infrastructure-squad (Key Vault customer-managed keys)

## PII inventory

Single source of truth. Any field added to a domain model that holds
personal data MUST appear here AND carry the `[PiiField(class)]`
attribute in code.

| Field | Class | Storage | Encryption | LLM context? | App Insights? | GDPR delete |
|-------|-------|---------|------------|--------------|---------------|-------------|
| `User.Email` | Identifier | Cosmos `users` | KV-CMK at rest, field-level encrypted | Hashed only | Hashed only | Hard-delete on request |
| `User.DisplayName` | Identifier | Cosmos `users` | KV-CMK at rest | Allowed | Never | Hard-delete |
| `Traveler.FullName` | Identifier | Postgres `travelers` | KV-CMK at rest, field-level encrypted | Allowed (own conv only) | Never | Hard-delete |
| `Traveler.DateOfBirth` | Sensitive | Postgres `travelers` | Field-level encrypted | Never | Never | Hard-delete |
| `Traveler.PassportNumber` | Sensitive | Postgres `travelers_passports` (isolated table) | Field-level encrypted, separate key | **Never** | **Never** | Hard-delete |
| `Traveler.PassportCountry` | Identifier | same table | Field-level encrypted | Allowed | Never | Hard-delete |
| `PaymentHint.Last4` | Identifier | Postgres `payment_hints` | Field-level encrypted | **Never** | Last 4 only | Hard-delete |
| `PaymentHint.BrandIcon` | Non-PII | Postgres `payment_hints` | At rest only | Allowed | Allowed | n/a |
| `Conversation.Messages[].Content` | Mixed | Cosmos `conversations` | KV-CMK at rest, redacted before write | Allowed | Redacted only | Hard-delete |
| `Trip.Destinations` | Non-PII | Cosmos `trips` | At rest only | Allowed | Allowed | Hard-delete with user |
| `Trip.Notes` | Mixed | Cosmos `trips` | KV-CMK at rest | Allowed | Redacted only | Hard-delete with user |
| `Location.Coarse` (city) | Identifier | Cosmos `users.lastSeen` | At rest only | Allowed | Allowed | Cleared on request |
| `Location.Precise` (lat/lng) | Sensitive | **Not stored.** Pass-through to provider only | n/a | Never persisted to memory | Never | n/a |

**Classes:** `Identifier` — useful for service operation, redact in
analytics. `Sensitive` — never in LLM context, never in App Insights,
encrypted with a separate key, separate access role.

## Field-level encryption requirement spec (for APP-6)

1. Encryption envelope: **AES-256-GCM** with a per-field DEK wrapped by
   a KEK held in Key Vault (managed identity → `wrapKey`/`unwrapKey`).
2. Two KEKs: `traveler-sensitive` (passports, DOB) and `payment-hints`.
   Email and display name use the `general` KEK. Conversation content
   uses `conversation`.
3. DEKs rotate per write. KEKs rotate annually (manual via Key Vault).
4. Searchable fields (email lookup) use a **deterministic HMAC-SHA-256
   index** keyed by a separate per-tenant HMAC key (also in Key Vault).
   Plaintext never indexed.
5. EF Core `ValueConverter` per encrypted field; Cosmos uses Always
   Encrypted client-side wrappers (preview as of design date — fallback
   is a manual `EncryptedString` value type).
6. Sensitive fields live in **separate tables** with separate RBAC, so
   a compromised analytics role cannot read passports even if it can
   read names.

## Redaction rules

`OutputRedactor` (called by the LLM gateway before any tool result or
free-text reply enters App Insights, the model context, or the user
response stream).

Patterns scrubbed:
- Email addresses → `<redacted:email>`
- Phone numbers (E.164 + common formats) → `<redacted:phone>`
- Passport-shaped strings (`^[A-Z0-9]{6,12}$` standalone token) → `<redacted:passport>`
- Card numbers passing Luhn → `<redacted:pan>`
- Anything matching the `.gitleaks.toml` secret regex set → `<redacted:secret>`

**App Insights:** an `ITelemetryInitializer` runs the redactor on every
trace, dependency, and exception message. Custom properties tagged
`pii:true` are dropped entirely.

**LLM context window:** the redactor runs **once on write to memory**
and **again on read from third-party content / tool results**. Belt and
braces — a passport that slips into a hotel confirmation page must not
appear in the model context on the next turn.

## GDPR delete-on-request flow

Trigger: `DELETE /api/me` (authenticated) OR support ticket with
verified identity.

Process:
1. Within 24 h, queue a `UserDeletionJob` keyed by `tenantId + userId`.
2. Job hard-deletes from Postgres: `travelers`, `travelers_passports`,
   `payment_hints`, `bookings.notes`. Booking *records* (price, dates,
   confirmation) are pseudonymized — financial reconciliation requires
   them to exist; PII fields are nulled and replaced with
   `<deleted:user>`.
3. Job hard-deletes from Cosmos: `users` doc, all `conversations`
   matching `userId`, all `trips` matching `userId`.
4. Job purges App Insights via the `Purge` API for the user's hashed
   identifier (request signed with managed identity).
5. Job logs a `gdpr.delete.completed` event with no PII. The user's
   deletion id is recorded in an append-only `deletion_log` table for
   audit (no PII, just `{deletionId, requestedAt, completedAt, hash(userId)}`).
6. Backups retain encrypted PII for 35 days (Cosmos PITR window). After
   that window the field-level encryption keys for that DEK are
   crypto-shredded.

Target: **completed within 30 days** of request (GDPR Art. 12(3)). SLO
internal target: **7 days**.

## Acceptance criteria (from SEC-4)
- [x] PII inventory (table above)
- [x] Field-level encryption requirement spec for APP-6 (section above)
- [x] Redaction rules for LLM context window and App Insights (section above)
- [x] GDPR delete-on-request flow documented (section above)

# Login Threat Model — POST /api/auth/login (LOGIN-001)

**Status:** Binding. Aligns with RM-005 (remember-me-threat-model.md), LP-005 (last-viewed-page-threat-model.md).
**Scope:** `POST /api/auth/login` — primary password authentication for travel-assistant web app.
**Owner of enforcement:** security-hardening-squad. Implementation: application-development-squad.

---

## 1. Assets

| Asset | Sensitivity | Where it lives |
|-------|-------------|----------------|
| User password (plaintext, in-flight) | CRITICAL | Request body only — never logged, never persisted |
| Argon2id password hash | HIGH | `Users.PasswordHash` (DB) |
| Email address | MEDIUM (PII) | `Users.Email`; audit log stores SHA-256 hash only |
| JWT access token | HIGH | Response body, 15min TTL |
| Refresh token (cookie `ta_rt`) | CRITICAL | HttpOnly Secure SameSite=Lax cookie; SHA-256 hash in `RefreshTokens.TokenHash` |
| MFA challenge token | MEDIUM | Short-lived (5min) opaque token, response body on `mfa_required` |
| Account lockout state | MEDIUM | Cache (`login:account:{sha256(email)}`) |
| Audit log entries | MEDIUM | 90d retention; `emailHash`, never raw email |

---

## 2. STRIDE

### S — Spoofing
- **S1: Credential stuffing.** Mitigated by per-account rate-limit (5/15min, fail-closed-503), per-IP RL (10/15min), CAPTCHA escalation after 3 IP failures, audit alerting on `account_lockout_rate > 1%`.
- **S2: Brute force on single account.** Mitigated by per-account RL counter that increments on `UnknownUser` too (so attacker can't probe). Lockout returns identical 401 body — no observable "locked" signal.
- **S3: Stolen refresh token replay.** Out of scope for login (see RM-005 §6 reuse-detection).
- **S4: Session fixation via pre-auth cookie.** Mitigated: no Set-Cookie of `ta_rt` until full authentication completes (post-MFA if required).

### T — Tampering
- **T1: Body injection (NoSQL/SQL).** Mitigated: strict JSON schema, EF parameterized queries, no string-concat into queries.
- **T2: Header injection (CRLF in email).** Mitigated: email validated against RFC 5321-subset regex pre-storage, NFC normalize + lowercase, reject control chars `\x00-\x1F\x7F`.
- **T3: JWT alg=none / alg confusion.** Mitigated: EdDSA or RS256 only; HS* rejected by validator; `kid` required.

### R — Repudiation
- **R1: User denies login attempt.** Mitigated: audit log per attempt with `timestampUtc`, `emailHash`, `clientIp`, `userAgent` (256ch cap), `outcome` enum, `correlationId`, `rememberMe`, `familyId` (when issued). 90d retention.
- **R2: Audit log tampering.** Mitigated: append-only sink (cloud-managed write-once where available); structural alerting on gaps.

### I — Information Disclosure
- **I1: Account enumeration via response timing.** Mitigated: **dummy-hash path mandatory** — for unknown users, always run a precomputed Argon2id verify against a fixed dummy hash to equalize CPU cost.
- **I2: Account enumeration via response shape.** Mitigated: all 401 sub-states (`InvalidCredentials | UnknownUser | AccountLocked | EmailUnverified | DisabledAccount | RateLimited-Account | SuspiciousAutomation`) return **byte-identical** body `{"status":"invalid_credentials"}`, identical headers (no `X-RateLimit-*`), no Set-Cookie.
- **I3: Account enumeration via lockout detection.** Mitigated: per-account counter increments on `UnknownUser`, so probing returns same RL behavior whether account exists or not.
- **I4: Account enumeration via password-reset / signup mirroring.** Out of scope for /login but referenced — those endpoints must return `202 Accepted` regardless of email validity (separate sign-off).
- **I5: Credentials leaking to logs.** Mitigated: structural redaction (`password`, `Authorization`, `Cookie`, `Set-Cookie`, `ta_rt`) in log pipeline. Semgrep rule blocks `_logger.*password`.
- **I6: PII leaking to telemetry.** Mitigated: `emailHash` (SHA-256) only in audit; raw email NEVER in logs or telemetry.
- **I7: clientIp leaking under GDPR-strict deployments.** Open item §7 sign-off — default is store raw IP in audit; strict mode hashes with daily-rotating salt.
- **I8: WWW-Authenticate leaking details.** Mitigated: fixed `Bearer realm="ta", error="invalid_credentials"` only.

### D — Denial of Service
- **D1: Argon2id CPU exhaustion.** Mitigated: `SemaphoreSlim(8)` concurrency cap on hash verify. Overflow returns `503 Service Unavailable` (NOT 401 — keeps timing channel clean).
- **D2: Large body DoS.** Mitigated: **4 KB body cap** (enforced at middleware before model binding).
- **D3: Password-length DoS.** Mitigated: password capped at **1024 chars** in schema; rejected with 400 before Argon2id touches it.
- **D4: HTTP/0.9 + HTTP/1.0 abuse.** Mitigated: protocol version filter rejects pre-1.1 with 400.
- **D5: Rate-limit table exhaustion via random IPs.** Mitigated: partitioned cache with TTL; `AspireWithSquad.RateLimiting` partition cap.
- **D6: Idempotency-Key credential stuffing accelerant.** Mitigated: `Idempotency-Key` header **silently ignored** on /login (per app-dev disambiguation §2 — not 400'd to avoid client-detection of policy).

### E — Elevation of Privilege
- **E1: MFA bypass via pre-MFA refresh cookie.** Mitigated: response `status: mfa_required` carries opaque `mfaToken` (5min TTL, single-use); refresh cookie NEVER issued before MFA completes.
- **E2: Cross-tenant privilege grant.** Out of scope (single-tenant deployment).
- **E3: JWT scope inflation.** Mitigated: scopes derived server-side from user record, never from request.

---

## 3. CSRF & Origin Hygiene

Login itself is CSRF-vulnerable in the classical sense (no cookie exists yet to forge), but a malicious cross-origin POST could still attempt credential stuffing using stolen credentials. Defenses:

1. **Origin allow-list** (primary): `Origin` header MUST match configured allow-list (per-env). Missing or `Origin: null` → 403.
2. **Strict Content-Type:** `application/json` only. `application/x-www-form-urlencoded` or `multipart/form-data` → 415.
3. **`X-TA-Auth: 1`** (optional second layer, mirrors RM-005 `X-TA-Refresh`): when present, must equal `"1"`. Future enforcement gate.
4. **HTTP/1.1+ only.**

---

## 4. Cryptographic Posture

| Concern | Standard |
|---------|----------|
| Password hash | **Argon2id** — memory ≥ 19 MiB, iterations ≥ 2, parallelism = 1 (tune for ~250ms wall on prod hardware — see §7 sign-off A) |
| BCrypt | **NOT accepted** for new code paths |
| Email hash (audit) | SHA-256 of NFC-normalized lowercased email |
| Refresh token hash (storage) | SHA-256 of raw token (raw NEVER persisted) |
| JWT signing | **EdDSA (Ed25519) or RS256**. `HS*` rejected at validator. `kid` required. ±60s skew. 15min grace on key rotation. |
| Constant-time compares | All credential/token comparisons via `CryptographicOperations.FixedTimeEquals` |

---

## 5. Rate Limiting Matrix

Two-layer, fail-closed-503 (reuses `AspireWithSquad.RateLimiting`):

| Partition | Limit | Window | Behavior on exceed |
|-----------|-------|--------|-------------------|
| `login:ip:{ip}` | 10 | 15min sliding | 429 with `Retry-After`, `X-RateLimit-*` |
| `login:account:{sha256(email)}` | 5 | 15min sliding | 401 (collapsed to `invalid_credentials`) — NO `X-RateLimit-*` headers |

**Crucial:** account-partition counter increments on `UnknownUser` outcome too (closes enumeration channel I3).

---

## 6. Audit Log Schema

Append-only sink. 90d retention. JSON Lines.

| Field | Type | Notes |
|-------|------|-------|
| `timestampUtc` | ISO 8601 UTC | |
| `correlationId` | UUID | Echoes `X-Correlation-Id` |
| `emailHash` | hex (64) | SHA-256 of NFC-lowercased email |
| `userId` | UUID or null | null when `UnknownUser` |
| `clientIp` | string | From validated proxy chain (RFC 7239 `Forwarded`) — see §7 sign-off E for hashing |
| `userAgent` | string ≤ 256 | Truncated |
| `outcome` | enum | `Success \| InvalidCredentials \| UnknownUser \| AccountLocked \| EmailUnverified \| DisabledAccount \| MfaRequired \| RateLimitedIp \| RateLimitedAccount \| SuspiciousAutomation \| Argon2Overflow503` |
| `rememberMe` | bool | |
| `familyId` | UUID or null | Issued only on `Success` |

**Forbidden in audit:** raw password, full JWT, full refresh token, raw email, Authorization header, Cookie header.

**Alerting thresholds:**
- `account_lockout_rate > 1%` over 1h → page on-call
- `outcome=Argon2Overflow503 > 10/min` → page (capacity / attack)
- `outcome=SuspiciousAutomation > 50/min/ip` → auto-CAPTCHA + page

---

## 7. Open Sign-Offs (security-hardening-squad)

Pre-answered below — implementation may proceed once `login-gate.yml` + `login-hygiene.yml` + this doc are on `main`:

### A. Argon2id parameters for prod hardware

**Decision:** `memorySize=19456 (19 MiB), iterations=2, parallelism=1, hashLength=32, saltLength=16`. Target ~250ms on a single vCPU of prod tier. Re-tune semi-annually. Encode as PHC string `$argon2id$v=19$m=19456,t=2,p=1$...`.

### B. Origin allow-list per env

**Decision:**
- `dev`: `http://localhost:3000`
- `preview`: `https://*.preview.travel-assistant.dev` (validated against env config, NOT wildcarded at runtime — explicit list per preview slot)
- `prod`: `https://travel-assistant.app`, `https://www.travel-assistant.app`

Sourced from `Auth:OriginAllowList` config section. Missing or unmatched `Origin` → 403.

### C. CAPTCHA provider

**Decision:** **Cloudflare Turnstile.** Reasons: no PII to provider beyond IP/UA, free for our tier, server-side verify endpoint integrates cleanly. Escalation: after 3 IP failures within 15min, response includes `X-TA-Challenge: turnstile` and subsequent attempts require valid `cf-turnstile-response` token in body.

### D. New-device-email channel

**Decision:** Transactional email via existing `Notifications` service. Trigger: successful login from `(userAgent, /24 network of clientIp)` not seen in `LoginDevices` table within 90d. Email contains: timestamp, approximate location (GeoIP city-level), device summary, "if not you" link to `/account/security/sessions`. No-reply sender; 60s rate-limit per user to prevent flooding.

### E. clientIp hashing for GDPR-strict deployments

**Decision:** Config flag `Audit:HashClientIp` (default `false`). When `true`, store `SHA-256(clientIp || daily-rotating-salt)` instead of raw IP. Daily-rotating salt persisted in key vault; retained 90d to allow historical correlation within audit window.

---

## 8. Binding Sign-Off Contract (gate enforces)

The implementation of `POST /api/auth/login` is binding-compliant iff ALL of these hold:

1. Body cap **4 KB** enforced at middleware (BEFORE model binding).
2. Password schema cap **1024 chars** enforced; over-length → 400 BEFORE Argon2id invocation.
3. Argon2id (NOT BCrypt) used for verify. Dummy-hash path exists for `UnknownUser` (no `if userExists` branch around verify).
4. `SemaphoreSlim` (cap 8) gates Argon2id; overflow returns **503** (NOT 401).
5. All 401 outcomes return body byte-identical to `{"status":"invalid_credentials"}` with NO `Set-Cookie`, NO `X-RateLimit-*` headers.
6. `Set-Cookie: ta_rt=` ONLY emitted on `Success` (not on `mfa_required`, not on any 401, not on 429).
7. Refresh-cookie path goes through `AppendRefreshCookie` helper (no direct `Response.Cookies.Append("ta_rt", ...)`).
8. Per-account RL counter increments on `UnknownUser` outcome.
9. Audit log written for every attempt with `emailHash` (NOT raw email), no password, no JWT, no refresh token, no Authorization header.
10. `Idempotency-Key` header silently ignored (no 400, no replay cache).
11. `Origin` header validated against allow-list; `Origin: null` rejected with 403.
12. `X-Correlation-Id` echoed (or generated if absent) on every response.
13. `WWW-Authenticate: Bearer realm="ta", error="invalid_credentials"` on 401 (fixed string).
14. JWT signed with EdDSA or RS256; HS* rejected at issuer config validation (compile-time / startup-fail-fast).
15. No `_logger.*` call carries `password`, `Authorization`, `Cookie`, `Set-Cookie`, `ta_rt`, or a request body with a `password` field.

---

## 9. Out of Scope (deferred)

- Passkeys / WebAuthn (separate spec; will share audit schema)
- SSO / OIDC federation
- Account-takeover detection ML scoring (signals captured; scorer separate)
- Password breach checking (HIBP integration — separate sign-off)

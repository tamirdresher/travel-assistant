# POST /api/auth/login — Specification v1

**Status:** Draft v1 (folds sec-hard `login-threat-model` checklist into the contract).
**Owners:** app-dev (contract + impl), security-hardening (gate + semgrep + threat model), review-deployment (CI).
**Related:** [`docs/api/remember-me-api.md`](./remember-me-api.md) is the binding source of truth for `/refresh`, `/logout`, refresh cookie, family rotation, and lifetime constants. This spec defers to it on all those points.

---

## 1. Endpoint

- **Method + Path:** `POST /api/auth/login`
  (canonical path — grouped under `/api/auth/*` to match refresh-cookie `Path=/api/auth` and OpenAPI tag grouping)
- **Content-Type (request):** `application/json; charset=utf-8` — **strict**. Reject `application/x-www-form-urlencoded` with `415` (cross-origin form posts are a CSRF vector).
- **Max body size:** **4 KB** (login payload is tiny; larger = Argon2-DoS amplifier).
- **HTTP version:** Reject HTTP/0.9 and HTTP/1.0 at the edge.
- **Origin header:** **REQUIRED**. Must be in the allow-list (`https://<prod-host>`, `http://localhost:3000` in dev). Missing, `null`, or mismatch → 401 generic. This is the primary login-CSRF defense.
- **Authentication:** none (this endpoint *establishes* auth). No cookies, no bearer tokens consumed.

---

## 2. Request body

```jsonc
{
  "email": "string",      // required, 3–254 chars, lowercased + NFC + trimmed server-side
  "password": "string",   // required, 1–1024 chars (hard cap, reject before hash); no whitespace stripping
  "rememberMe": false     // optional, default false; ONLY accepted from body (never cookie/query)
}
```

- **Unknown fields → 400** (strict schema; blocks `isAdmin: true`-style mass-assignment).
- **Email normalization:** lowercase, NFC, trim. Do **not** strip Gmail `+aliases`.
- **Password normalization:** UTF-8 NFC. Do **not** strip whitespace.

---

## 3. Successful authentication — discriminated union response shape

Login response is a **discriminated union from day one** (MFA hook is designed in, not retrofitted). `status` is the discriminator.

### 3a. `authenticated` (no MFA required)

- **HTTP:** `200 OK`
- **Set-Cookie:** refresh cookie via `AppendRefreshCookie(ctx, env, token, lifetime)` — never inline. Lifetime = `rememberMe ? RefreshTokenLifetimes.Long (30d, 90d absolute cap) : RefreshTokenLifetimes.Short (8h)`.
- **Body:**

  ```json
  {
    "status": "authenticated",
    "accessToken": "<JWT>",
    "expiresIn": 900,
    "tokenType": "Bearer"
  }
  ```

- **DB side-effects:** new `FamilyId = Guid.NewGuid()`, `FamilyOriginAt = utcNow`, store **SHA-256(refreshToken)** in `TokenHash` (never raw).

### 3b. `mfa_required` (Phase 2-ready, shape is fixed now)

- **HTTP:** `200 OK`
- **Set-Cookie:** **none**. Refresh cookie is NEVER issued before MFA completes.
- **Body:**

  ```json
  {
    "status": "mfa_required",
    "mfaToken": "<opaque>",
    "expiresIn": 300,
    "methods": ["totp", "webauthn"]
  }
  ```

- `mfaToken` is server-side state, single-use, bound to `(userId, ip, ua-hash)`, max 5min TTL. Consumed by separate `POST /api/auth/mfa/verify` which then calls `AppendRefreshCookie`.

### 3c. Failure (any reason)

- **HTTP:** `401 Unauthorized`
- **Set-Cookie:** **none** (any Set-Cookie on a 401 is a bug — gated by `login-gate.yml`).
- **Body (byte-identical for every failure reason — see §5):**

  ```json
  { "status": "invalid_credentials" }
  ```

- **WWW-Authenticate:** `Bearer realm="ta", error="invalid_credentials"`
- **No `X-RateLimit-*` headers on 401** (they leak attempts-remaining → enumeration aid). Only attach them on 429.

---

## 4. Other status codes

| Status | Trigger | Body |
|--------|---------|------|
| `400 Bad Request` | Malformed JSON, unknown field, schema violation, missing required field, body > 4 KB | RFC 7807 problem+json, no email/password echo |
| `401 Unauthorized` | Any credential failure (see §5 internal sub-states) | `{ "status": "invalid_credentials" }` (NOT problem+json — fixed shape, identical bytes) |
| `415 Unsupported Media Type` | Content-Type ≠ `application/json` | problem+json |
| `429 Too Many Requests` | Per-IP or per-account rate limit exceeded | problem+json + `Retry-After`, `X-RateLimit-Limit/Remaining/Reset` |
| `503 Service Unavailable` | Rate-limiter store unreachable (fail-closed), or Argon2 concurrency semaphore queue full | `Retry-After` |
| `500 Internal Server Error` | Unhandled fault. **No** Argon2/EF/DB exception text in body | problem+json with `X-Correlation-Id` only |

**Migration note (RFC 7807 alignment):** Current `/api/auth/refresh` and `/logout` ship `{error, message}` per `docs/api/remember-me-api.md` @ 245e6d0. This spec introduces problem+json on 400/415/429/500 paths only. The 401 path stays `{ "status": "invalid_credentials" }` (fixed-shape, byte-identical — see §5). When the broader auth surface migrates to problem+json, `type` slugs MUST equal the old machine code to preserve client error-handling.

---

## 5. Internal failure sub-states (audit-only; externally indistinguishable)

All of these → externally identical `401 { "status": "invalid_credentials" }` with identical headers, identical Set-Cookie behavior (none), and **identical wall-clock timing** (see §6).

| Internal `outcome` | Cause |
|--------------------|-------|
| `InvalidCredentials` | Wrong password for known user |
| `UnknownUser` | Email not in DB |
| `AccountLocked` | Per-account lockout in effect |
| `EmailUnverified` | Verification pending (surface via separate post-login step, NOT here) |
| `DisabledAccount` | Admin-disabled |
| `RateLimited-Account` | Per-account counter tripped |
| `MfaTokenInvalid` | (only reachable from `/mfa/verify`, listed for completeness) |
| `SuspiciousAutomation` | Header heuristics (missing UA/Accept, curl/python-requests/Go-http-client UAs) |

Only `RateLimited-IP` is allowed to surface externally as `429` (because it is observable IP-wide regardless — hiding it gains nothing).

---

## 6. Timing & enumeration defenses

- **Dummy-hash path:** when user not found, still run Argon2id verify against a pre-computed throwaway hash loaded at startup. Never short-circuit on unknown user.
- **No early returns** between parse and verify other than 400/415 (which are syntactic and don't depend on email existence).
- Wall-clock target: **~250ms** per attempt regardless of branch taken. Argon2 parameters chosen to hit this on prod hardware.
- **Argon2id required.** BCrypt not accepted.
- **Argon2 concurrency cap:** `SemaphoreSlim(8)`. Beyond cap → 503 with `Retry-After: 1`. Prevents CPU DoS via concurrent-login flood.
- **Constant-time compare** on any string-equality check involving secrets.
- **No Argon2 parameter values** (memory, iterations, parallelism) returned in any response.

---

## 7. Rate limiting & lockout

Both layers are required. Both fail-**closed** with `503` if the limiter store is unreachable.

| Layer | Partition key | Limit | Window | Lockout effect |
|-------|---------------|-------|--------|----------------|
| Per-IP (all attempts) | `login:ip:{ip}` | 10 | 15 min | 429 |
| Per-account (failures only) | `login:account:{sha256(email)}` | 5 | 15 min | 15-min soft lock; externally indistinguishable from wrong password |

- **Email is SHA-256-hashed before partitioning.** Raw emails MUST NOT live in rate-limiter memory.
- Per-account counter **increments on `UnknownUser` too** — otherwise attackers harvest accounts by watching which emails trigger lockout.
- Reset window on **successful login** (not on password reset alone).
- Distributed limiter store required for any horizontal scale-out. Reuse `AspireWithSquad.RateLimiting` partition wiring.

---

## 8. Headers (every response, success or fail)

- `Cache-Control: no-store, no-cache, must-revalidate`
- `Pragma: no-cache`
- `X-Content-Type-Options: nosniff`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `X-Correlation-Id` — echo if supplied, else generate UUID
- `Content-Type: application/json` on success, `application/problem+json` on 4xx (except the 401 fixed-shape body which is `application/json`)
- On 401: `WWW-Authenticate: Bearer realm="ta", error="invalid_credentials"` (never `error="user_not_found"`)
- On 429 only: `Retry-After`, `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset`

---

## 9. Idempotency — explicitly NOT supported

`Idempotency-Key` header is **ignored** on `/login`. Login is intentionally non-idempotent: every replay = fresh attempt = fresh audit row + fresh rate-limit increment. An idempotency cache here would be a credential-stuffing accelerant.

Gated by `login-gate.yml` (rejects code that reads `Idempotency-Key` in the login pipeline).

---

## 10. Audit log

Append-only. PII-aware. Logged for **every** attempt, regardless of outcome.

| Field | Type | Notes |
|-------|------|-------|
| `timestampUtc` | ISO 8601, ms precision | |
| `emailHash` | SHA-256 hex | **Never raw email** |
| `userId` | Guid? | Only on success; `null` otherwise (don't leak existence to log readers) |
| `clientIp` | string | Resolved via RFC 7239 `Forwarded` with validated proxy chain — NOT raw `X-Forwarded-For` |
| `userAgent` | string | Truncated to 256 chars |
| `outcome` | enum | `Success \| InvalidCredentials \| UnknownUser \| AccountLocked \| RateLimited-IP \| RateLimited-Account \| MfaRequired \| EmailUnverified \| DisabledAccount \| SuspiciousAutomation` |
| `correlationId` | string | From `X-Correlation-Id` |
| `rememberMe` | boolean | Body value |
| `familyId` | Guid? | On `Success` only — ties to `RefreshTokens` row |

**Retention:** 90 days hot, archive thereafter.

**Forbidden in log (any sink, any level, including stack traces):** raw password, full JWT, full refresh token, raw email if hashing policy is active. Exception messages must be sanitized before they reach the log shipper.

**Alert thresholds:** >50 failures/min from single IP, >20 failures/min on single account, >5 successful logins from new geos in <1h on one account.

---

## 11. CSRF — login-specific

Standard refresh-flow CSRF defenses (SameSite + `X-TA-Refresh`) **do not apply** at login time because no cookie exists yet. Login uses:

1. **Origin header allow-list check** (required, fails to 401 if mismatch).
2. **Content-Type strict** (`application/json` only — blocks cross-origin form submissions).
3. **No support for `Origin: null`** (blocks `file://`, sandboxed iframes).

Optional second layer (if/when adopted): `X-TA-Auth: 1` custom header to force CORS preflight. Document if added.

After successful login, regenerate any server-side session identifier (n/a for current JWT-only stack, but reserved if server-session is ever introduced).

---

## 12. Bot defenses

- **CAPTCHA escalation:** after 3 IP failures in window, require Turnstile / hCaptcha token in body. Missing/invalid → 401 generic (don't 403 — leaks detection).
- **Header heuristics:** missing `User-Agent`, missing `Accept`, or UA matches `curl|python-requests|Go-http-client` → 401 generic + audit `SuspiciousAutomation`.
- **Honeypot field:** optional `website` field (hidden in HTML form). If populated, fake 200 OK with no cookie. Wastes attacker time without leaking detection.

---

## 13. Don't-do list (gated)

| ❌ Forbidden | Gated by |
|--------------|----------|
| Echo email/username in error body | `login-hygiene.yml` semgrep `raw-email-in-error-response` |
| Distinct status codes per failure mode | `login-gate.yml` |
| `Set-Cookie` on 401 | `login-gate.yml` + semgrep `Set-Cookie-in-401-path` |
| Log raw password (any level, any sink) | semgrep `raw-password-in-log-arg` |
| Accept `rememberMe` from query string or cookie | code review + semgrep |
| Issue refresh cookie before MFA completes | `login-gate.yml` |
| Trust raw `X-Forwarded-For` | code review |
| Allow `Origin: null` | code review + gate |
| Accept HTTP/0.9 / HTTP/1.0 on login | edge config |
| Return Argon2 parameters in any response | code review |
| Handle `Idempotency-Key` on login | `login-gate.yml` |
| Missing dummy-hash on user-not-found | semgrep `missing-dummy-hash-on-user-not-found` |

---

## 14. Password reset & email-change interactions

(Login-adjacent, not in this endpoint but binding on this spec.)

- Password reset → revoke **all** refresh-token families for user (RM-005 §6).
- Reset token: single-use, 15min TTL, SHA-256 at rest, bound to email-at-issue-time.
- Reset link MUST NOT auto-log-in. User re-enters new password on next login.
- Email-change requires current password AND revokes all refresh families.

---

## 15. Forgot-password / signup mirroring

These endpoints must mirror login's enumeration-resistance:

- `POST /api/auth/forgot-password` → always `202 Accepted`. Never reveal "no account with that email".
- `POST /api/auth/signup` → always `202 Accepted` with "if this email is new, we sent a verification". Never reveal "already registered".
- `/refresh`, `/logout`, `/me` → 401 identically whether cookie missing, malformed, expired, or revoked.

---

## 16. JWT details

- **Algorithm:** EdDSA or RS256. HS* not accepted.
- **`kid` header required.** Key store retains old `kid` for 15min grace after rotation. Unknown `kid` → 401, never 500.
- **`nbf`/`exp` skew tolerance:** ±60s. Document in code comment.
- **Lifetime:** 15min (`expiresIn: 900` in body).

---

## 17. Open items (sec-hard sign-off needed)

1. Confirm Argon2id parameters (memory, iterations, parallelism) for prod hardware → ~250ms target.
2. Confirm Origin allow-list values per environment (dev / preview / prod).
3. Confirm CAPTCHA provider (Turnstile vs hCaptcha) for §12.
4. Confirm new-device-email channel (existing notification service or new).
5. Confirm whether `clientIp` is hashed for GDPR-strict deployments.

---

## 18. Handoff to security-hardening

Per sec-hard's checklist, when this spec lands:

1. `login-gate.yml` workflow — assert generic 401 body, no `Set-Cookie` on 401, Origin check present, audit log fields complete, no `Idempotency-Key` handling.
2. `.semgrep/login-hygiene.yml` — ERROR rules: `raw-email-in-error-response`, `raw-password-in-log-arg`, `Set-Cookie-in-401-path`, `missing-dummy-hash-on-user-not-found`.
3. `docs/security/login-threat-model.md` — STRIDE + binding sign-off (mirrors `remember-me-threat-model.md`).

No implementation code lands until those three artifacts are in place.

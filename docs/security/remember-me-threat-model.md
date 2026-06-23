# Remember-Me Threat Model (RM-001..RM-008)

Status: **DECIDED** — sign-off contract in §8 is binding for RM-003/RM-004 merge.
Owner: security-hardening-squad. Last updated: 2026-06-23.

## 1. Scope

Login flow gains a "Remember me" checkbox. Frontend (apps/web) sends `rememberMe: bool` to
`POST /api/auth/login`. Backend (src/TravelAssistant.Api) issues access + refresh tokens
with TTL keyed off the flag. `POST /api/auth/refresh` honors the same window.

## 2. Asset inventory

| Asset                    | Sensitivity | Location                                     |
|--------------------------|-------------|----------------------------------------------|
| Access token (JWT)       | High        | In-memory only (frontend)                    |
| Refresh token (opaque)   | Critical    | **httpOnly Secure SameSite=Lax cookie**      |
| `rememberMe` UI choice   | Low         | localStorage `ta.auth.rememberMe` (bool)     |
| RefreshToken row         | Critical    | DB; carries `RememberMe`, `FamilyId`, `RevokedAt` |

## 3. STRIDE

| Threat | Vector | Mitigation |
|--------|--------|------------|
| **S**poofing | Stolen refresh token replayed from attacker browser | httpOnly cookie (not JS-readable) + rotation + reuse-detection revokes family |
| **T**ampering | `rememberMe` flipped in transit to extend session | Server is source of truth; cookie is HMAC-signed; client value ignored on /refresh |
| **R**epudiation | User denies long-lived session | Audit log: token issued/rotated/revoked with `FamilyId`, IP, UA |
| **I**nfo disclosure | Refresh token in localStorage → XSS exfil | **Rejected** localStorage path; cookie path only |
| **D**oS | Credential stuffing on /login, refresh flooding | Reuse `AspireWithSquad.RateLimiting` policies (see §5) |
| **E**oP | Stale refresh after password change still works | Server revokes all token families on password change / logout-all |

## 4. Decisions (D5)

### D5-1 — Access-token TTL: **Accepted** unchanged (15 min)
Access token TTL is **NOT** affected by `rememberMe`. Only refresh TTL differs.

### D5-2 — Refresh-token TTL: **Accepted with revision**
- `rememberMe=true`  → **30 days** sliding (rotated on each refresh, capped at 30d absolute from login).
- `rememberMe=false` → **8 hours** sliding (was proposed 1d — tightened: an "unchecked" session should not survive a workday + overnight on a shared device).
- Absolute cap on `rememberMe=true` family: **90 days** from initial login regardless of rotation — forces re-auth.

### D5-3 — Refresh-token storage: **Accepted: httpOnly cookie ONLY**
- Cookie name: `ta_rt`
- Flags: `HttpOnly; Secure; SameSite=Lax; Path=/api/auth; Max-Age=<ttl>`
- **Rejected**: storing refresh token in localStorage or sessionStorage (XSS-exfil).
- The `rememberMe` boolean (NOT the token) may be stored in localStorage via the typed setter in RM-003 — that's the user UX choice, not a credential.

### D5-4 — Rotation + reuse-detection: **Accepted, REQUIRED**
- Each `/refresh` invalidates the prior refresh token and issues a new one with the **same `FamilyId`**.
- If a revoked token is presented → revoke **the entire family** + audit-log + force re-login.
- `FamilyId` is a GUID minted at login; survives rotation.

### D5-5 — Revocation triggers: **Accepted, REQUIRED**
Server MUST revoke all active refresh-token families for a user on:
1. Password change
2. Explicit logout (current family only) and logout-all (all families)
3. Email change confirmed
4. Admin/abuse action
Implementation: `UPDATE RefreshTokens SET RevokedAt=NOW() WHERE UserId=@u AND RevokedAt IS NULL` (scoped by family for #2-current).

### D5-6 — Rate limits: **Accepted**
Reuse `AspireWithSquad.RateLimiting` (spec 2ee8534) with new policy entries:
- `POST /api/auth/login`    → 10/15min per IP + 5/15min per account (existing login policy).
- `POST /api/auth/refresh`  → 60/min per IP + 30/min per `FamilyId` (new — refresh is high-volume but per-family abuse = reuse-detection trigger).
- `POST /api/auth/logout`   → 30/min per IP.
Fail-closed-503 on Redis outage for /login; fail-open-with-audit on /refresh (do not lock users out of active sessions during partial outage).

### D5-7 — Cookie scoping: **Accepted**
- `Path=/api/auth` so the cookie is sent only to auth endpoints (not to /api/trips, /api/bookings, etc.).
- `Domain` left blank → host-only (no subdomain leakage).
- Production requires HTTPS; `Secure` flag enforced even in dev via middleware override gated on `ASPNETCORE_ENVIRONMENT != "Development"`.

## 5. CSRF posture
Refresh endpoint accepts the httpOnly cookie. To prevent CSRF on /refresh and /logout:
- Require custom header `X-TA-Refresh: 1` on POST /api/auth/refresh and /api/auth/logout. Browsers cannot set custom headers cross-origin without CORS preflight → effective CSRF gate without a synchronizer token.
- `SameSite=Lax` is defense-in-depth; the header check is the primary control.

## 6. RefreshToken schema (binding for RM-004)

```
RefreshTokens
  Id           GUID PK
  UserId       GUID  FK Users
  FamilyId     GUID  (survives rotation)
  TokenHash    BYTEA (SHA-256 of opaque secret; raw secret never stored)
  RememberMe   BOOL
  IssuedAt     TIMESTAMPTZ
  ExpiresAt    TIMESTAMPTZ
  AbsoluteExp  TIMESTAMPTZ  (login + 90d if RememberMe else login + 8h)
  RotatedToId  GUID NULL    (set when this token is rotated; reuse-detection key)
  RevokedAt    TIMESTAMPTZ NULL
  RevokedReason TEXT NULL   ('rotation'|'reuse-detected'|'password-change'|'logout'|'logout-all'|'admin')
  ClientIp     INET NULL
  UserAgent    TEXT NULL
```

Reuse-detection algorithm: on /refresh, if presented `TokenHash` matches a row where `RotatedToId IS NOT NULL` OR `RevokedAt IS NOT NULL` → revoke entire family (`UPDATE … WHERE FamilyId=@fam`), audit-log, return 401.

## 7. Frontend contract (binding for RM-003)
1. Refresh token MUST NOT touch JS — only the cookie carries it.
2. `ta.auth.rememberMe` localStorage write MUST route through a typed setter `setRememberMe(value: boolean)` (mirror DM-002 `setTheme.ts`).
3. Access token kept in module-scoped variable only; never in localStorage/sessionStorage.
4. Login POST body: `{ email, password, rememberMe: boolean }`.
5. Logout: POST /api/auth/logout (the cookie is sent + custom header). After 200, clear in-memory access token.

## 8. Sign-off contract (RM-003 + RM-004 merge gate)
Reviewer MUST verify ALL before approve:

- [ ] Refresh token issued as cookie only: `HttpOnly`, `Secure`, `SameSite=Lax`, `Path=/api/auth`. No `Set-Cookie` for refresh outside `/api/auth/*` controllers.
- [ ] No `localStorage.setItem` / `sessionStorage.setItem` of any string containing 'token', 'refresh', 'jwt', 'bearer' anywhere in apps/web (enforced by `.semgrep/remember-me-token-hygiene.yml`).
- [ ] `rememberMe` value on /refresh is read from the DB row, NOT from the request body.
- [ ] Reuse-detection unit test: presenting a rotated token revokes the whole family.
- [ ] Revocation tests: password-change revokes all families; logout revokes current; logout-all revokes all.
- [ ] xUnit covers both branches: `rememberMe=true` issues 30d, `rememberMe=false` issues 8h; rotation preserves `RememberMe` and `FamilyId`.
- [ ] CSRF: /refresh and /logout reject requests missing `X-TA-Refresh: 1` header (403).
- [ ] Rate-limit policies wired per §6 (D5-6); /login fail-closed-503, /refresh fail-open-audit on Redis outage.
- [ ] Absolute cap honored: `rememberMe=true` family rejected after 90d from initial login even if rotated.
- [ ] All access-token TTLs unchanged (15 min). Diff search confirms no edits to access-token expiry.

# LP-005: Threat model — "Remember last viewed page"

**Status:** LP-005: APPROVED
**Owner:** security-hardening-squad
**Binds:** LP-001 (XD typed setter), LP-002 (app-dev opt-out toggle), LP-003 (app-dev restore-on-boot)
**Storage key (locked):** `ta.nav.lastPage.v1`
**Opt-out key (locked):** `ta.nav.lastPage.optOut.v1`

## 1. Asset

A single localStorage entry recording the last route the user visited so the app re-opens on it. Shape:

```ts
type LastPage = { pathname: string; search: string; ts: number };
```

`pathname` and `search` are both same-origin relative strings. `ts` is `Date.now()` (epoch ms).

## 2. STRIDE

| Class | Threat | Mitigation | Owner |
|---|---|---|---|
| **S**poofing | Attacker writes a forged entry via another tab on the same origin (e.g. an injected script) so opening the app navigates the victim to an attacker-chosen route. | Path-only same-origin restore (no host/protocol). Reject on **read** as well as write — never trust storage state. | LP-003 |
| **T**ampering | User edits localStorage manually to `{pathname:"javascript:..."}` or `{pathname:"//evil.example/"}`. | Validator §3.1 rejects; restore falls back to `/`. | LP-003 |
| **R**epudiation | n/a — not an auditable action. | — | — |
| **I**nformation disclosure (telemetry) | Search params on travel-assistant carry PII: `?origin=TLV&dest=NRT&depart=2026-07-12&pax=2&cabin=BUSINESS`, plus potentially user-typed free text. Routing this into analytics events leaks PII to third-party telemetry sinks. | Telemetry events MAY carry `pathname` only. `search` is forbidden in any `track*` / `logEvent` / `telemetry` payload. Enforced by semgrep `no-lastpage-search-in-telemetry`. | LP-001, LP-002 |
| **I**nformation disclosure (token capture) | Search params for OAuth/magic-link/email-verify routes carry credentials: `?token=`, `?code=`, `?state=`, `?id_token=`, `?access_token=`, `?refresh_token=`, `?session=`, `?otp=`, `?magic=`. Storing these makes them retrievable by any later same-origin script. | Deny-list at write time (§5) AND scrub at read time. Setter strips matching params before persisting; reader re-validates and discards entry if any forbidden key is present. | LP-001 |
| **I**nformation disclosure (route disclosure) | An attacker with access to the device sees which last route the user visited (e.g. a private trip search). | Accepted residual risk — the same info already lives in browser history. Opt-out (§7) is the user-facing control. | — |
| **D**enial of service | Adversary stores a huge JSON blob (megabytes) under the key to slow `JSON.parse` on boot. | Setter clamps total serialized length to 2KB; reader treats `length > 2048` as invalid and clears the key. | LP-001 |
| **E**levation of privilege | Signed-out user is restored to an authenticated-only route (`/account`, `/trips/:id`, `/checkout/*`). Either (a) the protected page leaks state before its own auth gate fires, or (b) the post-login redirect can be hijacked to an attacker route. | Restore step MUST verify auth state BEFORE `router.replace`. Restore to a route on the auth-required allow-list when signed-out → fall through to default landing `/`. See §4. | LP-003 |
| **E**levation (stored XSS via pathname) | Crafted pathname like `javascript:alert(1)` or `data:text/html,...` or `//evil.example/path` passed to `router.replace`. | Validator §3.1 is **anchored** and **reject-on-read**. Encoded variants (`%2F%2Fevil`, `%6Aavascript:`) caught by post-decode re-validation. | LP-003 |

## 3. Validator (binding)

### 3.1 `isSafeRelativePath(value: unknown): value is string`

```ts
// Accept ONLY:
//   - starts with exactly one '/'
//   - body chars: A-Z a-z 0-9 / _ -
//   - optional query: '?' then chars: A-Z a-z 0-9 = & _ - % . ,
//   - max length 1024
const RE = /^\/[A-Za-z0-9/_\-]*(\?[A-Za-z0-9=&_\-%.,]*)?$/;
```

**Reject** if any of:
- `typeof value !== 'string'`
- `value.length > 1024`
- `!RE.test(value)`
- starts with `//` (protocol-relative) — covered by RE but assert explicitly
- contains `\` (Windows-style traversal)
- after `decodeURIComponent`, fails RE again (catches `%6Aavascript:`)

Validator must run **both** on write (setter) and on read (restore). LP-002 setter rejects silently (no-op); LP-003 reader clears the key and falls back to `/`.

### 3.2 Search-param deny-list (locked — share with XD for LP-001)

```ts
// Case-insensitive match on any URLSearchParams key name.
export const TOKEN_PARAM_DENYLIST = [
  /^token$/i,
  /^code$/i,
  /^state$/i,
  /^id_token$/i,
  /^access_token$/i,
  /^refresh_token$/i,
  /^session(_?id)?$/i,
  /^otp$/i,
  /^magic$/i,
  /^auth$/i,
  /^ticket$/i,
  /^assertion$/i,
  /^sig(nature)?$/i,
  /^jwt$/i,
];
```

Setter behavior: if **any** param key matches, persist `{pathname, search: '', ts}` (strip ALL params — do not selectively keep). This avoids partial-leak edge cases.

## 4. Auth-gated routes (binding for LP-003)

Routes that require an authenticated session — restore MUST verify auth state before navigating:

```
/account
/account/**
/trips/**
/checkout/**
/booking/**
/payments/**
/settings
/settings/**
```

If the stored pathname matches any of the above **and** the user is not authenticated at boot, **do not** call `router.replace(stored)`. Fall through to `/`. Do not store the deferred path for post-login redirect — that path would itself need a separate validation chain (out of scope for LP slice).

## 5. Routes where writing is forbidden (binding for LP-002)

Setter must no-op when called from any of these route files (semgrep heuristic flags violations):

```
**/login/**
**/signin/**
**/signup/**
**/register/**
**/auth/callback/**
**/oauth/**
**/verify-email/**
**/reset-password/**
**/checkout/confirm/**
**/payments/result/**
**/logout/**
```

Rationale: these are transient pages; landing here on app open is either confusing (login form when already signed in) or actively dangerous (replaying an OAuth callback URL).

## 6. Opt-out (binding for LP-002)

The opt-out toggle must be **genuine**:

1. Writing `optOut=true` MUST `removeItem('ta.nav.lastPage.v1')` in the same call (atomic with the toggle write).
2. While `optOut=true`, **all** writes via `setLastPage()` no-op.
3. Restore reader checks opt-out **first**; if true, returns null without reading the page key.
4. There is no third state. Boolean only. No "ask me later".

QT must assert all four behaviors (§8 item 6).

## 7. Boot performance / DoS

- Total serialized JSON length capped at 2048 bytes (writer enforces; reader re-checks).
- Reader uses `try/catch` around `JSON.parse`. On any throw → clear the key.
- Schema check: object with exactly `{pathname:string, search:string, ts:number}`. Extra keys → reject.

## 8. Sign-off contract (BINDING on LP-002 and LP-003 PRs)

Both PRs are blocked until ALL items below are demonstrated:

1. **`apps/web/src/nav/setLastPage.ts` exists** and is the single writer. All `localStorage.setItem('ta.nav.lastPage` calls route through it (enforced by semgrep `no-raw-lastpage-localstorage-write`).
2. **Storage key is the literal `'ta.nav.lastPage.v1'`** — no template strings, no concatenation (enforced by semgrep `no-dynamic-lastpage-key`).
3. **Validator §3.1 runs on write AND on read.** Test cases: `javascript:alert(1)`, `//evil.example`, `/ok`, `/ok?q=1`, `\\evil`, `/ok?token=abc`, `%6Aavascript:`, 1025-char string, non-string.
4. **Search-param deny-list (§3.2) strips ALL params** when ANY denied key is present. Test: `/auth/callback?code=X&next=/trips` → stored as `{pathname:'/auth/callback', search:'', ...}` (or rejected entirely if path is on §5 list — preferred).
5. **Auth-gated restore (§4)**: signed-out user with stored `/account` → boot lands on `/`, not `/account`. Test asserts no `router.replace('/account')` call.
6. **Opt-out (§6)**: toggling opt-out ON clears the existing key in the same operation; subsequent writes no-op; reader returns null. All 4 sub-behaviors covered by tests.
7. **No `search` in telemetry**: any `track*` / `logEvent` / `telemetry.*` call carrying lastPage data must use `pathname` only (enforced by semgrep `no-lastpage-search-in-telemetry`).
8. **No setter call from §5 route files**: enforced by semgrep `no-lastpage-write-on-deny-list-route` (heuristic flag → reviewer signs off).

---

LP-005: APPROVED

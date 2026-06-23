# LP-001 — "Remember last viewed page" UX contract

Status: **Locked** (XD, 2026-06-23). Unblocks LP-002 (storage + hook), LP-003 (route guard), LP-005 (sec-hard threat model).
Owner: experience-design-squad. Consumers: application-development-squad, security-hardening-squad, quality-testing-squad.

This document is the single source of truth for the **client-side restore flow**. Server-side / cross-device restore is explicitly out of scope (see D3).

---

## 1. Cold-open restore decision tree

> **First-paint rule (non-negotiable):** the server / static shell ALWAYS renders the `/` route. Restore happens **after** client hydration. No SSR/CSR mismatch. No flash-of-wrong-page. The user may see `/` for ~1 frame before the client replaces it — that is the contract, not a bug.

```
                       ┌─────────────────────────────┐
                       │  App boots → render `/`     │
                       │  skeleton (SSR or static)   │
                       └──────────────┬──────────────┘
                                      │
                              hydration complete
                                      │
                                      ▼
                       ┌─────────────────────────────┐
                       │ read `ta.lastPage` from     │
                       │ localStorage (typed reader) │
                       └──────────────┬──────────────┘
                                      │
            ┌─────────────────────────┼───────────────────────────┐
            │                         │                           │
            ▼                         ▼                           ▼
   ┌────────────────┐        ┌────────────────┐         ┌────────────────────┐
   │ no value /     │        │ value present  │         │ value present      │
   │ malformed JSON │        │ AND valid      │         │ BUT invalid        │
   │ AND opt-out OFF│        │ (see §1.1)     │         │ (see §1.2)         │
   └───────┬────────┘        └───────┬────────┘         └─────────┬──────────┘
           │                         │                            │
           ▼                         ▼                            ▼
   stay on `/`             router.replace(stored)         router.replace(`/`)
   no toast                no toast                       + toast (§3)
                           no SR announce                 + clear stored value
                                                          + telemetry event
```

### 1.1 What makes a stored route **valid**

ALL must hold:

1. **Pathname is in the route manifest.** The route is still mounted in the current build (compare against the Next.js `app/` directory output captured at build time — LP-002 surfaces the list).
2. **Auth requirement satisfied.** If the route is auth-gated and the user is currently signed out, it is invalid. (Route-guard ownership is LP-003.)
3. **Required search params resolvable.** A route may declare a list of required search-param keys (e.g. `/trips/search` requires `from`, `to`, `depart`). If any required key is missing or empty, invalid.
4. **Pathname is not on the deny-list (D2).**
5. **Stored payload schema-valid** (see §4).

If 1 + 2 + 3 + 4 + 5 → **navigate**. Otherwise → **fall back**.

### 1.2 Invalid-restore failure modes (all route to fallback + toast)

| Failure | Trigger | Telemetry reason |
|---|---|---|
| Route removed | Pathname not in manifest (deploy removed it) | `route_removed` |
| Auth required | Route gated, user signed out | `auth_required` |
| Missing params | Required search params missing | `missing_params` |
| Deny-listed | Pathname matches D2 regex | `deny_listed` |
| Schema invalid | JSON parse failed / wrong shape | `schema_invalid` |
| Storage error | localStorage threw (quota / disabled) | `storage_error` |

`storage_error` and `schema_invalid` do **not** show a toast (user never opted in to expect a restore). Other reasons **do**.

---

## 2. Locked decisions

### D1 — Scope: pathname + search params ONLY

Store: `{ pathname: string, search: string, savedAt: number }`.

**Explicitly excluded from v1:**

- ❌ Scroll position (UX bug magnet on infinite-scroll lists; defer to v2 behind an explicit per-route opt-in)
- ❌ Form state (privacy risk: drafts may contain PII; conflicts with auth/payment routes)
- ❌ Modal / drawer / dialog open-state (most are transient UI; restoring them on cold open is jarring)
- ❌ Hash fragment (`location.hash`) — anchor-jumps reset on reload anyway; storing them implies a guarantee we can't keep

If a consumer needs scroll/form/modal restoration, that is a **separate** feature with its own UX contract. Do not bolt it onto LP-001.

### D2 — Deny-list (never stored, never restored)

Stored as a regex array shipped in `packages/web/src/last-page/deny-list.ts` (path is normative — LP-002 lands the file).

```ts
// docs/wireframes/last-page/deny-list.ts — normative copy, LP-002 mirrors this verbatim
export const DENY_LIST: readonly RegExp[] = [
  /^\/login\/?$/,
  /^\/signup\/?$/,
  /^\/logout\/?$/,
  /^\/auth(\/|$)/,           // covers /auth, /auth/verify, /auth/reset, etc.
  /^\/oauth\/callback\/?$/,
  /^\/checkout\/confirm\/?$/, // post-payment landing — re-visiting is wrong (and may double-charge UX-wise)
  /^\/_next(\/|$)/,
  /^\/api(\/|$)/,
];

// Search-param deny — applied to BOTH pathname store AND restore decision.
// If ANY of these keys appears, do not store and do not restore.
export const DENY_SEARCH_KEYS: readonly string[] = [
  'token',     // email verify, password reset, magic link
  'code',      // OAuth authorization code
  'state',     // OAuth CSRF token
  'session',   // session-handoff parameter
  'otp',
];
```

**Both checks run on write AND on read.** Belt-and-braces — if the deny-list grows between a write and a later read (e.g. a hotfix adds `/admin` to the list), the read-time check protects users on the old store.

### D3 — Per-device only in v1

- Storage: `localStorage` key `ta.lastPage`.
- No server sync. No cross-device restore. No account-bound storage.
- v2 stretch (NOT this PR): account-bound restore via `/me/preferences` round-trip, gated on the same D4 opt-out.

Rationale: cross-device restore needs a server-side privacy review (does "last viewed page" leak browsing history across the user's family-shared account?), conflict-resolution UX (which device wins?), and probably a "trusted device" UX. None of that is needed to ship LP-001 value.

### D4 — Opt-out: Settings → Privacy

Settings → Privacy section gets one new control:

```
┌──────────────────────────────────────────────────────────────┐
│ ☑  Remember the last page I was on                            │
│    When you reopen Travel Assistant, we'll take you back      │
│    to the page you were last viewing on this device.          │
│    Stored only on this device. Not shared across devices.     │
└──────────────────────────────────────────────────────────────┘
```

- Default: **ON** (checked).
- DOM contract (load-bearing, do not rename): `id="settings-remember-last-page"`, `name="rememberLastPage"`, `data-testid="settings-remember-last-page"`.
- Native `<input type="checkbox">` + native `<label for>`. No ARIA shims. (DM-001 + RM-002 precedent.)
- Persistence key for the **preference itself** (separate from the stored route): `ta.lastPage.enabled` ∈ `"true"` | `"false"`. Absent = treated as `"true"` (default-on).
- **On flip ON → OFF:** immediately call `localStorage.removeItem('ta.lastPage')`. Future writes are no-ops. No confirmation dialog (reversible, low blast radius). Emit `lastpage.optout.changed` with `{ enabled: false }`.
- **On flip OFF → ON:** future writes resume. Do NOT retroactively populate a value. Emit `lastpage.optout.changed` with `{ enabled: true }`.
- Microcopy is final. Specifically not "Remember my activity" (too broad, sounds like full history) and not "Resume where I left off" (implies form/scroll restore — we don't do that, D1).
- Hint copy lives inside `<label>` as the second line via `<small>` element. Not `aria-describedby` (matches RM-002 D5 precedent — SR reads label fully, no tooltip-only affordance).

### D5 — Privacy: pathname only in telemetry

- Telemetry **never includes search params, never includes pathname dynamic segments resolved to values**.
- Allowed in events: route **template** (e.g. `/trips/[tripId]`), NOT the resolved pathname (`/trips/abc-123`).
- For travel-assistant specifically, search params routinely carry: city names (`from=TLV&to=JFK`), dates (`depart=2026-07-04`), passenger counts (`pax=2`), traveler initials (`name=T.D.`). These are **PII or PII-adjacent** under most interpretations. Never log them.
- localStorage IS allowed to hold the resolved pathname + search verbatim — that's required for the feature to work and stays on-device.

Events (full list — LP-002 / LP-006 implement):

| Event | When | Payload |
|---|---|---|
| `lastpage.write` | Successful store | `{ routeTemplate: string }` |
| `lastpage.restore.attempted` | On cold open, after read | `{ hadStoredValue: boolean }` |
| `lastpage.restore.succeeded` | Navigation complete to stored route | `{ routeTemplate: string }` |
| `lastpage.restore.failed` | Fallback to `/` | `{ reason: <see §1.2 table>, routeTemplate?: string }` |
| `lastpage.optout.changed` | D4 toggle flipped | `{ enabled: boolean }` |

**Route-template resolution:** the Next.js `usePathname()` returns the resolved path. LP-002 must include a template-mapper utility that walks the route manifest and replaces dynamic segments with their bracketed names. If no template can be resolved (e.g. catch-all under feature flag), emit `routeTemplate: "<unknown>"` — never the raw value.

---

## 3. Restore-failure toast

When restore fails and the failure is **user-visible** (see §1.2 — schema/storage errors are silent):

```
┌───────────────────────────────────────────────────────────┐
│ We couldn't reopen your last page.            [Dismiss ✕] │
└───────────────────────────────────────────────────────────┘
```

- DOM: `<div role="status" aria-live="polite" data-testid="lastpage-restore-failed-toast">…</div>`
- **`role="status"`, `aria-live="polite"`** — NOT `role="alert"`. This is informational; nothing failed that the user must act on. Per WAI-ARIA practices, `alert` is for critical/time-sensitive content and interrupts SR users mid-utterance. Polite status is correct.
- Auto-dismiss: 5 seconds. Manual dismiss button always present.
- Mount: 1 frame AFTER the navigation to `/` completes, so the SR announces "Home" first then the toast. Reverse order causes the toast to be drowned out.
- Visual: matches existing app toast component (DM-001 `--color-bg-elevated` + `--color-border-subtle` + `--color-text-primary`). Use `--color-warn` icon prefix if a leading icon is present; do NOT use `--color-danger` (red implies the user did something wrong).
- Does not reappear on subsequent navigations in the same session. One toast per cold open, max.
- Reduced-motion: no slide-in transition under `prefers-reduced-motion: reduce`. Instant appear (DM-001 vestibular hazard precedent).
- Touch target on dismiss: ≥44px mobile / ≥32px desktop (matches DM-001 + RM-002).

---

## 4. Stored-value schema (normative)

```ts
// localStorage key: "ta.lastPage"
type StoredLastPage = {
  v: 1;                     // schema version. ALWAYS present. LP-002 reader rejects anything where v !== 1.
  pathname: string;         // e.g. "/trips/abc-123". MUST start with "/". MUST NOT include origin.
  search: string;           // e.g. "?from=TLV&to=JFK". May be "" (empty string, NOT undefined).
  savedAt: number;          // Date.now() at write time. UTC ms epoch. Used for staleness gate (see §4.1).
};
```

### 4.1 Staleness gate

- If `Date.now() - savedAt > 30 days` → treat as invalid, reason `stale`. Add `stale` to the §1.2 table.
- Rationale: a user returning after a month likely doesn't want to be dropped into a deep search-results page from a forgotten trip. Land on `/` instead.
- 30 days is the locked number. Negotiable if sec-hard pushes back in LP-005, but XD recommendation is 30d.

### 4.2 Reader behavior

The reader function must be **total** (never throws into the React tree):

```ts
function readLastPage(): StoredLastPage | null {
  try {
    const raw = localStorage.getItem('ta.lastPage');
    if (raw === null) return null;
    const parsed = JSON.parse(raw);
    if (typeof parsed !== 'object' || parsed === null) return null;
    if (parsed.v !== 1) return null;
    if (typeof parsed.pathname !== 'string' || !parsed.pathname.startsWith('/')) return null;
    if (typeof parsed.search !== 'string') return null;
    if (typeof parsed.savedAt !== 'number' || !Number.isFinite(parsed.savedAt)) return null;
    return parsed as StoredLastPage;
  } catch {
    return null; // JSON.parse threw or localStorage threw (private mode / quota)
  }
}
```

### 4.3 Writer behavior

- Debounce: 500ms trailing-edge per route change. (Rapid client-side navigation — e.g. step-through wizard — should not thrash localStorage.)
- Idempotent: if the value to write equals the current stored value byte-for-byte, skip the `setItem` call entirely (avoids spurious `storage` events on other tabs).
- Errors swallowed silently (private mode / quota / disabled storage). Emit nothing user-visible. Optionally telemetry `lastpage.write` with a `failed: true` flag (LP-006 decides).

---

## 5. Test selectors (locked — QT writes E2E against these)

| Element | Selector | Notes |
|---|---|---|
| Settings opt-out checkbox | `[data-testid="settings-remember-last-page"]` | Native checkbox, `name="rememberLastPage"` |
| Restore-failure toast | `[data-testid="lastpage-restore-failed-toast"]` | `role="status"`, contains exact string "We couldn't reopen your last page." |
| Toast dismiss button | `[data-testid="lastpage-restore-failed-toast"] [data-testid="toast-dismiss"]` | aria-label="Dismiss" |

QT does **not** need to test the localStorage contents directly — that's a unit-test concern in LP-002. E2E only asserts the user-visible outcomes (URL after cold open + toast presence/absence).

### 5.1 E2E test matrix (handoff to QT for LP-004)

| # | Setup | Cold-open expectation | Toast? |
|---|---|---|---|
| 1 | No stored value | URL = `/` | No |
| 2 | Stored value = `/trips` (valid) | URL = `/trips` after hydration | No |
| 3 | Stored value = `/trips/search?from=TLV&to=JFK` (valid) | URL = `/trips/search?from=TLV&to=JFK` | No |
| 4 | Stored value = `/removed-route` (404) | URL = `/`, telemetry reason `route_removed` | Yes |
| 5 | Stored value = `/account` (auth-gated), user signed out | URL = `/`, telemetry reason `auth_required` | Yes |
| 6 | Stored value = `/auth/verify?token=abc` (deny-listed AND has token) | Value should never have been stored; if injected manually, URL = `/`, reason `deny_listed`. No leakage of `token` to telemetry. | Yes |
| 7 | Stored value = `/login` (deny-listed) | URL = `/`, reason `deny_listed` | Yes |
| 8 | Stored value = `{ v: 2, ... }` (future schema) | URL = `/`, reason `schema_invalid` | No |
| 9 | Stored value = `{ v: 1, savedAt: <31d ago>, ... }` | URL = `/`, reason `stale` | Yes |
| 10 | Opt-out OFF, stored value present | URL = `/`, stored value cleared by hook on mount | No |
| 11 | Opt-out toggled OFF mid-session, then cold open | URL = `/`, no stored value present | No |
| 12 | `localStorage.setItem` throws (quota) | URL = `/`, no crash, no toast | No |

---

## 6. Accessibility summary

- All new interactive elements use native HTML semantics (`<input type="checkbox">`, native `<button>` for toast dismiss). No custom widgets, no ARIA shims.
- Toast is `role="status"` polite (§3). Never `role="alert"`.
- Focus: restore navigation MUST move focus to the page's `<h1>` (matches existing app post-navigation focus contract — confirm with app-dev in LP-003; if no such contract exists, LP-003 is responsible for establishing it). Toast does NOT steal focus.
- Reduced motion respected on toast entrance.
- Touch targets ≥44px mobile / ≥32px desktop.
- All copy: plain-language, 8th-grade reading level. No jargon ("restore", "session", "state" avoided in user-facing strings).

---

## 7. Out of scope (explicit non-goals for LP-001)

- Cross-device restore (D3, v2)
- Scroll position restoration (D1, separate feature)
- Form-draft restoration (D1, separate feature with privacy review)
- Modal/drawer reopening (D1, separate feature)
- A "go back to last session" CTA on `/` (different feature — pull vs push)
- Multi-tab coordination — last write wins. If user has the app open in two tabs and navigates in both, the more recent navigation's value is what restores on cold open.

---

## 8. Handoff

- **LP-002 (app-dev):** implement `packages/web/src/last-page/` — `deny-list.ts`, `storage.ts` (reader/writer from §4), `useLastPageRestore.ts` hook driving §1 decision tree. Mirror the deny-list verbatim from this doc.
- **LP-003 (app-dev):** integrate restore into route-guard / app-shell so it runs post-hydration before any user interaction. Establish the post-navigation focus-on-`<h1>` contract if not already in place.
- **LP-004 (QT):** E2E from §5.1. Unit tests on `readLastPage` covering every §4.2 rejection path.
- **LP-005 (sec-hard):** threat model. Specifically: (a) localStorage as a write target from malicious extensions / XSS — does our restore widen any attack surface? (b) deny-list completeness against the current route table. (c) confirm 30d staleness gate is acceptable; if not, propose alternative. (d) confirm no need to encrypt at rest (XD says no — pathname is not a secret).
- **LP-006 (app-dev):** wire telemetry events from §2 D5. Server-side OTel counter pattern (DM-006 precedent).
- **LP-007 (rev-deploy):** PR rollup, transplant XD branch via `tamirdresher` keyring (EMU still blocks `tamirdresher_microsoft`).

---

## 9. Open questions routed out

- **To sec-hard (LP-005):** ratify 30d staleness gate; ratify deny-list; flag any additional search-param keys to add to `DENY_SEARCH_KEYS`.
- **To app-dev (LP-003):** confirm existing post-navigation focus convention OR establish it now.
- **To planning:** v2 cross-device restore — when, and gated on what? Not blocking this PR.

— experience-design-squad, 2026-06-23

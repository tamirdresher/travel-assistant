# Remember Last Viewed Page — Restore Flow (LP-001)

**Status:** Locked  
**Owner:** experience-design-squad  
**Epic:** LP-001..007  
**Target repo:** tamirdresher/travel-assistant (`apps/web`, Next.js App Router)  
**Sibling specs:** `apps/web/src/navigation/lastPage.denylist.ts` (this PR), `docs/security/last-viewed-page-threat-model.md` (LP-005)

---

## 1. Purpose

When a user closes and reopens the app on the same device, return them to the page they were last viewing — unless doing so would leak data, break auth, or surprise them. This document is the binding UX contract for LP-002 (storage), LP-003 (route guard / restore hook), LP-004 (settings toggle), LP-005 (security), and LP-006 (test matrix).

The default UX shape is **silent restore**: most users should never notice this feature; it should just feel like the app remembered. Restore failures are the only visible surface, and they are non-blocking.

---

## 2. First-paint contract (non-negotiable)

> **First paint is ALWAYS `/` skeleton. Restore happens after client hydration.**

Rationale:

- Next.js App Router SSR cannot read `localStorage`. Any attempt to restore server-side produces an SSR/CSR mismatch and a flash-of-wrong-page on slow clients.
- A skeleton at `/` is what un-restored cold-open users already see today — restore is purely additive.
- Restoring after hydration costs one `router.replace()` call (no full reload, no second SSR roundtrip).

What this means for LP-003:

- `useRestoreLastPage()` MUST be a client-only effect that fires once on app mount.
- It MUST NOT block first paint, suspense, or hydration. No throwing during render. No suspense boundary.
- If hydration fails, restore silently no-ops (user stays on `/`).

What this means for LP-006:

- E2E asserts first paint is the `/` skeleton, then asserts URL bar updates to the restored route after hydration completes. Playwright tracing or `page.waitForURL` after `domcontentloaded`.

---

## 3. Decision tree (cold open)

```
On app mount (client only, after hydration)
│
├─ Opt-out is OFF (ta.privacy.rememberLastPage === false)?
│  └─ YES → no-op. Stay on `/`. No toast. No telemetry.
│
├─ User arrived via deep link (history.length === 1 && pathname !== '/')?
│  └─ YES → no-op. They explicitly navigated. Stay on the deep-linked URL.
│           DO NOT overwrite their intent with a stored route.
│           (Note: do NOT clear stored value — they may close this tab and
│            reopen `/` later, at which point restore should still work.)
│
├─ getLastPage() === null?
│  └─ YES → no-op. Stay on `/`. No toast.
│
├─ Stored pathname matches deny-list (see §4)?
│  └─ YES → clearLastPage(). Stay on `/`. No toast.
│           (Stale value from before deny-list expanded — quietly drop it.)
│
├─ Stored pathname is auth-gated AND user is signed out?
│  └─ YES → clearLastPage(). Stay on `/`. No toast.
│           (Signed-out users see `/` as the natural landing. A toast here
│            would be noisy and confusing — they didn't ask to go anywhere.)
│
├─ router.replace(stored.pathname + stored.search) → 404 or throws?
│  └─ YES → clearLastPage(). Show toast (see §6). URL stays `/`.
│
└─ All checks pass
   └─ router.replace(stored.pathname + stored.search). Silent. No toast.
```

**Implementation note for LP-003:** the 404 path is detectable only after the navigation resolves (Next.js renders the not-found segment). Wire the toast off the `not-found.tsx` boundary or a `useEffect` in the page that detects a stored-restore origin. Use a sessionStorage breadcrumb `ta.nav.lastPage.restoring` set right before `router.replace` and cleared on the destination page's mount or in `not-found.tsx`; if `not-found.tsx` sees the breadcrumb, it triggers the toast and `router.replace('/')`.

---

## 4. Locked decisions

### D1 — Scope: pathname + search params, nothing else (v1)

**Stored:** `pathname` (e.g. `/flights/search`) + `search` (e.g. `?from=TLV&to=JFK&date=2026-08-01`).

**NOT stored in v1** (explicit non-goals — do not "just add" any of these without a new RFC):

- Scroll position
- Form field state (drafts, partial inputs)
- Modal / dialog open state
- Tab state within a page (`<Tabs>` selection)
- Hash fragment (`#section-2`) — pathnames only, no `#`

Rationale: each of these has its own state-restoration semantics and failure modes (form state can be stale or invalid; modals can be auth-gated; tabs are component-local). Adding them piecemeal creates a quagmire. v2 RFC if requested.

### D2 — Deny-list (routes that MUST NOT be stored)

Authoritative list lives in `apps/web/src/navigation/lastPage.denylist.ts` (shipped alongside this doc, see §9). Conceptually:

**Pathname patterns (regex, anchored to start of pathname):**

- `^/login(/|$)`
- `^/signup(/|$)`
- `^/logout(/|$)`
- `^/auth(/|$)` — covers `/auth/verify-email`, `/auth/reset`, etc.
- `^/oauth/`  — `/oauth/callback`, `/oauth/start`
- `^/checkout/confirm(/|$)` — payment confirmation is single-use
- `^/_next/` — Next.js internals (defensive)
- `^/api/` — API routes (defensive; should never be a viewed page anyway)

**Search-param patterns (any match → skip, case-insensitive):**

- `[?&]token=`
- `[?&]code=` — OAuth authorization code
- `[?&]id_token=`
- `[?&]state=` — OAuth state, also CSRF state
- `[?&]access_token=`
- `[?&]refresh_token=`
- `[?&]session=`
- `[?&]otp=`
- `[?&]password=` — defensive; should never appear in URL but if a misconfigured form GETs, drop it

**Behavior:** deny-list is enforced on **both write AND read**. Write enforcement prevents new entries. Read enforcement protects against rollouts where the deny-list expands (a stored value that's now denied is dropped silently on next mount).

### D3 — Per-device only (v1)

- `localStorage`, key `ta.nav.lastPage.v1`. Single device. Single browser. Single profile.
- No server sync. No cookie. No cross-device.
- Cross-device restore (e.g., close on desktop, reopen on mobile) is a v2 RFC. It requires API surface, identity tie, and a privacy review materially larger than v1.

### D4 — Opt-out: Settings → Privacy → "Remember the last page I was on" (default ON)

- Default: **ON** (this feature is privacy-low-risk per LP-005 deny-list + per-device scope).
- Storage key: `ta.privacy.rememberLastPage` (boolean string `"true" | "false"`, missing/malformed → treat as `true`).
- When toggled **OFF**: setLastPage becomes a no-op AND existing stored value is cleared **immediately** (synchronous in the toggle handler). LP-004 ships the UI.
- When toggled **ON** (from OFF): start writing from the next navigation. Do NOT backfill the current page synchronously — wait for the next route change, so the user understands the toggle takes effect going forward.

Copy (locked, for LP-004):

```
Remember the last page I was on
When you close and reopen Travel Assistant on this device,
we'll take you back to where you left off.
This only works on this browser. Sign-in, sign-up, and
payment pages are never remembered.
```

Microcopy constraints: ≤ 250 chars for the helper. No tooltip — always-visible helper text below the toggle. Matches RM (Remember Me) pattern.

### D5 — Privacy: pathname goes to telemetry, search params do NOT

- Any telemetry event about restore (success, failure, skipped) MAY include the pathname.
- Telemetry MUST NEVER include the search string. Travel-assistant search params carry PII: origin/destination cities, dates, passenger counts, fare classes, sometimes traveler names.
- LP-005 semgrep rule `no-lastpage-search-in-telemetry` enforces this at CI.
- Telemetry event names pre-allocated (server-side OTel counters, matching DM-006 / RM-005 pattern — no client beacon):
  - `nav.lastpage.stored` (counter, attribute: `pathname`)
  - `nav.lastpage.restored` (counter, attribute: `pathname`)
  - `nav.lastpage.restore_skipped` (counter, attribute: `pathname`, `reason` ∈ {`opt_out`, `deep_link`, `none_stored`, `deny_list`, `auth_gated`})
  - `nav.lastpage.restore_failed` (counter, attribute: `pathname`, `reason` ∈ {`not_found`, `threw`})

### D6 — No animation on restore

- `router.replace()` swaps the route in place. No transition, no fade, no "Welcome back" splash.
- Restore is meant to feel like the app remembered, not like a guided tour.
- Reduced-motion users get the same UX (no special case).

---

## 5. Accessibility contract

### 5.1 Restore happens silently

- Successful restore makes **no** screen-reader announcement of its own. The destination page's existing title / heading / live regions are the only SR-visible change.
- Rationale: announcing "Restored your last page" on every cold open is noisy and infantilizing for daily users. The URL change + heading change is sufficient.

### 5.2 Restore-failure toast

- Surface: existing app toast region.
- ARIA: `role="status"` `aria-live="polite"`. **NOT** `role="alert"` — this is informational, not urgent.
- Dismissable. Auto-dismiss after 8s.
- Focus is NOT moved. The toast appears, the polite live region reads it once, focus stays where the user expects (typically the first focusable element on `/`).
- Copy (locked):

  ```
  We couldn't reopen your last page. You're back on home.
  ```

  No exclamation marks. No emoji. No "Sorry!" — neutral, factual.

- Test selector: `data-testid="lastpage-restore-failed-toast"`.

### 5.3 Settings toggle (LP-004)

- Native `<input type="checkbox">` + native `<label for>`. No ARIA shims.
- Touch target ≥ 44px mobile / ≥ 32px desktop (matches DM-001 / RM-002).
- Focus ring uses `--color-focus-ring` (DM-001 token).
- Toggling fires no SR announcement beyond the native checkbox state change.

### 5.4 Reduced-motion / NO_COLOR / SR_MODE

- Restore is invisible to all three modes — there is nothing to suppress.
- Toast renders in all modes (it's text-only, no animation beyond the existing toast region's enter/exit, which is already reduced-motion-aware per app convention).

---

## 6. Test selectors (locked — do not rename without QT signoff)

| Surface | Selector | Owner |
|---|---|---|
| Restore-failed toast | `[data-testid="lastpage-restore-failed-toast"]` | LP-003 |
| Settings toggle input | `[data-testid="settings-remember-lastpage"]` (also `name="rememberLastPage"`, `id="settings-remember-lastpage"`) | LP-004 |
| Settings toggle label | `label[for="settings-remember-lastpage"]` | LP-004 |
| Settings toggle helper | `[data-testid="settings-remember-lastpage-hint"]` | LP-004 |
| sessionStorage restore breadcrumb (internal) | key `ta.nav.lastPage.restoring` (any truthy value) | LP-003 |

QT writes Playwright/RTL queries against these. Renaming any of them is a breaking change.

---

## 7. Edge cases handled

| # | Scenario | Behavior |
|---|---|---|
| E1 | User on `/flights/search?from=TLV` closes tab, reopens `/` | Restore → `/flights/search?from=TLV` (silent) |
| E2 | User on `/account/settings` closes, logs out elsewhere, reopens `/` | Stored value cleared on next mount (auth-gated check), stay on `/`, no toast |
| E3 | User on `/login` closes, reopens `/` | `/login` was deny-listed → never stored → land on `/` |
| E4 | User on `/oauth/callback?code=abc` closes, reopens `/` | `?code=` deny-listed → never stored → land on `/` |
| E5 | User deep-links to `/vacations/123` from email | `history.length === 1 && pathname !== '/'` → restore skipped → user stays on `/vacations/123`; stored value (if any) NOT cleared |
| E6 | Opt-out toggled OFF mid-session | setLastPage no-ops; existing stored value cleared synchronously on toggle |
| E7 | Stored route `/promotions/expired-deal` returns 404 | not-found.tsx detects restoring breadcrumb → toast → `router.replace('/')` → stored value cleared |
| E8 | Stored value malformed JSON or > 2KB | getLastPage returns null (LP-002 contract) → no-op |
| E9 | Safari private mode (localStorage throws on write) | setLastPage swallows → app continues; restore never has data → no-op |
| E10 | User has multiple tabs open, closes one on `/flights`, closes one on `/hotels` last | Last write wins (whichever tab navigated most recently) — acceptable for v1 |
| E11 | New install (no stored value) | No-op. No toast. Land on `/` as today. |
| E12 | Stored value points to a route that now requires a feature flag the user lacks | App's existing feature-flag guard renders not-found OR redirect-to-`/` → toast fires (treated as not-found from restore's perspective) |
| E13 | User clicks browser back after restore | Browser history contains: `/` (initial) → `/flights/search` (replaced). Back goes to `/`. Acceptable — `router.replace` (not `push`) is correct. |
| E14 | Stored value's search params include a now-removed param the app rejects | Route still mounts (Next.js doesn't 404 on unknown search params). App-level handling is the app's problem; restore considers it success. |

---

## 8. Non-goals (v1) — do not implement

- Restoring scroll position (D1)
- Restoring form state (D1)
- Cross-device restore (D3)
- "Welcome back" UI / animation (D6)
- "Last 5 pages" history view
- Pinning a "home page" different from `/`
- Per-route opt-out (the global toggle is the only opt-out)
- Restoring on hard refresh of a deep link (would override user intent — see E5)

---

## 9. Companion artifact: deny-list module

Ships in this same PR at `apps/web/src/navigation/lastPage.denylist.ts` so semgrep, setter, and tests all import the same source of truth. See file.

---

## 10. Handoffs

| Squad | Owes | Blocked-on |
|---|---|---|
| app-dev | LP-002 setter consuming this deny-list; LP-003 hook implementing §3 tree; LP-004 settings UI per §5.3 + D4 copy | this doc (now ratified) |
| security-hardening | LP-005 threat model citing §4 (D2) deny-list verbatim; semgrep rules including `no-lastpage-search-in-telemetry` (D5) | this doc (now ratified) |
| quality-testing | LP-006 test matrix already drafted — E1..E14 from §7 should map to E2E scenarios | this doc (now ratified) |
| review-deployment | LP-007 gate enforces invariants: storage key literal `ta.nav.lastPage.v1`, setter path `apps/web/src/navigation/setLastPage.ts`, deny-list path `apps/web/src/navigation/lastPage.denylist.ts` | nothing — go |

---

## 11. Versioning

This is v1 of the UX contract. Material changes (any change to D1–D6, the deny-list, the failure toast copy, or §2) require an RFC + ratification by ideation-research-planning. Cosmetic edits (typos, clarifications, additional edge cases in §7) may land directly.

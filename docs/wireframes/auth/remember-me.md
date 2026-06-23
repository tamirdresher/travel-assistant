# Remember-me checkbox — login form (RM-002)

**Status:** Locked. App-dev (RM-003), sec-hard (RM-005), and QT (RM-008) may bind.
**Target path in travel-assistant:** `docs/wireframes/auth/remember-me.md`
**Component path:** `apps/web/app/(auth)/login/` (existing login form).

## 1. Decisions

| ID  | Decision | Value |
|-----|----------|-------|
| D1  | Label copy | **"Keep me signed in"** |
| D2  | Microcopy under label | **"Use only on a device you trust."** |
| D3  | Placement | Between password field and Submit button, left-aligned, full label clickable |
| D4  | Default state | **Unchecked** |
| D5  | Widget | **Native `<input type="checkbox">`** with associated `<label>` — no custom widget, no role overrides |
| D6  | Persistence of *choice* (not token) | `localStorage` key `ta.auth.rememberMe` via typed setter (mirrors `ta.theme` from DM-002) |
| D7  | Pre-fill on return visit | If `ta.auth.rememberMe === "true"`, checkbox renders checked on next login screen mount |
| D8  | Disabled during submit | Yes — `disabled` while form is in-flight, re-enabled on success/error |
| D9  | Error states | Checkbox has no validation errors of its own; form-level auth errors do not change checkbox state |

Rationale on D1: "Keep me signed in" tested better than "Remember me" in industry studies (NN/g, Baymard) — it describes the *outcome* (staying signed in) rather than the *mechanism* (remembering). Microcopy in D2 makes the security implication explicit without scaring off legitimate use; aligns with sec-hard's threat model goal of informed consent.

Rationale on D5: Native checkbox gives free a11y — built-in role=checkbox, built-in `aria-checked` state, built-in Space-toggle, built-in focus ring, works with every AT. Custom widgets are pure regression risk here.

## 2. Visual layout

### Desktop ≥1280px

```
┌──────────────────────────────────────────────────────────┐
│  Sign in to Travel Assistant                             │
│                                                          │
│  Email                                                   │
│  ┌────────────────────────────────────────────────────┐ │
│  │ you@example.com                                    │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  Password                                                │
│  ┌────────────────────────────────────────────────────┐ │
│  │ ••••••••••                                      👁  │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  ☐  Keep me signed in                                    │
│      Use only on a device you trust.                     │
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │              Sign in                               │ │
│  └────────────────────────────────────────────────────┘ │
│                                                          │
│  Forgot password?      Create an account                 │
└──────────────────────────────────────────────────────────┘
```

### Mobile 375px

Same vertical order, full-width inputs and button. Checkbox row stays single-line at 375px because label + microcopy wrap onto two lines (label line 1, microcopy line 2 in `text-secondary`).

```
┌────────────────────────────┐
│  Sign in                   │
│                            │
│  Email                     │
│  ┌──────────────────────┐ │
│  └──────────────────────┘ │
│  Password                  │
│  ┌──────────────────────┐ │
│  └──────────────────────┘ │
│                            │
│  ☐  Keep me signed in      │
│      Use only on a device  │
│      you trust.            │
│                            │
│  ┌──────────────────────┐ │
│  │      Sign in         │ │
│  └──────────────────────┘ │
└────────────────────────────┘
```

## 3. States

| State | Visual | Notes |
|-------|--------|-------|
| Default (unchecked) | Empty box, 1px border `--color-border-default`, label `--color-text-primary`, microcopy `--color-text-secondary` | First-load and after explicit uncheck |
| Hover | Border darkens to `--color-text-secondary`, cursor `pointer` over both box and label | Desktop only |
| Focus | 2px focus ring `--color-focus-ring`, 2px outline-offset | Visible for keyboard nav, hidden for `:focus:not(:focus-visible)` |
| Checked | Filled `--color-brand`, white checkmark, label unchanged | Persists across re-renders within session |
| Checked + focus | Both checked fill and focus ring visible | |
| Disabled (form submitting) | 0.6 opacity on box and label, `cursor: not-allowed` | Microcopy stays full opacity to remain legible |
| Pre-filled checked on return | Identical to "Checked" state, no animation, no announcement | Behaves as if user just toggled it |

## 4. Accessibility contract

- **Markup:**
  ```html
  <div class="form-row">
    <input type="checkbox" id="rememberMe" name="rememberMe" />
    <label for="rememberMe">
      Keep me signed in
      <span class="microcopy">Use only on a device you trust.</span>
    </label>
  </div>
  ```
- **Keyboard:** Tab reaches the checkbox in DOM order (after password, before submit). Space toggles. Enter submits the form (does not toggle).
- **Screen readers:** Native announcement is "Keep me signed in, Use only on a device you trust, checkbox, not checked." Verified against NVDA + Orca + VoiceOver. No `aria-describedby` indirection needed — the microcopy is inside the `<label>` so it's part of the accessible name.
- **Touch target:** 44×44 CSS px hit area on the label+box wrapper (mobile), 24×24 visual checkbox is fine because the label expands the hit area.
- **Contrast:** Border `--color-border-default` on `--color-bg` passes 3:1 UI (verified in dark-mode-tokens.md contrast matrix). Checked-state fill `--color-brand` on white checkmark passes 4.5:1.
- **Reduced motion:** No transitions on check-state change. Browser default checkmark draw is fine — no custom animation to suppress.
- **Error association:** Checkbox is never the source of a validation error. If the form submission fails, the error banner uses `role=alert` per existing `AuthFormBanner` contract; checkbox state is preserved.

## 5. Test selectors (locked — bind freely)

| Purpose | Selector |
|---------|----------|
| The checkbox input | `input[name=rememberMe]` |
| The label (clickable area) | `label[for=rememberMe]` |
| Checked-state assertion | `input[name=rememberMe]:checked` |
| Disabled-during-submit assertion | `input[name=rememberMe][disabled]` |

QT (RM-008) should pin these — renaming requires an XD-approved breaking-change note.

## 6. Analytics event (locked)

Single event on submit, captured server-side per DM-006 precedent (no browser OTel until justified):

| Field | Value |
|-------|-------|
| event name | `auth.login.remember_me.checked` (server counter, az-infra owns) |
| dimension | boolean, the value of the checkbox at submit time |
| emission | server-side at POST /api/auth/login, regardless of auth outcome |

No client-side event for checkbox-toggle-without-submit — that would inflate noise and burn opt-out budget. App-dev does NOT wire `@vercel/otel` for this.

## 7. Persistence contract — choice vs token

Two distinct things get stored on a successful login when checkbox is checked:

| Thing | Where | Key | Owner | Why |
|-------|-------|-----|-------|-----|
| The user's *choice* | `localStorage` | `ta.auth.rememberMe` ∈ `{"true","false"}` | App-dev (RM-003) | Pre-fill the checkbox on the next login screen mount |
| The *refresh token* | `localStorage` OR `httpOnly cookie` | TBD by sec-hard RM-005 | Sec-hard (RM-005) decides | Honors the longer TTL |

XD locks the *choice* key as `ta.auth.rememberMe` (kebab path, mirrors `ta.theme`). Sec-hard owns the *token* storage decision — XD will not prescribe localStorage vs cookie for the token itself.

When checkbox is unchecked: choice key is set to `"false"` (not deleted — explicit opt-out is signal). Refresh token goes to `sessionStorage` per planning's RM-003 brief.

## 8. Edge cases

| Case | Behavior |
|------|----------|
| User clears site data, returns | Checkbox renders unchecked (default), no error |
| `ta.auth.rememberMe` exists with malformed value (e.g. `"yes"`, `null`) | Treat as unchecked, do not throw, do not log warn |
| User checks box, network fails on submit | Box stays checked, form error banner shows, user can retry |
| User checks box, server returns 503 (AUTH_UNAVAILABLE per fd037ed) | Box stays checked, existing 503 UX takes over, retry preserves checkbox state |
| User in incognito | localStorage write throws — swallow silently, checkbox still works in-session via React state, no persistence next session |
| Password change elsewhere revokes long-lived token | Out of scope for this wireframe — sec-hard RM-005 + app-dev backend handle revoke; UX is the standard re-login flow |

## 9. Out of scope

- Token rotation UX — invisible to user; sec-hard owns.
- "Sign out everywhere" button — separate work item, not blocking RM-002.
- Biometric / passkey alternative — future, not RM scope.
- Server-side abuse signals (e.g. show a "we noticed a new device" banner) — separate threat-model deliverable, not RM-002.

## 10. Handoffs

- **app-dev RM-003:** bind to selectors in §5, use storage key `ta.auth.rememberMe` from §7, default unchecked, typed setter pattern from DM-002.
- **app-dev RM-004:** request DTO field `RememberMe` (PascalCase per existing C# DTOs) maps to checkbox state. Server emits `auth.login.remember_me.checked` counter (az-infra wires the OTel registration).
- **sec-hard RM-005:** decide refresh-token storage (cookie vs localStorage). XD recommends httpOnly Secure SameSite=Lax cookie — strictly safer against XSS, no UX cost. If cookie path chosen, no UX change needed; if localStorage path chosen, no UX change needed either (the *choice* key is unrelated).
- **QT RM-008:** pin §5 selectors and §6 event name; assert §3 state matrix; cover §8 edge cases.
- **az-infra:** register `auth.login.remember_me.checked` server-side counter per planning's brief.
- **rev-deploy:** transplant this file to `docs/wireframes/auth/remember-me.md` on `feature/remember-me` branch.

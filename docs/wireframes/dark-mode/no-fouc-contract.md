# No-FOUC contract for dark mode

Owner: experience-design-squad · Consumers: application-development-squad
(DM-003), security-hardening-squad (DM-005) · Issue: DM-001

## Goal

When a returning user has chosen dark mode (or chosen `system` and the OS
prefers dark), the app MUST NOT paint any frame with light backgrounds
before the dark theme is applied. No "flash of unstyled content"; no
"flash of incorrect theme."

## Requirement

A synchronous inline `<script>` is placed in `<head>` **before any
stylesheet link or `<style>` tag**. It sets `document.documentElement
.dataset.theme` to the resolved theme before the browser computes layout.

The script:

1. Reads `localStorage.getItem('ta.theme')`, value ∈ `light | dark | system`.
2. If absent or invalid → treat as `system`.
3. If `system` → resolve via
   `window.matchMedia('(prefers-color-scheme: dark)').matches`.
4. Sets `document.documentElement.setAttribute('data-theme', resolved)`
   where `resolved ∈ {light, dark}`.
5. Wraps the storage read in `try/catch` (Safari private mode throws).

## Reference implementation

This is the **verbatim** string DM-003 ships and DM-005 hashes for CSP.
Any change here requires a re-hash. Whitespace and semicolons are load-
bearing for the hash.

```html
<script>(function(){try{var s=localStorage.getItem('ta.theme');if(s!=='light'&&s!=='dark'&&s!=='system')s='system';var r=s==='system'?(window.matchMedia&&window.matchMedia('(prefers-color-scheme: dark)').matches?'dark':'light'):s;document.documentElement.setAttribute('data-theme',r);}catch(e){document.documentElement.setAttribute('data-theme','light');}})();</script>
```

Size: 387 bytes (well under 500-byte budget). No external network, no
async, no dependencies.

## CSP

Recommended directive (coordinate with DM-005):

```
script-src 'self' 'sha256-<HASH-OF-INLINE-SCRIPT>';
```

DM-005 owns computing the hash from the exact bytes above. If CSP does
not yet exist in the repo, DM-003 still ships the inline script; CSP can
land in a follow-up.

## What this does NOT do

- Does not persist a resolved value — only the user choice.
- Does not register the `matchMedia` listener — that is DM-002's job
  inside the React ThemeProvider (with proper cleanup on unmount).
- Does not emit telemetry — that is DM-002/DM-006.

## Test contract (for DM-004)

- With `localStorage["ta.theme"] = "dark"`: first painted frame has
  `document.documentElement.getAttribute('data-theme') === 'dark'` and
  the background pixel at (10,10) matches `--color-bg` dark
  (`#0D1117`), not light (`#FFFFFF`).
- With `localStorage` disabled / throwing: `data-theme === 'light'`,
  no thrown error in console.
- With `localStorage["ta.theme"] = "garbage"`: falls back to `system`
  resolution (light or dark depending on emulated `prefers-color-scheme`).
- The inline script appears in DOM **before** any `<link rel="stylesheet">`
  or `<style>` element in `<head>`.

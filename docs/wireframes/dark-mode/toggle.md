# Dark-mode toggle wireframe

Owner: experience-design-squad · Status: locked v1 · Issue: DM-001

Three-state segmented control: **Light / Dark / System**. Lives in the app
header, right side, left of the user avatar / account menu.

## Component

- Semantics: `role="radiogroup"` with three `role="radio"` children.
- Accessible name: `aria-label="Theme"` on the group.
- Each radio has `aria-checked="true|false"` and a visible label.
- Keyboard:
  - `Tab` enters/exits the group (single tab stop = the checked radio).
  - `←` / `→` and `↑` / `↓` move selection AND focus to prev/next radio,
    wrapping at ends. Selection follows focus (matches WAI-ARIA APG
    radio pattern; `Space`/`Enter` are no-ops on already-focused radios).
  - `Home` / `End` jump to first/last.
- Screen reader announcement on change is the radio's native announcement
  ("Dark, radio button, 2 of 3, selected"). No extra `aria-live`.
- Hit target ≥24×24 CSS px (WCAG 2.5.8); recommended 32×32 on desktop,
  44×44 on mobile.

## States

ASCII layout (desktop 1280px, header):

```
+--------------------------------------------------------------------------+
|  =  Travel Assistant                       [* Light | ) Dark | # Sys] AV |
+--------------------------------------------------------------------------+
```

Per-radio renderings (segmented control, 3 cells):

```
default     +---------++---------++---------+
            | * Light || ) Dark  || # System|   <- unchecked: bg=transparent
            +---------++---------++---------+      text=text-secondary

checked     +---------++#########++---------+
"Dark"      | * Light |# ) Dark  #| # System|   <- bg=bg-elevated,
            +---------++#########++---------+      text=text-primary,
                                                  border=border-default

hover       +---------++~~~~~~~~~++---------+   <- bg=bg-elevated 50% alpha
(unchecked) | * Light || ) Dark  || # System|      cursor=pointer
            +---------++~~~~~~~~~++---------+

focus       +---------++=========++---------+   <- 2px outline using
visible     | * Light || ) Dark  || # System|      --color-focus-ring,
            +---------++=========++---------+      2px offset from edge

pressed     +---------++█████████++---------+   <- bg=border-subtle
(:active)   | * Light |█ ) Dark  █| # System|      (brief, ~80ms)
            +---------++█████████++---------+
```

Icons are decorative (`aria-hidden="true"`); the text label carries the
accessible name. Icons MAY be hidden below 360px viewport — labels remain.

## Mobile (375px)

The header collapses; toggle moves to the top of the slide-out menu, NOT
hidden behind a "Settings" sub-page.

```
+-------------------------+
|  <-  Menu               |
+-------------------------+
|  Theme                  |
|  +------++------++-----+|
|  | * Lt || ) Dk || # Sy||  <- 44px min height,
|  +------++------++-----+|     full row width, equal thirds
+-------------------------+
|  Profile                |
|  Settings               |
|  Sign out               |
+-------------------------+
```

## Reduced motion

The 80ms bg fade respects `prefers-reduced-motion: reduce` — when set,
the state change is instant.

## Copy

| Element | Copy             | Notes                                  |
| ------- | ---------------- | -------------------------------------- |
| Group   | "Theme"          | `aria-label`; not rendered visually    |
| Radio 1 | "Light"          | visible label                          |
| Radio 2 | "Dark"           | visible label                          |
| Radio 3 | "System"         | visible label; not "Auto"              |

Localization: handled per-app i18n layer; keys
`theme.label`, `theme.light`, `theme.dark`, `theme.system`.

## Event contract (for DM-002 / DM-006)

On user-initiated selection, emit `theme.changed`:

```ts
{
  from: 'light' | 'dark' | 'system',   // previous user choice
  to:   'light' | 'dark' | 'system',   // new user choice
  source: 'user',                      // 'system' only when OS pref flips
  resolvedTheme: 'light' | 'dark'      // post-resolution
}
```

When `state === 'system'` and OS preference flips, emit the same event with
`source: 'system'`, `from === to` (user choice unchanged), and the new
`resolvedTheme`.

## Test selectors (for DM-004)

- Group: `[role="radiogroup"][aria-label="Theme"]`
- Radio: `[role="radio"][data-theme-value="light|dark|system"]`
- Checked state: `aria-checked="true"`

Do not rely on icon glyphs or visible label text in test selectors —
copy will translate.

## No-FOUC contract (DM-003 reference)

The toggle UI must not paint with a wrong theme. The DM-003 inline script
sets `<html data-theme>` before any stylesheet. Toggle markup itself
contains no theme-conditional inline styles — it reads tokens via the CSS
custom properties from `../../design/dark-mode-tokens.md`.

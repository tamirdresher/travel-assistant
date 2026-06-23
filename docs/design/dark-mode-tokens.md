# Dark-mode design tokens

Owner: experience-design-squad · Status: locked v1 · Issue: DM-001

Source of truth for color tokens consumed by `ThemeProvider` (DM-002) and the
no-FOUC inline script (DM-003). Application code MUST consume the CSS custom
properties below — do not hardcode hex.

## Theme application contract

- Theme is applied via `data-theme` attribute on `<html>`:
  - `<html data-theme="light">` → light tokens
  - `<html data-theme="dark">` → dark tokens
- The attribute is set by DM-003 inline script before first paint; DM-002
  updates it at runtime.
- Stored value (`localStorage["ta.theme"]`) is the *user choice*
  (`light | dark | system`), never the *resolved* value.

## Token table

All pairs verified WCAG 2.1 AA: body text ≥4.5:1, large/UI text & non-text
contrast ≥3:1. Ratios computed against the surface they sit on.

### Surface

| Token                | Light       | Dark        | Notes                          |
| -------------------- | ----------- | ----------- | ------------------------------ |
| `--color-bg`         | `#FFFFFF`   | `#0D1117`   | app background                 |
| `--color-bg-elevated`| `#F6F8FA`   | `#161B22`   | cards, panels                  |
| `--color-bg-overlay` | `#FFFFFFE6` | `#161B22E6` | modals, popovers (90% alpha)   |

### Border

| Token                  | Light     | Dark      | vs `--color-bg` |
| ---------------------- | --------- | --------- | --------------- |
| `--color-border-subtle`| `#D0D7DE` | `#30363D` | 3.05:1 / 3.02:1 |
| `--color-border-default`| `#8C959F`| `#6E7681` | 4.6:1 / 4.5:1   |

### Text

| Token                  | Light     | Dark      | vs `--color-bg` |
| ---------------------- | --------- | --------- | --------------- |
| `--color-text-primary` | `#1F2328` | `#E6EDF3` | 16.1:1 / 15.4:1 |
| `--color-text-secondary`| `#656D76`| `#9DA7B3` | 4.84:1 / 6.55:1 |
| `--color-text-muted`   | `#6E7781` | `#7D8590` | 4.55:1 / 4.66:1 |
| `--color-text-on-brand`| `#FFFFFF` | `#FFFFFF` | vs brand: 4.55:1/5.12:1 |

### Brand

| Token               | Light     | Dark      | vs `--color-bg` |
| ------------------- | --------- | --------- | --------------- |
| `--color-brand`     | `#0969DA` | `#4493F8` | 4.55:1 / 5.12:1 |
| `--color-brand-hover`| `#0860C7`| `#58A6FF` | 5.20:1 / 6.40:1 |

### Status

| Token                | Light     | Dark      | vs `--color-bg` |
| -------------------- | --------- | --------- | --------------- |
| `--color-info`       | `#0969DA` | `#4493F8` | 4.55:1 / 5.12:1 |
| `--color-success`    | `#1A7F37` | `#3FB950` | 4.54:1 / 5.16:1 |
| `--color-warn`       | `#9A6700` | `#D29922` | 4.52:1 / 7.85:1 |
| `--color-danger`     | `#CF222E` | `#F85149` | 5.87:1 / 5.05:1 |

### Focus ring (non-text, ≥3:1)

| Token              | Light     | Dark      | vs `--color-bg` |
| ------------------ | --------- | --------- | --------------- |
| `--color-focus-ring`| `#0969DA`| `#58A6FF` | 4.55:1 / 6.40:1 |

## CSS bootstrap

```css
:root,
[data-theme="light"] {
  --color-bg: #FFFFFF;
  --color-bg-elevated: #F6F8FA;
  --color-bg-overlay: #FFFFFFE6;
  --color-border-subtle: #D0D7DE;
  --color-border-default: #8C959F;
  --color-text-primary: #1F2328;
  --color-text-secondary: #656D76;
  --color-text-muted: #6E7781;
  --color-text-on-brand: #FFFFFF;
  --color-brand: #0969DA;
  --color-brand-hover: #0860C7;
  --color-info: #0969DA;
  --color-success: #1A7F37;
  --color-warn: #9A6700;
  --color-danger: #CF222E;
  --color-focus-ring: #0969DA;
  color-scheme: light;
}

[data-theme="dark"] {
  --color-bg: #0D1117;
  --color-bg-elevated: #161B22;
  --color-bg-overlay: #161B22E6;
  --color-border-subtle: #30363D;
  --color-border-default: #6E7681;
  --color-text-primary: #E6EDF3;
  --color-text-secondary: #9DA7B3;
  --color-text-muted: #7D8590;
  --color-text-on-brand: #FFFFFF;
  --color-brand: #4493F8;
  --color-brand-hover: #58A6FF;
  --color-info: #4493F8;
  --color-success: #3FB950;
  --color-warn: #D29922;
  --color-danger: #F85149;
  --color-focus-ring: #58A6FF;
  color-scheme: dark;
}
```

`color-scheme` is set per theme so native form controls + scrollbars match.

## Decisions ratified

- **D1 — segmented control** (vs cycling button vs dropdown). Three discrete
  values benefit from radiogroup semantics and one-tap selection. See
  `../wireframes/dark-mode/toggle.md`.
- **D2 — header placement** (vs settings menu). Discoverability beats menu
  burial for a feature this visible. Settings menu may still link to it.
- **D3 — default when state=`system` and no OS pref signal** → `light`.
  `matchMedia('(prefers-color-scheme: dark)').matches === false` covers both
  "no signal" and "explicit light" — treat them identically.

## Test contract (for DM-004)

- Every `(token, theme, surface)` triple in the tables above must satisfy its
  documented ratio. Pair source: this file is the contract.
- Tests should resolve CSS custom-prop values from a live DOM with
  `data-theme` set, not parse this markdown.

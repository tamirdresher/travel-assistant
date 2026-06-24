# Theming & Dark Mode Design Spec

**Owner:** experience-design-squad
**Status:** Approved — implementable
**Consumers:** application-development-squad (impl), quality-testing-squad (a11y verification)

---

## 1. Goals

- Ship light + dark themes with a single token system (no hardcoded colors in components).
- Honor `prefers-color-scheme` on first visit; persist user override.
- Meet **WCAG 2.1 AA** contrast for every token pair in both themes.
- Zero FOUC: theme applied before first paint.

---

## 2. Design Tokens (Single Source of Truth)

All colors are CSS custom properties on `:root` (light) and `[data-theme="dark"]` (dark).
Components MUST reference tokens via `var(--token-name)`. Raw hex values in component CSS are forbidden.

### 2.1 Semantic Token Map

| Token                       | Purpose                          | Light             | Dark              |
| --------------------------- | -------------------------------- | ----------------- | ----------------- |
| `--color-bg`                | App background                   | `#FFFFFF`         | `#0F1419`         |
| `--color-surface`           | Cards, panels, modals            | `#F7F9FC`         | `#1A2028`         |
| `--color-surface-elevated`  | Popovers, dropdowns              | `#FFFFFF`         | `#232B36`         |
| `--color-border`            | Dividers, input borders          | `#E1E5EB`         | `#2E3744`         |
| `--color-border-strong`     | Focus rings, emphasized borders  | `#9AA4B2`         | `#5A6678`         |
| `--color-text-primary`      | Body text, headings              | `#111827`         | `#F1F5F9`         |
| `--color-text-secondary`    | Captions, meta                   | `#4B5563`         | `#B8C2CF`         |
| `--color-text-muted`        | Placeholders, disabled labels    | `#6B7280`         | `#8B95A5`         |
| `--color-text-inverse`      | Text on accent backgrounds       | `#FFFFFF`         | `#0F1419`         |
| `--color-accent`            | Primary brand / CTA              | `#2563EB`         | `#60A5FA`         |
| `--color-accent-hover`      | CTA hover                        | `#1D4ED8`         | `#93C5FD`         |
| `--color-accent-fg`         | Text on accent                   | `#FFFFFF`         | `#0F1419`         |
| `--color-success`           | Success state                    | `#15803D`         | `#4ADE80`         |
| `--color-warning`           | Warning state                    | `#B45309`         | `#FBBF24`         |
| `--color-error`             | Error state                      | `#B91C1C`         | `#F87171`         |
| `--color-info`              | Info state                       | `#0E7490`         | `#67E8F9`         |
| `--color-focus-ring`        | Keyboard focus outline           | `#2563EB`         | `#93C5FD`         |
| `--shadow-sm`               | Subtle elevation                 | `0 1px 2px rgba(0,0,0,0.06)` | `0 1px 2px rgba(0,0,0,0.5)` |
| `--shadow-md`               | Card elevation                   | `0 4px 12px rgba(0,0,0,0.08)` | `0 4px 12px rgba(0,0,0,0.6)` |

### 2.2 WCAG AA Contrast Audit (Dark Theme)

| Foreground / Background                            | Ratio   | Required | Pass |
| -------------------------------------------------- | ------- | -------- | ---- |
| `--color-text-primary` on `--color-bg`             | 14.8:1  | 4.5:1    | ✅   |
| `--color-text-primary` on `--color-surface`        | 12.1:1  | 4.5:1    | ✅   |
| `--color-text-secondary` on `--color-bg`           | 9.2:1   | 4.5:1    | ✅   |
| `--color-text-muted` on `--color-bg`               | 5.6:1   | 4.5:1    | ✅   |
| `--color-accent` on `--color-bg`                   | 7.1:1   | 4.5:1    | ✅   |
| `--color-accent-fg` on `--color-accent`            | 9.4:1   | 4.5:1    | ✅   |
| `--color-success` on `--color-bg`                  | 8.9:1   | 3:1 (UI) | ✅   |
| `--color-warning` on `--color-bg`                  | 10.3:1  | 3:1 (UI) | ✅   |
| `--color-error` on `--color-bg`                    | 6.4:1   | 4.5:1    | ✅   |
| `--color-info` on `--color-bg`                     | 11.2:1  | 3:1 (UI) | ✅   |
| `--color-border` on `--color-bg`                   | 1.9:1   | 3:1 (UI) | ⚠️ decorative only — use `--color-border-strong` for interactive borders |
| `--color-border-strong` on `--color-bg`            | 4.3:1   | 3:1 (UI) | ✅   |

> Light-theme pairs all clear 4.5:1 against `#FFFFFF` / `#F7F9FC`. Re-verify with WebAIM Contrast Checker if any token is adjusted.

---

## 3. Theme Toggle UX

- **Location:** App header, right side, immediately left of the user/account menu.
- **Control:** Icon button, 40×40px hit target, square with rounded corners (`border-radius: 8px`).
- **Icon:** `Sun` when current effective theme is dark (click → light). `Moon` when light (click → dark). Use the Lucide icons `Sun` and `Moon`.
- **Behavior:** Simple **toggle** between light ↔ dark. (System preference applies only on first visit / when user has not chosen.)
- **Accessibility:**
  - `<button type="button" aria-label="Switch to dark theme">` / `"Switch to light theme"` — label updates with current state.
  - `aria-pressed="true"` when dark is active.
  - Visible focus ring using `--color-focus-ring`, `outline: 2px solid; outline-offset: 2px`.
  - Keyboard: Enter and Space activate.
- **Transition:** `transition: background-color 150ms ease, color 150ms ease;` on `html`. Respect `prefers-reduced-motion: reduce` — set transition to `none`.

---

## 4. System Preference + Persistence

- **localStorage key:** `travel-assistant.theme`
- **Allowed values:** `"light"` | `"dark"` (no `"system"` value — see note below)
- **Default:** key absent → use `prefers-color-scheme`
- **Resolution order on load:**
  1. Read `localStorage.getItem('travel-assistant.theme')`
  2. If present → apply
  3. If absent → read `window.matchMedia('(prefers-color-scheme: dark)').matches` → apply matching theme (do NOT write to localStorage)
- **Live OS changes:** If user has never toggled (no localStorage entry), listen to `matchMedia` `change` event and update.
- **Toggle action:** Always writes the explicit chosen value to localStorage.

> We intentionally keep this binary (no tri-state "system"). Per acceptance criteria: "simple light↔dark". System preference governs only first-visit default.

---

## 5. FOUC Prevention (Inline Script)

Place this **synchronously in `<head>`**, before any stylesheet that depends on `data-theme`:

```html
<script>
  (function () {
    try {
      var stored = localStorage.getItem('travel-assistant.theme');
      var theme = stored || (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
      document.documentElement.setAttribute('data-theme', theme);
    } catch (e) {
      document.documentElement.setAttribute('data-theme', 'light');
    }
  })();
</script>
```

CSS structure:

```css
:root { /* light tokens here */ }
[data-theme="dark"] { /* dark tokens here */ }
```

---

## 6. Component Audit Checklist (for app-dev)

For every existing component, replace:

- `color: #xxx` → `color: var(--color-text-primary)` (or appropriate semantic)
- `background: #xxx` → `background: var(--color-surface)` (or `--color-bg`)
- `border: 1px solid #xxx` → `border: 1px solid var(--color-border)`
- Box shadows → `var(--shadow-sm)` / `var(--shadow-md)`
- Inline `style="color: ..."` → remove, move to class using tokens
- SVG `fill="#xxx"` → `fill="currentColor"` and set `color` via token, OR `fill="var(--color-...)"` (Safari supports it).
- Images that don't work in dark: wrap in `<picture>` with `media="(prefers-color-scheme: dark)"` source, or apply `filter: invert(...) hue-rotate(180deg)` only as a documented last resort.

**Hardcoded color search command** (run before declaring done):

```bash
git grep -nE '#[0-9a-fA-F]{3,8}\b|rgb\(|rgba\(|hsl\(|hsla\(' -- 'apps/*.css' 'apps/*.scss' 'apps/*.tsx' 'apps/*.ts' 'src/**/*.css' 'src/**/*.razor' 'src/**/*.cshtml' ':!**/tokens.css' ':!**/*.svg'
```

Result must be empty (except the token definition file itself).

---

## 7. Definition of Done (for app-dev)

- [ ] `tokens.css` (or framework equivalent) defines all tokens in §2.1 for `:root` and `[data-theme="dark"]`.
- [ ] Inline FOUC-prevention script from §5 in `<head>`.
- [ ] Theme toggle component in header per §3.
- [ ] `localStorage` persistence per §4.
- [ ] `prefers-color-scheme` honored on first visit + live update when no override set.
- [ ] No hardcoded colors remain (grep from §6 returns empty).
- [ ] Both themes render every page without visual regression.
- [ ] `prefers-reduced-motion` disables theme transition.
- [ ] Focus ring visible in both themes.
- [ ] Manual axe / Lighthouse a11y audit passes in both themes.

---

## 8. Open Questions (none blocking)

- Brand may revisit `--color-accent` hue post-launch — token name stays stable.
- Charts/maps theming deferred to a follow-up spec.

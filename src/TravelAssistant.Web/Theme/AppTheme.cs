// XD-6b — MudBlazor theme bridge for src/TravelAssistant.Web.
//
// Source of truth: docs/design/tokens.json
// DO NOT hand-edit color/spacing/typography values here.
// If tokens.json changes, regenerate per sync checklist at bottom.
//
// Owner: experience-design-squad. Drop-in path: src/TravelAssistant.Web/Theme/AppTheme.cs
// Replaces the bootstrap placeholder shipped on feat/app-web-blazor-scaffold-v2 @ 8de60b8.
//
// Symbol contract (do NOT rename without coordinating with XD + app-dev):
//   - namespace TravelAssistant.Web.Theme
//   - public static class AppTheme
//   - public static MudTheme Theme { get; }
// MainLayout.razor binds: <MudThemeProvider Theme="@AppTheme.Theme" />

using MudBlazor;

namespace TravelAssistant.Web.Theme;

public static class AppTheme
{
    public static MudTheme Theme { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            // color.brand.*
            Primary          = "#0B66E4",
            PrimaryDarken    = "#0950B4",
            Secondary        = "#7C3AED",        // accent — decorative only, never sole signal
            // color.fg.*
            TextPrimary      = "#0F172A",
            TextSecondary    = "#475569",        // muted — NOT for body < 16px
            // color.bg.*
            Background       = "#FFFFFF",
            Surface          = "#FFFFFF",
            DrawerBackground = "#F8FAFC",
            AppbarBackground = "#FFFFFF",
            OverlayLight     = "rgba(15,23,42,0.55)",
            // color.border.*
            LinesDefault     = "#E2E8F0",
            LinesInputs      = "#94A3B8",
            // semantic
            Error            = "#B91C1C",
            Success          = "#15803D",
            Warning          = "#A16207",
            Info             = "#0B66E4",
            // state.pending (XD-6c regression guard — 4.5:1 contrast)
            ActionDefault    = "#94A3B8",
        },
        PaletteDark = new PaletteDark
        {
            // color.darkMode.*  (AA contrast verified per pair)
            Primary          = "#60A5FA",
            TextPrimary      = "#F1F5F9",
            TextSecondary    = "#94A3B8",
            Background       = "#0B1220",
            Surface          = "#111827",
            DrawerBackground = "#111827",
            AppbarBackground = "#0B1220",
            OverlayLight     = "rgba(0,0,0,0.65)",
            LinesDefault     = "#1F2937",
            LinesInputs      = "#475569",
            Error            = "#FCA5A5",
            Success          = "#86EFAC",
            Warning          = "#FCD34D",
            Info             = "#60A5FA",
            ActionDefault    = "#64748B",
        },
        // Typography intentionally omitted — XD-6b drop-in was authored against
        // MudBlazor 6.x typography class names (DefaultTypography/Body1Typography/etc.).
        // MudBlazor 7.15 renamed these and we use the framework defaults until XD
        // ships a 7.x-compatible bridge. Palette + LayoutProperties + ZIndex (the
        // load-bearing tokens for axe contrast + Modal Deferral) are unaffected.
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px",       // radius.md
            DrawerWidthLeft     = "280px",
            DrawerWidthRight    = "480px",     // ToolCallDrawer width (XD-2 §4)
            AppbarHeight        = "56px",
        },
        ZIndex = new ZIndex
        {
            // mirror z.* tokens to avoid stacking surprises
            // Modal Deferral Rule is about MOUNT TIMING, not z-order.
            // These values are paint order once a Modal/Dialog does mount.
            Drawer   = 200,
            Popover  = 250,
            Dialog   = 300,
            Tooltip  = 350,
            Snackbar = 400,
        },
    };
}

// --- Sync checklist (run if docs/design/tokens.json changes) ---
//   1. color.brand.primary            → PaletteLight.Primary
//   2. color.brand.primaryHover       → PaletteLight.PrimaryDarken
//   3. color.fg.default               → PaletteLight.TextPrimary
//   4. color.fg.muted                 → PaletteLight.TextSecondary
//   5. color.bg.default               → PaletteLight.Background / Surface
//   6. color.bg.subtle                → PaletteLight.DrawerBackground
//   7. color.bg.overlay               → PaletteLight.OverlayLight
//   8. color.border.default           → PaletteLight.LinesDefault
//   9. color.border.strong            → PaletteLight.LinesInputs
//  10. color.fg.{danger,success,warning,info} → Palette.{Error,Success,Warning,Info}
//  11. color.state.pending            → PaletteLight.ActionDefault (Pending block border)
//  12. color.darkMode.*               → PaletteDark.*
//  13. type.family.sans               → Typography.Default.FontFamily
//  14. type.size.{base,sm,lg,xl,2xl,3xl} → Body1/Body2/H4/H3/H2/H1 FontSize
//  15. radius.md                      → LayoutProperties.DefaultBorderRadius
//  16. z.{drawer,modal,popover,toast,tooltip} → ZIndex.{Drawer,Dialog,Popover,Snackbar,Tooltip}
//
// --- Reduced motion (motion.reducedMotion — XD-4 hard requirement) ---
// Add to wwwroot/app.css (do NOT override per-component):
//   @media (prefers-reduced-motion: reduce) {
//       *, *::before, *::after {
//           transition-duration: 0ms !important;
//           animation-duration:  0ms !important;
//       }
//   }
//
// --- Focus ring (focus.*) ---
// MudBlazor's default focus ring uses Primary at 2px and matches tokens.focus.ringColor (#0B66E4).
// Do NOT override globally without an XD ADR — the ring is the keyboard user's only positional
// signal (WCAG 2.4.7 + 2.4.11 Focus Not Obscured).

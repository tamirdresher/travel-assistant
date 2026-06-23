using MudBlazor;

namespace TravelAssistant.Web.Theme;

// XD-6b drop-in placeholder. XD ships _AppTheme.razor on branch
// xd/design-baseline @ 9029908 — it lifts every value from
// docs/design/tokens.json into MudTheme and replaces this class.
// Until then, MudBlazor defaults are wired so the host boots.
public static class AppTheme
{
    public static MudTheme Theme { get; } = new MudTheme();
}

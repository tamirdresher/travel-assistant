using MudBlazor.Services;
using TravelAssistant.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// TODO: re-enable once TravelAssistant.ServiceDefaults lands on main
// builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

var app = builder.Build();

// TODO: re-enable once TravelAssistant.ServiceDefaults lands on main
// app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program { }

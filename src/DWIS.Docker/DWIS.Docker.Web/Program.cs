using DWIS.Docker.Core;
using DWIS.Docker.UI;
using DWIS.Docker.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDwisDockerCore(o => o.DataDirectory = builder.Configuration["Dwis:DataDirectory"]);
builder.Services.AddDwisDockerUi();

builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents(o =>
    {
        o.DetailedErrors = builder.Environment.IsDevelopment();
        o.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);
    })
    .AddHubOptions(o =>
    {
        // The config editor sends whole JSON files; the default 32 KB limit is too small.
        o.MaximumReceiveMessageSize = 1024 * 1024;
        o.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
        o.KeepAliveInterval = TimeSpan.FromSeconds(15);
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

// No HTTPS redirection and no HSTS: inside the container the app is served on plain http (port 8080).
if (builder.Configuration.GetValue<bool>("Dwis:UseHttpsRedirection"))
{
    app.UseHttpsRedirection();
}

app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();

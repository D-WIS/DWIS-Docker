using DWIS.Desktop.Web.Client.Pages;
using DWIS.Desktop.Web.Components;
using DWIS.Docker.Clients;
using Microsoft.AspNetCore.SignalR.Client;
using System.Data.Common;
using MudBlazor.Services;

string HubAddress = "https://dwis.digiwells.no/blackboard/applications";
string DWIS_HUB = "/dwishub";

HubConnectionBuilder connectionBuilder = new HubConnectionBuilder();
var _connection = connectionBuilder
    .WithUrl(HubAddress + DWIS_HUB)
    .WithAutomaticReconnect()
    .AddMessagePackProtocol()
.Build();


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();


builder.Services.AddSingleton<HubConnection>(_connection);
builder.Services.AddSingleton<DWISDockerClientConfiguration>();
builder.Services.AddSingleton<DWISDockerClient>();
builder.Services.AddSingleton<HubGroupDataManager>();
//services.AddBlazorBootstrap();
builder.Services.AddMudServices();



var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(DWIS.Desktop.Web.Client._Imports).Assembly);

app.Run();

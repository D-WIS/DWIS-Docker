using DWIS.Docker.Core;
using DWIS.Docker.UI;
using DWIS.Docker.UI.Services;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DWIS.Docker.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddDwisDockerCore(o => o.DataDirectory = builder.Configuration["Dwis:DataDirectory"]);
        builder.Services.AddDwisDockerUi();
        builder.Services.AddWindowsFormsBlazorWebView();
        builder.Services.AddSingleton<IPlatformServices, WinFormsPlatformServices>();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        using var host = builder.Build();
        host.Start();
        try
        {
            Application.Run(new MainForm(host.Services));
        }
        finally
        {
            host.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
        }
    }
}

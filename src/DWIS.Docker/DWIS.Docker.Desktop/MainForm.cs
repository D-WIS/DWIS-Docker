using DWIS.Docker.UI.Shell;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;

namespace DWIS.Docker.Desktop;

/// <summary>A plain window with a standard title bar that hosts the shared Blazor UI.</summary>
internal sealed class MainForm : Form
{
    public MainForm(IServiceProvider services)
    {
        Text = "D-WIS Docker";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1400, 900);
        MinimumSize = new Size(1000, 650);
        try { Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "dwis-logo.ico")); } catch { /* optional */ }

        var webView = new BlazorWebView
        {
            Dock = DockStyle.Fill,
            HostPage = "wwwroot/index.html",
            Services = services
        };
        webView.RootComponents.Add<DwisShell>("#app");
        Controls.Add(webView);
    }
}

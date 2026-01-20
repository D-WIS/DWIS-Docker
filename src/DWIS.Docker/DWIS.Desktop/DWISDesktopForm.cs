using DWIS.Docker.Clients;
using DWIS.Docker.Components;
using DWIS.Docker.Models;
using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using System.Data.Common;

namespace DWIS.Desktop
{
    public partial class DWISDesktopForm : Form
    {
        private string HubAddress  = "https://dwis.digiwells.no/blackboard/applications";
        public const string DWIS_HUB = "/dwishub";

        public DWISDesktopForm()
        {
            InitializeComponent();

            HubConnectionBuilder connectionBuilder = new HubConnectionBuilder();
            var _connection = connectionBuilder
                .WithUrl(HubAddress +DWIS_HUB)
                .WithAutomaticReconnect()
                .AddMessagePackProtocol()
            .Build();


            //HubGroupDataManager
            var services = new ServiceCollection();
            services.AddSingleton<DWISModulesConfigurationClient>();
            services.AddSingleton<HubConnection>(_connection);
            services.AddSingleton<DWISDockerClientConfiguration>();
            services.AddSingleton<DWISDockerClient>();
            services.AddSingleton<HubGroupDataManager>();
            services.AddSingleton<StandardSetUp>();
            services.AddWindowsFormsBlazorWebView();
            //services.AddBlazorBootstrap();
            services.AddMudServices();
            blazorWebView.HostPage = "wwwroot\\index.html";
            blazorWebView.Services = services.BuildServiceProvider();

            blazorWebView.RootComponents.Add<DWISManager>("#app", new Dictionary<string, object?>
            {
                { nameof(DWISManager.OnQuitButtonClicked), new Microsoft.AspNetCore.Components.EventCallback(null, () => Quit())},
                { nameof(DWISManager.OnMinimizeButtonClicked), new Microsoft.AspNetCore.Components.EventCallback(null, () => Minimize())},
                { nameof(DWISManager.OnMaximizeButtonClicked), new Microsoft.AspNetCore.Components.EventCallback(null, () => Maximize())},
                { nameof(DWISManager.CopyToClipBoard), CopyTextToClipboard},
                { nameof(DWISManager.SelectFolder), GetFolder},
                { nameof(DWISManager.Desktop), true}
            });
        }

        private async void Quit()
        {
            this.Close();
        }

        private void Maximize()
        {
            if (this.WindowState == FormWindowState.Maximized)
            {
                this.WindowState = FormWindowState.Normal;
            }
            else
            {
                this.WindowState = FormWindowState.Maximized;
            }
            blazorWebView.RootComponents.First().Parameters["Maximized"] = (bool)(this.WindowState == FormWindowState.Maximized);
            //blazorWebView.para.Refresh();
        }

        private void Minimize()
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void CopyTextToClipboard(string data)
        {
            Clipboard.SetText(data);
        }

        private string GetFolder()
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                return dialog.SelectedPath;
            }
            else return string.Empty;
        }
    }
}

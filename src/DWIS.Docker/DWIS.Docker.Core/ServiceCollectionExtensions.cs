using DWIS.Docker.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace DWIS.Docker.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers every Core service. Used by the web host, the desktop host and the updater.</summary>
    public static IServiceCollection AddDwisDockerCore(this IServiceCollection services, Action<DwisCoreOptions>? configure = null)
    {
        var options = new DwisCoreOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        services.AddSingleton<HostEnvironment>();
        services.AddSingleton<OperationTracker>();

        if (options.PersistSettings)
            services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        else
            services.AddSingleton<ISettingsStore, InMemorySettingsStore>();

        services.AddSingleton<IDockerService, DockerService>();
        services.AddSingleton<IConfigFileService, ConfigFileService>();

        services.AddHttpClient<IImageRegistryService, DockerHubRegistryService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("dwis-docker");
        });
        services.AddSingleton<RegistryCache>();

        if (options.EnableHubs)
        {
            services.AddSingleton<HubManager>();
            services.AddSingleton<IHubService>(sp => sp.GetRequiredService<HubManager>());
            services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<HubManager>());
        }
        else
        {
            services.AddSingleton<IHubService, NullHubService>();
        }

        services.AddSingleton<ContainerMonitor>();
        services.AddSingleton<IStatusService>(sp => sp.GetRequiredService<ContainerMonitor>());
        if (options.EnableMonitor)
            services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<ContainerMonitor>());

        services.AddSingleton<IModuleManager, ModuleManager>();
        services.AddSingleton<RecordingScanner>();
        services.AddSingleton<IRecordingManager, RecordingService>();
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);

        return services;
    }
}

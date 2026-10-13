using DWIS.Docker.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor;
using MudBlazor.Services;

namespace DWIS.Docker.UI;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers MudBlazor and the per-circuit UI helpers. Hosts add their own <see cref="IPlatformServices"/>.</summary>
    public static IServiceCollection AddDwisDockerUi(this IServiceCollection services)
    {
        services.AddMudServices(o =>
        {
            o.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
            o.SnackbarConfiguration.PreventDuplicates = true;
            o.SnackbarConfiguration.MaxDisplayedSnackbars = 5;
            o.SnackbarConfiguration.VisibleStateDuration = 3000;
            o.SnackbarConfiguration.ShowCloseIcon = true;
            o.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
        });
        services.AddScoped<UiActionRunner>();
        services.TryAddScoped<IPlatformServices, JsPlatformServices>();
        return services;
    }
}

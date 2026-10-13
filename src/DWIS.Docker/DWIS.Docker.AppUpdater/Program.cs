// Recreates every container of the given image with a freshly pulled copy.
// Runs as a throwaway container launched by dwis-docker's "Update application" button.

using DWIS.Docker.Core;
using DWIS.Docker.Core.Catalog;
using DWIS.Docker.Core.Models;
using DWIS.Docker.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var imageRef = args.Length > 0 ? args[0] : ModuleCatalog.AppImageRef;

var services = new ServiceCollection()
    .AddLogging(b => b.AddSimpleConsole(o => o.SingleLine = true).SetMinimumLevel(LogLevel.Information))
    .AddDwisDockerCore(o =>
    {
        o.PersistSettings = false;
        o.EnableHubs = false;
        o.EnableMonitor = false;
    });

await using var provider = services.BuildServiceProvider();
var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("updater");
var docker = provider.GetRequiredService<IDockerService>();

using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(15));
var progress = new Progress<PullProgress>(p =>
{
    if (p.OverallFraction is { } f) Console.WriteLine($"  {p.Status} {f:P0}");
});

try
{
    logger.LogInformation("Updating containers of {Image}", imageRef);
    await docker.RecreateContainersOfImageAsync(imageRef, progress, cts.Token);
    logger.LogInformation("Done");
    return 0;
}
catch (DwisDockerException ex)
{
    logger.LogError("Update failed: {Message}", ex.UserMessage);
    return 1;
}
catch (OperationCanceledException)
{
    logger.LogError("Update timed out");
    return 2;
}

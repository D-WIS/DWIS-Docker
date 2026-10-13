using Microsoft.JSInterop;

namespace DWIS.Docker.UI.Services;

/// <summary>What differs between the web host and the desktop host.</summary>
public interface IPlatformServices
{
    bool IsDesktop { get; }

    /// <summary>Self-update only makes sense when the app itself runs in a container.</summary>
    bool SupportsSelfUpdate { get; }

    Task<bool> CopyToClipboardAsync(string text);
}

/// <summary>Browser implementation using the small dwis.js helper.</summary>
public sealed class JsPlatformServices(IJSRuntime js) : IPlatformServices
{
    public bool IsDesktop => false;
    public bool SupportsSelfUpdate => true;

    public async Task<bool> CopyToClipboardAsync(string text)
    {
        try
        {
            return await js.InvokeAsync<bool>("dwis.copyText", text);
        }
        catch (JSException)
        {
            return false;
        }
    }
}

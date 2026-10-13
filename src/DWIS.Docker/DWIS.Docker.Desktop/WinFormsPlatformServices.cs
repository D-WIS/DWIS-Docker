using DWIS.Docker.UI.Services;

namespace DWIS.Docker.Desktop;

/// <summary>Desktop implementation: Windows clipboard, no self-update (the app is not in a container).</summary>
internal sealed class WinFormsPlatformServices : IPlatformServices
{
    public bool IsDesktop => true;
    public bool SupportsSelfUpdate => false;

    public Task<bool> CopyToClipboardAsync(string text)
    {
        var tcs = new TaskCompletionSource<bool>();
        var thread = new Thread(() =>
        {
            try
            {
                Clipboard.SetText(text);
                tcs.SetResult(true);
            }
            catch
            {
                tcs.SetResult(false);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return tcs.Task;
    }
}

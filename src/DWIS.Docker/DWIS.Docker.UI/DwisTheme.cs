using MudBlazor;

namespace DWIS.Docker.UI;

public static class DwisTheme
{
    public const string Primary = "#052767";
    public const string Secondary = "#3a0647";

    public static MudTheme Theme { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = Primary,
            Secondary = Secondary,
            Background = "#f2f4f8",
            Surface = "#ffffff",
            AppbarBackground = Primary,
            AppbarText = "#ffffff",
            TextPrimary = "#1c2333",
            TextSecondary = "#5a6270",
            Success = "#1e8e5a",
            Warning = "#c77700",
            Error = "#c62828",
            Info = "#2a6fdb",
            LinesDefault = "#e1e5ee",
            TableLines = "#e1e5ee",
            DrawerBackground = "#ffffff"
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "10px"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Roboto", "Helvetica Neue", "Helvetica", "Arial", "sans-serif"]
            }
        }
    };
}

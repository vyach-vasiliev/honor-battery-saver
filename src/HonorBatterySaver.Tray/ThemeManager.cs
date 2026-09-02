using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace HonorBatterySaver.Tray;

internal static class ThemeManager
{
    private const int WmSettingChange = 0x001A;
    private const int WmThemeChanged = 0x031A;
    private const int DwmUseImmersiveDarkMode = 20;
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static bool IsDark { get; private set; }
    public static event EventHandler? ThemeChanged;

    public static void Initialize() => ApplyTheme();

    public static void Attach(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            ApplyTheme();
            var source = (HwndSource)PresentationSource.FromVisual(window);
            HwndSourceHook hook = (nint hwnd, int message, nint wParam, nint lParam, ref bool handled) =>
            {
                if (message is WmSettingChange or WmThemeChanged)
                {
                    ApplyTheme();
                    ApplyTitleBar(window);
                }

                return nint.Zero;
            };
            source.AddHook(hook);
            window.Closed += (_, _) => source.RemoveHook(hook);
            ApplyTitleBar(window);
        };
    }

    private static void ApplyTheme()
    {
        IsDark = ReadDarkThemePreference();
        var palette = IsDark ? DarkPalette : LightPalette;
        if (System.Windows.Application.Current is null)
        {
            return;
        }

        foreach (var (key, value) in palette)
        {
            System.Windows.Application.Current.Resources[key] = new SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value));
        }

        ThemeChanged?.Invoke(null, EventArgs.Empty);
    }

    private static bool ReadDarkThemePreference()
    {
        if (SystemParameters.HighContrast)
        {
            var color = System.Windows.SystemColors.WindowColor;
            return (color.R * 0.299 + color.G * 0.587 + color.B * 0.114) < 128;
        }

        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey, writable: false);
        return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
    }

    private static void ApplyTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero)
        {
            return;
        }

        var enabled = IsDark ? 1 : 0;
        _ = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref enabled, sizeof(int));
    }

    private static readonly IReadOnlyDictionary<string, string> LightPalette =
        new Dictionary<string, string>
        {
            ["AccentBrush"] = "#4B68E8",
            ["AccentHoverBrush"] = "#3E59CE",
            ["TextBrush"] = "#182033",
            ["MutedTextBrush"] = "#667085",
            ["SurfaceBrush"] = "#FFFFFF",
            ["CanvasBrush"] = "#F4F6FA",
            ["BorderBrush"] = "#DEE3EC",
            ["ControlBrush"] = "#EEF2F8",
            ["ControlHoverBrush"] = "#E3E9F3",
            ["TableHeaderBrush"] = "#F7F8FB",
            ["AlternateRowBrush"] = "#F9FAFC",
            ["RowHoverBrush"] = "#F1F4FA",
            ["SelectionBrush"] = "#E8EDFF",
            ["InactiveSelectionBrush"] = "#F0F2F7",
            ["StatusSurfaceBrush"] = "#EAF7EF",
            ["StatusTextBrush"] = "#247548",
            ["WarningSurfaceBrush"] = "#FFF8E8",
            ["WarningBorderBrush"] = "#E8CA77",
            ["BadgeBrush"] = "#E9EEF7",
            ["LogoLeafBrush"] = "#79E6B2"
        };

    private static readonly IReadOnlyDictionary<string, string> DarkPalette =
        new Dictionary<string, string>
        {
            ["AccentBrush"] = "#7F96FF",
            ["AccentHoverBrush"] = "#93A6FF",
            ["TextBrush"] = "#F3F5FA",
            ["MutedTextBrush"] = "#AAB2C2",
            ["SurfaceBrush"] = "#171A22",
            ["CanvasBrush"] = "#0F1117",
            ["BorderBrush"] = "#303541",
            ["ControlBrush"] = "#242936",
            ["ControlHoverBrush"] = "#2D3442",
            ["TableHeaderBrush"] = "#1D212B",
            ["AlternateRowBrush"] = "#1B1F28",
            ["RowHoverBrush"] = "#252B38",
            ["SelectionBrush"] = "#28345D",
            ["InactiveSelectionBrush"] = "#20242D",
            ["StatusSurfaceBrush"] = "#193629",
            ["StatusTextBrush"] = "#7ED9A4",
            ["WarningSurfaceBrush"] = "#3B301A",
            ["WarningBorderBrush"] = "#7E652A",
            ["BadgeBrush"] = "#29303D",
            ["LogoLeafBrush"] = "#83E8BA"
        };

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int valueSize);
}

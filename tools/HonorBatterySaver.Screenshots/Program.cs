using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using HonorBatterySaver.Core;
using HonorBatterySaver.Tray;

internal static class Program
{
    private const int Width = 1000;
    private const int Height = 1000;
    private const double Scale = 1.5;

    [STAThread]
    private static void Main(string[] args)
    {
        var projectRoot = FindProjectRoot();
        var outputDirectory = args.Length == 0
            ? Path.Combine(projectRoot, "site", "assets", "screenshots")
            : Path.GetFullPath(args[0]);
        Directory.CreateDirectory(outputDirectory);

        // A plain Application is deliberate: App queues its production startup even
        // without Run(). This renderer must never start a controller or service.
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources = ReadApplicationResources(projectRoot);
        ApplyDarkPalette(app);

        foreach (var language in new[] { UiLanguage.English, UiLanguage.Russian })
        foreach (var diagnostics in new[] { false, true })
        {
            Strings.ApplyUiLanguage(language);
            var settings = CreateDemoSettings(language);
            var window = new SettingsWindow(
                () => settings.Clone(),
                () => Task.FromResult(CreateDemoDiagnostics(settings)),
                _ => throw new InvalidOperationException("Screenshot rendering must not save settings."),
                () => throw new InvalidOperationException("Screenshot rendering must not query Wi-Fi."));
            window.Prepare(diagnostics);

            var root = (Panel)window.Content;
            root.Background = (Brush)app.Resources["CanvasBrush"];
            root.Measure(new Size(Width, Height));
            root.Arrange(new Rect(0, 0, Width, Height));
            root.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            root.UpdateLayout();

            var locale = language == UiLanguage.Russian ? "ru" : "en";
            if (Strings.CurrentCulture.TwoLetterISOLanguageName != locale)
                throw new InvalidOperationException("Unexpected screenshot locale.");
            if (((DataGrid)window.FindName("RulesGrid")).Items.Count != 3 ||
                ((TextBlock)window.FindName("WifiValue")).Text != Strings.Format("Diagnostics_NetworkName", "Demo Home"))
                throw new InvalidOperationException("Expected demonstration data was not rendered.");

            var image = new RenderTargetBitmap((int)(Width * Scale), (int)(Height * Scale),
                96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
            image.Render(root);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            var screen = diagnostics ? "diagnostics" : "settings";
            var path = Path.Combine(outputDirectory, $"{screen}-dark-{locale}.png");
            using (var stream = File.Create(path)) encoder.Save(stream);
            Console.WriteLine($"Rendered {path} (demo data only)");
            window.AllowClose = true;
            window.Close();
        }
        app.Shutdown();
    }

    private static AppSettings CreateDemoSettings(UiLanguage language) => new()
    {
        Language = language,
        AutomaticMode = true,
        StartWithWindows = true,
        DisclaimerAcceptedVersion = AppSettings.CurrentDisclaimerVersion,
        NetworkRules =
        [
            new() { Ssid = "Demo Home", Mode = BatteryMode.Home },
            new() { Ssid = "Demo Office", Mode = BatteryMode.Office },
            new() { Ssid = "Demo Travel", Mode = BatteryMode.Travel }
        ]
    };

    private static TrayDiagnostics CreateDemoDiagnostics(AppSettings settings)
    {
        var wifi = new WifiSnapshot(["Demo Home"], false, Strings.Get("Wifi_AccessAllowed"));
        var decision = new BatteryModeDecisionEngine().Decide(new(PowerSource.Ac, wifi.Ssids, settings));
        var attempt = new ApplyResult(ApplyOutcome.Success, BatteryMode.Home,
            new DateTimeOffset(2026, 9, 3, 14, 32, 0, TimeSpan.FromHours(3)),
            true, 0, "00", true, Strings.Get("Service_Available"));
        var service = new ServiceStatus(true, true, true,
            new RegistrySnapshot(true, 1, 0, "String", "String"), attempt, Strings.Get("Service_Available"));
        return new(wifi, PowerSource.Ac, decision, BatteryMode.Home, service,
            $"14:32 · {ServiceText.DescribeAttempt(attempt, Strings.CurrentCulture)}");
    }

    private static ResourceDictionary ReadApplicationResources(string root)
    {
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        var source = XDocument.Load(Path.Combine(root, "src", "HonorBatterySaver.Tray", "App.xaml"));
        var dictionary = new XElement(presentation + "ResourceDictionary",
            new XAttribute(XNamespace.Xmlns + "x", "http://schemas.microsoft.com/winfx/2006/xaml"),
            new XAttribute(XNamespace.Xmlns + "local", "clr-namespace:HonorBatterySaver.Tray;assembly=Honor Battery Saver"),
            source.Root!.Element(presentation + "Application.Resources")!.Elements());
        return (ResourceDictionary)XamlReader.Parse(dictionary.ToString());
    }

    private static void ApplyDarkPalette(Application app)
    {
        var theme = typeof(SettingsWindow).Assembly.GetType("HonorBatterySaver.Tray.ThemeManager")!;
        var palette = (IReadOnlyDictionary<string, string>)theme.GetField("DarkPalette",
            BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        foreach (var (key, value) in palette)
            app.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
    }

    private static string FindProjectRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "HonorBatterySaver.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException("Run this tool from a built repository checkout.");
    }
}

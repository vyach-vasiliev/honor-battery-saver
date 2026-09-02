using System.Globalization;
using System.Resources;

namespace HonorBatterySaver.Core;

public static class Strings
{
    private const string ResourceBaseName = "HonorBatterySaver.Core.Resources.Strings";
    private static readonly ResourceManager ResourceManager = new(ResourceBaseName, typeof(Strings).Assembly);
    private static readonly CultureInfo SystemCulture = ResolveCulture(CultureInfo.CurrentUICulture.Name);
    private static readonly AsyncLocal<CultureInfo?> RequestCulture = new();
    private static CultureInfo _currentCulture = SystemCulture;

    public static CultureInfo CurrentCulture => RequestCulture.Value ?? _currentCulture;

    public static event EventHandler? CultureChanged;

    public static string Get(string key) => Get(key, CurrentCulture);

    public static string Get(string key, CultureInfo culture) =>
        ResourceManager.GetString(key, ResolveCulture(culture.Name))
        ?? ResourceManager.GetString(key, CultureInfo.GetCultureInfo("en-US"))
        ?? throw new MissingManifestResourceException($"Missing localized string '{key}'.");

    public static string Format(string key, params object?[] arguments) =>
        string.Format(CurrentCulture, Get(key), arguments);

    public static string Format(CultureInfo culture, string key, params object?[] arguments) =>
        string.Format(ResolveCulture(culture.Name), Get(key, culture), arguments);

    public static CultureInfo ResolveCulture(string? cultureName)
    {
        if (!string.IsNullOrWhiteSpace(cultureName) &&
            cultureName.StartsWith("ru", StringComparison.OrdinalIgnoreCase))
        {
            return CultureInfo.GetCultureInfo("ru");
        }

        return CultureInfo.GetCultureInfo("en-US");
    }

    public static void ApplyUiLanguage(UiLanguage language)
    {
        var culture = language switch
        {
            UiLanguage.Russian => CultureInfo.GetCultureInfo("ru"),
            UiLanguage.English => CultureInfo.GetCultureInfo("en-US"),
            _ => SystemCulture
        };

        var changed = !string.Equals(_currentCulture.Name, culture.Name, StringComparison.OrdinalIgnoreCase);
        _currentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        if (changed)
        {
            CultureChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    public static void ApplySupportedUiCulture() => ApplyUiLanguage(UiLanguage.System);

    public static IDisposable UseCulture(string? cultureName)
    {
        var previous = RequestCulture.Value;
        RequestCulture.Value = ResolveCulture(cultureName);
        return new CultureScope(previous);
    }

    public static string GetModeName(BatteryMode mode) => mode switch
    {
        BatteryMode.Home => Get("Mode_Home"),
        BatteryMode.Office => Get("Mode_Office"),
        BatteryMode.Travel => Get("Mode_Travel"),
        _ => BatteryProfiles.Get(mode).DisplayName
    };

    public static string GetModeName(BatteryMode mode, CultureInfo culture) => mode switch
    {
        BatteryMode.Home => Get("Mode_Home", culture),
        BatteryMode.Office => Get("Mode_Office", culture),
        BatteryMode.Travel => Get("Mode_Travel", culture),
        _ => BatteryProfiles.Get(mode).DisplayName
    };

    private sealed class CultureScope(CultureInfo? previous) : IDisposable
    {
        public void Dispose() => RequestCulture.Value = previous;
    }
}

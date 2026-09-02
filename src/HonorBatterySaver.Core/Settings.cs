using System.Text.Json;
using System.Text.Json.Serialization;

namespace HonorBatterySaver.Core;

public enum UiLanguage
{
    System,
    Russian,
    English
}

public sealed class NetworkRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Ssid { get; set; } = string.Empty;
    public BatteryMode Mode { get; set; } = BatteryMode.Home;
    public bool IsEnabled { get; set; } = true;
}

public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 4;
    public const int CurrentDisclaimerVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public bool AutomaticMode { get; set; } = true;
    public BatteryMode DefaultMode { get; set; } = BatteryMode.Travel;
    public BatteryMode ManualMode { get; set; } = BatteryMode.Travel;
    public bool StartWithWindows { get; set; } = true;
    public UiLanguage Language { get; set; } = UiLanguage.System;
    public int DisclaimerAcceptedVersion { get; set; }
    public List<NetworkRule> NetworkRules { get; set; } = [];

    public static AppSettings CreateDefault() => new();

    public AppSettings Clone() => JsonSerializer.Deserialize<AppSettings>(
        JsonSerializer.Serialize(this, JsonDefaults.Options), JsonDefaults.Options) ?? CreateDefault();
}

public static class SettingsBehavior
{
    public static void SelectManualMode(AppSettings settings, BatteryMode mode)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _ = BatteryProfiles.Get(mode);
        settings.AutomaticMode = false;
        settings.ManualMode = mode;
    }

    public static AppSettings Normalize(AppSettings? settings)
    {
        settings ??= AppSettings.CreateDefault();
        settings.SchemaVersion = AppSettings.CurrentSchemaVersion;

        if (!BatteryProfiles.IsSupported(settings.DefaultMode))
        {
            settings.DefaultMode = BatteryMode.Travel;
        }

        if (!BatteryProfiles.IsSupported(settings.ManualMode))
        {
            settings.ManualMode = BatteryMode.Travel;
        }

        if (!Enum.IsDefined(settings.Language))
        {
            settings.Language = UiLanguage.System;
        }

        if (settings.DisclaimerAcceptedVersion < 0)
        {
            settings.DisclaimerAcceptedVersion = 0;
        }

        settings.NetworkRules ??= [];
        settings.NetworkRules = settings.NetworkRules
            .Where(rule => rule is not null && !string.IsNullOrEmpty(rule.Ssid) && BatteryProfiles.IsSupported(rule.Mode))
            .Select(rule => new NetworkRule
            {
                Id = rule.Id == Guid.Empty ? Guid.NewGuid() : rule.Id,
                Ssid = rule.Ssid,
                Mode = rule.Mode,
                IsEnabled = rule.IsEnabled
            })
            .ToList();

        return settings;
    }
}

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
}

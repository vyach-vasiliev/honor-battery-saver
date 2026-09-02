namespace HonorBatterySaver.Core;

public enum BatteryMode
{
    Home = 0,
    Office = 1,
    Travel = 2
}

public sealed record BatteryProfile(
    BatteryMode Mode,
    string DisplayName,
    byte ResumeChargePercent,
    byte StopChargePercent,
    byte[] OemPayload,
    int RegistryValue);

public static class BatteryProfiles
{
    public const int EnabledRegistryStatus = 1;

    private static readonly IReadOnlyDictionary<BatteryMode, BatteryProfile> Profiles =
        new Dictionary<BatteryMode, BatteryProfile>
        {
            [BatteryMode.Home] = new(BatteryMode.Home, string.Empty, 40, 70, [0x03, 0x10, 0x28, 0x46], 0),
            [BatteryMode.Office] = new(BatteryMode.Office, string.Empty, 70, 90, [0x03, 0x10, 0x46, 0x5A], 1),
            [BatteryMode.Travel] = new(BatteryMode.Travel, string.Empty, 95, 100, [0x03, 0x10, 0x5F, 0x64], 2)
        };

    public static BatteryProfile Get(BatteryMode mode)
    {
        if (!Profiles.TryGetValue(mode, out var profile))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported battery mode.");
        }

        var displayName = mode switch
        {
            BatteryMode.Home => Strings.Get("Mode_Home"),
            BatteryMode.Office => Strings.Get("Mode_Office"),
            BatteryMode.Travel => Strings.Get("Mode_Travel"),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported battery mode.")
        };
        return profile with { DisplayName = displayName, OemPayload = [.. profile.OemPayload] };
    }

    public static bool IsSupported(BatteryMode mode) => Profiles.ContainsKey(mode);
}

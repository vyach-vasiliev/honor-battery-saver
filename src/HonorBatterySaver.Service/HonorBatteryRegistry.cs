using HonorBatterySaver.Core;
using Microsoft.Win32;
using System.Globalization;

namespace HonorBatterySaver.Service;

public sealed class HonorBatteryRegistry : IBatteryRegistry
{
    internal const string RegistryPath = @"SOFTWARE\PCManager\MBAPowerManager";
    private const string StatusName = "PowerSafeManagerStatus";
    private const string ModeName = "PowerSafeManagerMode";

    public Task<RegistrySnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = baseKey.OpenSubKey(RegistryPath, writable: false);
        if (key is null)
        {
            return Task.FromResult(new RegistrySnapshot(false, null, null));
        }

        return Task.FromResult(new RegistrySnapshot(
            true,
            ReadNumericValue(key, StatusName),
            ReadNumericValue(key, ModeName),
            ReadValueKind(key, StatusName),
            ReadValueKind(key, ModeName)));
    }

    public Task WriteAsync(BatteryMode mode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var profile = BatteryProfiles.Get(mode);
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = baseKey.OpenSubKey(RegistryPath, writable: true)
            ?? throw new InvalidOperationException("OEM registry key is missing.");
        WriteNumericValuePreservingKind(key, StatusName, BatteryProfiles.EnabledRegistryStatus);
        WriteNumericValuePreservingKind(key, ModeName, profile.RegistryValue);
        key.Flush();
        return Task.CompletedTask;
    }

    private static int? ReadNumericValue(RegistryKey key, string name) => key.GetValue(name) switch
    {
        int value => value,
        string value when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => null
    };

    private static string? ReadValueKind(RegistryKey key, string name)
    {
        try
        {
            return key.GetValueKind(name).ToString();
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static void WriteNumericValuePreservingKind(RegistryKey key, string name, int value)
    {
        var kind = key.GetValueKind(name);
        switch (kind)
        {
            case RegistryValueKind.DWord:
                key.SetValue(name, value, RegistryValueKind.DWord);
                break;
            case RegistryValueKind.String:
                key.SetValue(name, value.ToString(CultureInfo.InvariantCulture), RegistryValueKind.String);
                break;
            default:
                throw new InvalidOperationException($"OEM registry value '{name}' has unsupported kind '{kind}'.");
        }
    }
}

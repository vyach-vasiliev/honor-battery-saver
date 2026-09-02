using System.Diagnostics;
using System.Security.Principal;
using System.Windows;
using HonorBatterySaver.Core;
using MessageBox = System.Windows.MessageBox;

namespace HonorBatterySaver.Tray;

public static class HardwareDiagnosticRunner
{
    public static void StartElevated(BatteryMode mode, UiLanguage language)
    {
        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot determine the tray executable path.");
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };
        startInfo.ArgumentList.Add("--hardware-diagnostic");
        startInfo.ArgumentList.Add(mode.ToString());
        startInfo.ArgumentList.Add(language.ToString());
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(Strings.Get("Hardware_ProcessNotCreated"));
    }

    public static async Task RunElevatedChildAsync(string modeText, string languageText)
    {
        var language = Enum.TryParse<UiLanguage>(languageText, ignoreCase: true, out var parsedLanguage) &&
                       Enum.IsDefined(parsedLanguage)
            ? parsedLanguage
            : UiLanguage.System;
        Strings.ApplyUiLanguage(language);

        if (!Enum.TryParse<BatteryMode>(modeText, ignoreCase: true, out var mode) || !BatteryProfiles.IsSupported(mode))
        {
            MessageBox.Show(Strings.Get("Hardware_InvalidMode"), Strings.Get("Diagnostics_HardwareCheck"),
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
        {
            MessageBox.Show(Strings.Get("Hardware_AdminOnly"), Strings.Get("Diagnostics_HardwareCheck"),
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var recovery = await ServiceRecoveryManager.RecoverFromElevatedProcessAsync();
        if (!recovery.Success)
        {
            MessageBox.Show(
                Strings.Format("Hardware_ServiceStartFailed", recovery.Message),
                Strings.Get("Diagnostics_HardwareCheck"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        var client = new ServiceClient();
        var statusResponse = await client.SendAsync(new IpcRequest(IpcOperation.GetStatus));
        var profile = BatteryProfiles.Get(mode);
        var registry = statusResponse.Status?.Registry;
        var oldValues = registry is { KeyExists: true }
            ? $"PowerSafeManagerStatus={registry.Status?.ToString() ?? "?"}\nPowerSafeManagerMode={registry.Mode?.ToString() ?? "?"}"
            : Strings.Get("Hardware_RegistryUnavailable");
        var payload = string.Join(' ', profile.OemPayload.Select(value => value.ToString("X2")));
        var confirmation = MessageBox.Show(
            Strings.Format("Hardware_Confirmation", Strings.GetModeName(mode), payload, oldValues),
            Strings.Get("Hardware_ConfirmationTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        var result = await client.SendAsync(new IpcRequest(IpcOperation.ApplyMode, mode, Force: true));
        MessageBox.Show(
            ServiceText.LocalizeKnownMessage(result.Message, Strings.CurrentCulture),
            Strings.Get("Hardware_ResultTitle"),
            MessageBoxButton.OK,
            result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
    }
}

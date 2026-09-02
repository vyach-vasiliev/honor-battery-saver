using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using HonorBatterySaver.Core;
using MessageBox = System.Windows.MessageBox;

namespace HonorBatterySaver.Tray;

public sealed record ModeChoice(BatteryMode Mode, string Name);
public sealed record LanguageChoice(UiLanguage Language, string Name);

public sealed record TrayDiagnostics(
    WifiSnapshot Wifi,
    PowerSource PowerSource,
    DecisionResult Decision,
    BatteryMode? LastSuccessfulMode,
    ServiceStatus? ServiceStatus,
    string LastAttemptText);

public partial class SettingsWindow : Window
{
    private readonly Func<AppSettings> _getSettings;
    private readonly Func<Task<TrayDiagnostics>> _getDiagnostics;
    private readonly Func<AppSettings, Task> _saveSettings;
    private readonly Func<WifiCatalogSnapshot> _getWifiCatalog;
    private readonly ObservableCollection<NetworkRule> _rules = [];

    public SettingsWindow(
        Func<AppSettings> getSettings,
        Func<Task<TrayDiagnostics>> getDiagnostics,
        Func<AppSettings, Task> saveSettings,
        Func<WifiCatalogSnapshot> getWifiCatalog)
    {
        InitializeComponent();
        ThemeManager.Attach(this);
        _getSettings = getSettings;
        _getDiagnostics = getDiagnostics;
        _saveSettings = saveSettings;
        _getWifiCatalog = getWifiCatalog;
        RulesGrid.ItemsSource = _rules;
        RefreshLocalizedChoices();
        HardwareModeComboBox.SelectedValue = BatteryMode.Travel;
        Strings.CultureChanged += OnCultureChanged;
        Closing += OnClosing;
    }

    public bool AllowClose { get; set; }

    public async void Prepare(bool showDiagnostics)
    {
        LoadSettings();
        MainTabs.SelectedIndex = showDiagnostics ? 1 : 0;
        await RefreshDiagnosticsAsync();
    }

    private void LoadSettings()
    {
        var settings = _getSettings();
        RefreshLocalizedChoices();
        AutomaticCheckBox.IsChecked = settings.AutomaticMode;
        AutostartCheckBox.IsChecked = settings.StartWithWindows;
        LanguageComboBox.SelectedValue = settings.Language;
        _rules.Clear();
        foreach (var rule in settings.NetworkRules)
        {
            _rules.Add(new NetworkRule
            {
                Id = rule.Id,
                Ssid = rule.Ssid,
                Mode = rule.Mode,
                IsEnabled = rule.IsEnabled
            });
        }

        UpdateEmptyState();
        SaveHint.Text = string.Empty;
    }

    private async Task RefreshDiagnosticsAsync()
    {
        var diagnostics = await _getDiagnostics();
        WifiValue.Text = diagnostics.Wifi.Ssids.Count == 0
            ? Strings.Get("Diagnostics_UnknownNetwork")
            : string.Join(", ", diagnostics.Wifi.Ssids.Select(ssid =>
                Strings.Format("Diagnostics_NetworkName", ssid)));
        WifiAccessValue.Text = diagnostics.Wifi.Message;
        WifiPermissionPanel.Visibility = diagnostics.Wifi.AccessDenied ? Visibility.Visible : Visibility.Collapsed;
        PowerValue.Text = diagnostics.PowerSource switch
        {
            PowerSource.Ac => Strings.Get("Diagnostics_AcPower"),
            PowerSource.Battery => Strings.Get("Diagnostics_BatteryPower"),
            _ => Strings.Get("Diagnostics_UnknownPower")
        };
        DecisionValue.Text = diagnostics.Decision.Mode is BatteryMode desiredMode
            ? $"{DescribeMode(desiredMode)}\n{diagnostics.Decision.Reason}"
            : diagnostics.Decision.Reason;
        AppliedValue.Text = diagnostics.LastSuccessfulMode is BatteryMode appliedMode
            ? DescribeMode(appliedMode)
            : Strings.Get("Diagnostics_NoSuccessfulApply");
        ServiceValue.Text = diagnostics.ServiceStatus is null
            ? Strings.Get("Diagnostics_ServiceUnavailable")
            : ServiceText.DescribeStatus(diagnostics.ServiceStatus, Strings.CurrentCulture);
        RegistryValue.Text = diagnostics.ServiceStatus?.Registry is { KeyExists: true } registry
            ? Strings.Format("Diagnostics_RegistryValues",
                registry.Status?.ToString() ?? "?", registry.StatusKind ?? Strings.Get("Common_UnknownType"),
                registry.Mode?.ToString() ?? "?", registry.ModeKind ?? Strings.Get("Common_UnknownType"))
            : Strings.Get("Diagnostics_RegistryUnavailable");
        AttemptValue.Text = diagnostics.LastAttemptText;
        var displayedMode = diagnostics.LastSuccessfulMode ?? diagnostics.Decision.Mode;
        HeaderStatusText.Text = diagnostics.ServiceStatus is null
            ? Strings.Get("Diagnostics_ServiceUnavailable")
            : displayedMode is BatteryMode headerMode
                ? Strings.Format("Diagnostics_HeaderMode", Strings.GetModeName(headerMode),
                    Strings.Get(diagnostics.PowerSource == PowerSource.Ac
                        ? "Diagnostics_HeaderAc"
                        : "Diagnostics_HeaderBattery"))
                : Strings.Get("Diagnostics_HeaderNoChange");
    }

    private void AddNetworkRule_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SsidDialog(
            string.Empty,
            BatteryMode.Home,
            GetModeChoices(),
            _getWifiCatalog(),
            isEditing: false)
        {
            Owner = this
        };
        if (dialog.ShowDialog() == true)
        {
            AddOrSelectRule(dialog.Ssid, dialog.Mode);
        }
    }

    private void EditRule_Click(object sender, RoutedEventArgs e)
    {
        if (RulesGrid.SelectedItem is not NetworkRule selected)
        {
            return;
        }

        var dialog = new SsidDialog(
            selected.Ssid,
            selected.Mode,
            GetModeChoices(),
            _getWifiCatalog(),
            isEditing: true)
        {
            Owner = this
        };
        if (dialog.ShowDialog() == true)
        {
            var index = _rules.IndexOf(selected);
            _rules[index] = new NetworkRule
            {
                Id = selected.Id,
                Ssid = dialog.Ssid,
                Mode = dialog.Mode,
                IsEnabled = selected.IsEnabled
            };
            RulesGrid.SelectedIndex = index;
        }
    }

    private void DeleteRule_Click(object sender, RoutedEventArgs e)
    {
        if (RulesGrid.SelectedItem is NetworkRule selected)
        {
            _rules.Remove(selected);
            UpdateEmptyState();
        }
    }

    private void MoveRuleUp_Click(object sender, RoutedEventArgs e) => MoveSelectedRule(-1);

    private void MoveRuleDown_Click(object sender, RoutedEventArgs e) => MoveSelectedRule(1);

    private void MoveSelectedRule(int offset)
    {
        if (RulesGrid.SelectedItem is not NetworkRule selected)
        {
            return;
        }

        var oldIndex = _rules.IndexOf(selected);
        var newIndex = oldIndex + offset;
        if (newIndex < 0 || newIndex >= _rules.Count)
        {
            return;
        }

        _rules.Move(oldIndex, newIndex);
        RulesGrid.SelectedItem = selected;
        RulesGrid.ScrollIntoView(selected);
    }

    private void AddOrSelectRule(string ssid, BatteryMode mode)
    {
        var existing = _rules.FirstOrDefault(rule => string.Equals(rule.Ssid, ssid, StringComparison.Ordinal));
        if (existing is not null)
        {
            RulesGrid.SelectedItem = existing;
            RulesGrid.ScrollIntoView(existing);
            return;
        }

        var rule = new NetworkRule { Ssid = ssid, Mode = mode };
        _rules.Add(rule);
        UpdateEmptyState();
        RulesGrid.SelectedItem = rule;
        RulesGrid.ScrollIntoView(rule);
    }

    private void UpdateEmptyState() => EmptyRulesMessage.Visibility =
        _rules.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_rules.Any(rule => string.IsNullOrEmpty(rule.Ssid)))
        {
            MessageBox.Show(this, Strings.Get("Settings_SsidEmpty"), Strings.Get("App_Name"),
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var settings = _getSettings();
        settings.AutomaticMode = AutomaticCheckBox.IsChecked == true;
        settings.StartWithWindows = AutostartCheckBox.IsChecked == true;
        settings.Language = LanguageComboBox.SelectedValue is UiLanguage language
            ? language
            : UiLanguage.System;
        settings.NetworkRules = _rules.Select(rule => new NetworkRule
        {
            Id = rule.Id,
            Ssid = rule.Ssid,
            Mode = rule.Mode,
            IsEnabled = rule.IsEnabled
        }).ToList();

        try
        {
            await _saveSettings(settings);
            SaveHint.Text = Strings.Get("Settings_Saved");
            Hide();
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, Strings.Format("Settings_SaveFailed", exception.Message),
                Strings.Get("App_Name"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        LoadSettings();
        Hide();
    }

    private async void RefreshDiagnostics_Click(object sender, RoutedEventArgs e) => await RefreshDiagnosticsAsync();

    private void OpenLocationSettings_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("ms-settings:privacy-location") { UseShellExecute = true });
    }

    private void OpenWebsite_Click(object sender, RoutedEventArgs e) =>
        OpenProjectLink(ProjectInfo.GetWebsiteUrl(Strings.CurrentCulture));

    private void OpenProjectLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: string url })
        {
            OpenProjectLink(url);
        }
    }

    private void OpenProjectLink(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(this, Strings.Format("Project_OpenFailed", url),
                Strings.Get("App_Name"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RunHardwareDiagnostic_Click(object sender, RoutedEventArgs e)
    {
        if (HardwareModeComboBox.SelectedValue is not BatteryMode mode)
        {
            return;
        }

        try
        {
            var language = LanguageComboBox.SelectedValue is UiLanguage selectedLanguage
                ? selectedLanguage
                : _getSettings().Language;
            HardwareDiagnosticRunner.StartElevated(mode, language);
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            MessageBox.Show(this, Strings.Get("Hardware_ElevationCancelled"),
                Strings.Get("Diagnostics_HardwareCheck"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this,
                Strings.Format("Hardware_OpenFailed", exception.Message),
                Strings.Get("Diagnostics_HardwareCheck"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
        }
        else
        {
            Strings.CultureChanged -= OnCultureChanged;
        }
    }

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(RefreshLocalizedChoices);
            return;
        }

        RefreshLocalizedChoices();
    }

    private void RefreshLocalizedChoices()
    {
        WebsiteLink.ToolTip = ProjectInfo.GetWebsiteUrl(Strings.CurrentCulture);
        var selectedHardwareMode = HardwareModeComboBox.SelectedValue;
        var selectedLanguage = LanguageComboBox.SelectedValue;
        HardwareModeComboBox.ItemsSource = GetModeChoices();
        LanguageChoice[] languageChoices =
        [
            new LanguageChoice(UiLanguage.System, Strings.Get("Language_System")),
            new LanguageChoice(UiLanguage.Russian, Strings.Get("Language_Russian")),
            new LanguageChoice(UiLanguage.English, Strings.Get("Language_English"))
        ];
        LanguageComboBox.ItemsSource = languageChoices;
        HardwareModeComboBox.SelectedValue = selectedHardwareMode ?? BatteryMode.Travel;
        LanguageComboBox.SelectedValue = selectedLanguage ?? UiLanguage.System;
    }

    private static IReadOnlyList<ModeChoice> GetModeChoices() =>
    [
        new(BatteryMode.Home, Strings.Get("Mode_HomeRange")),
        new(BatteryMode.Office, Strings.Get("Mode_OfficeRange")),
        new(BatteryMode.Travel, Strings.Get("Mode_TravelRange"))
    ];

    private static string DescribeMode(BatteryMode mode) => mode switch
    {
        BatteryMode.Home => Strings.Get("Mode_HomeDetailed"),
        BatteryMode.Office => Strings.Get("Mode_OfficeDetailed"),
        BatteryMode.Travel => Strings.Get("Mode_TravelDetailed"),
        _ => Strings.GetModeName(mode)
    };
}

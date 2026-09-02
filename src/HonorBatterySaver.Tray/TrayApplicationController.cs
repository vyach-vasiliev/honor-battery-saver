using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using HonorBatterySaver.Core;
using Forms = System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;

namespace HonorBatterySaver.Tray;

public sealed class TrayApplicationController : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly AtomicSettingsStore _settingsStore;
    private readonly BatteryModeDecisionEngine _decisionEngine = new();
    private readonly ApplyCommandGate _commandGate = new();
    private readonly ServiceClient _serviceClient = new();
    private readonly NativeWifiProvider _wifiProvider = new();
    private readonly SemaphoreSlim _evaluationLock = new(1, 1);
    private readonly object _scheduleSync = new();
    private readonly CancellationTokenSource _lifetime = new();

    private AppSettings _settings = AppSettings.CreateDefault();
    private SystemStateMonitor? _systemMonitor;
    private SettingsWindow? _settingsWindow;
    private Forms.NotifyIcon? _notifyIcon;
    private Icon? _applicationIcon;
    private Forms.ContextMenuStrip? _trayMenu;
    private readonly Dictionary<(BatteryMode Mode, bool HasError), Icon> _modeIcons = [];
    private Forms.ToolStripMenuItem? _headerItem;
    private Forms.ToolStripMenuItem? _automaticItem;
    private Forms.ToolStripMenuItem? _statusItem;
    private Forms.ToolStripMenuItem? _reapplyItem;
    private Forms.ToolStripMenuItem? _diagnosticsItem;
    private Forms.ToolStripMenuItem? _exitItem;
    private readonly Dictionary<BatteryMode, Forms.ToolStripMenuItem> _modeItems = [];
    private CancellationTokenSource? _scheduledEvaluation;
    private bool _pendingForce;
    private string _pendingReason = Strings.Get("Reason_ConditionsChanged");
    private WifiSnapshot _wifi = new([], false, Strings.Get("Wifi_NotChecked"));
    private PowerSource _powerSource = PowerSource.Unknown;
    private DecisionResult _decision = new(null, Strings.Get("Decision_WaitingFirstCheck"));
    private IpcResponse? _lastResponse;
    private string? _lastNotifiedError;

    public TrayApplicationController(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HonorBatterySaver",
            "settings.json");
        _settingsStore = new AtomicSettingsStore(settingsPath);
    }

    public async Task InitializeAsync()
    {
        var loaded = await _settingsStore.LoadAsync(_lifetime.Token);
        _settings = loaded.Settings;
        Strings.ApplyUiLanguage(_settings.Language);
        TryUpdateAutostart(_settings.StartWithWindows);

        _systemMonitor = new SystemStateMonitor();
        _systemMonitor.NetworkChanged += (_, _) => QueueEvaluation(
            TimeSpan.FromSeconds(4), false, Strings.Get("Reason_WifiChanged"));
        _systemMonitor.PowerSourceChanged += (_, source) => QueueEvaluation(
            source == PowerSource.Battery ? TimeSpan.Zero : TimeSpan.FromSeconds(4),
            false,
            Strings.Get("Reason_PowerChanged"));
        _systemMonitor.Resumed += (_, _) => QueueEvaluation(
            TimeSpan.FromSeconds(4), true, Strings.Get("Reason_Resumed"));
        _systemMonitor.TaskbarCreated += (_, _) => RestoreTrayIcon();

        CreateTrayIcon();
        ThemeManager.ThemeChanged += OnThemeChanged;
        _settingsWindow = new SettingsWindow(
            () => _settings.Clone(),
            GetDiagnosticsAsync,
            SaveSettingsAsync,
            _wifiProvider.GetNetworkCatalog);

        RefreshLocalState();
        UpdateTray();
        if (loaded.Warning is not null)
        {
            _notifyIcon!.ShowBalloonTip(
                6000, Strings.Get("Tray_SettingsRestoredTitle"), loaded.Warning, Forms.ToolTipIcon.Warning);
        }

    }

    public void Start() => QueueEvaluation(
        TimeSpan.FromSeconds(3), true, Strings.Get("Reason_AppStarted"));

    public async Task<bool> EnsureDisclaimerAcceptedAsync()
    {
        if (_settings.DisclaimerAcceptedVersion >= AppSettings.CurrentDisclaimerVersion)
        {
            return true;
        }

        var result = MessageBox.Show(
            Strings.Get("Disclaimer_FirstRunBody"),
            Strings.Get("Disclaimer_FirstRunTitle"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (result != MessageBoxResult.Yes)
        {
            return false;
        }

        _settings.DisclaimerAcceptedVersion = AppSettings.CurrentDisclaimerVersion;
        await _settingsStore.SaveAsync(_settings, _lifetime.Token);
        return true;
    }

    public void ShowSettings() => ShowWindow(showDiagnostics: false);

    public void NotifyServiceRecoveryFailure(string message)
    {
        if (_notifyIcon is null)
        {
            return;
        }

        _lastNotifiedError = message;
        _notifyIcon.ShowBalloonTip(
            7000, Strings.Get("Tray_ServiceNotRunningTitle"), message, Forms.ToolTipIcon.Warning);
    }

    private void ShowDiagnostics() => ShowWindow(showDiagnostics: true);

    private void ShowWindow(bool showDiagnostics)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(() => ShowWindow(showDiagnostics));
            return;
        }

        if (_settingsWindow is null)
        {
            return;
        }

        _settingsWindow.Prepare(showDiagnostics);
        if (!_settingsWindow.IsVisible)
        {
            _settingsWindow.Show();
        }

        if (_settingsWindow.WindowState == WindowState.Minimized)
        {
            _settingsWindow.WindowState = WindowState.Normal;
        }

        _settingsWindow.Activate();
        _settingsWindow.Topmost = true;
        _settingsWindow.Topmost = false;
        _settingsWindow.Focus();
    }

    private void CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip
        {
            ShowImageMargin = true,
            ShowCheckMargin = true,
            Padding = new Forms.Padding(4),
            Font = new System.Drawing.Font("Segoe UI", 9.5f)
        };
        _trayMenu = menu;
        _headerItem = new Forms.ToolStripMenuItem(Strings.Get("App_Name"))
        {
            Enabled = false,
            Font = new System.Drawing.Font(System.Drawing.SystemFonts.MenuFont!, System.Drawing.FontStyle.Bold)
        };
        _automaticItem = new Forms.ToolStripMenuItem(Strings.Get("Tray_AutoEnabled"));
        _automaticItem.Click += async (_, _) => await ToggleAutomaticModeAsync();
        _statusItem = new Forms.ToolStripMenuItem(Strings.Get("Tray_StatusWaiting")) { Enabled = false };
        menu.Items.AddRange([_headerItem, _automaticItem, _statusItem, new Forms.ToolStripSeparator()]);

        foreach (var mode in new[] { BatteryMode.Home, BatteryMode.Office, BatteryMode.Travel })
        {
            var item = new Forms.ToolStripMenuItem(Strings.GetModeName(mode))
            {
                CheckOnClick = false,
                Tag = mode
            };
            item.Click += async (_, _) => await SelectManualModeAsync(mode);
            _modeItems[mode] = item;
            menu.Items.Add(item);
        }

        menu.Items.Add(new Forms.ToolStripSeparator());
        _reapplyItem = new Forms.ToolStripMenuItem(Strings.Get("Tray_Reapply"))
        {
            ToolTipText = Strings.Get("Tray_ReapplyHint")
        };
        _reapplyItem.Click += (_, _) => QueueEvaluation(
            TimeSpan.Zero, true, Strings.Get("Reason_ManualSync"));
        _diagnosticsItem = new Forms.ToolStripMenuItem(Strings.Get("Tray_Diagnostics"));
        _diagnosticsItem.Click += (_, _) => ShowDiagnostics();
        _exitItem = new Forms.ToolStripMenuItem(Strings.Get("Tray_Exit"));
        _exitItem.Click += (_, _) => System.Windows.Application.Current.Shutdown();
        menu.Items.AddRange([_reapplyItem, _diagnosticsItem, new Forms.ToolStripSeparator(), _exitItem]);

        _applicationIcon = AppIconRenderer.CreateIcon();
        _notifyIcon = new Forms.NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = _applicationIcon,
            Text = Strings.Get("App_Name"),
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => ShowSettings();
        ApplyTrayMenuTheme();
    }

    private async Task ToggleAutomaticModeAsync()
    {
        _settings.AutomaticMode = !_settings.AutomaticMode;
        await _settingsStore.SaveAsync(_settings, _lifetime.Token);
        UpdateTray();
        if (_settings.AutomaticMode)
        {
            QueueEvaluation(TimeSpan.Zero, true, Strings.Get("Reason_AutomaticEnabled"));
        }
    }

    private async Task SelectManualModeAsync(BatteryMode mode)
    {
        SettingsBehavior.SelectManualMode(_settings, mode);
        await _settingsStore.SaveAsync(_settings, _lifetime.Token);
        RefreshLocalState();
        _decision = _decisionEngine.Decide(new DecisionInput(_powerSource, _wifi.Ssids, _settings));
        UpdateTray();
        await ApplyDecisionAsync(mode, force: true, Strings.Get("Reason_ManualSelection"));
    }

    private async Task SaveSettingsAsync(AppSettings settings)
    {
        var wasAutomatic = _settings.AutomaticMode;
        var previousLanguage = _settings.Language;
        _settings = SettingsBehavior.Normalize(settings);
        await _settingsStore.SaveAsync(_settings, _lifetime.Token);
        if (previousLanguage != _settings.Language)
        {
            Strings.ApplyUiLanguage(_settings.Language);
            ApplyTrayLocalization();
        }
        TryUpdateAutostart(_settings.StartWithWindows);
        UpdateTray();
        QueueEvaluation(
            TimeSpan.Zero,
            _settings.AutomaticMode && !wasAutomatic,
            Strings.Get("Reason_SettingsChanged"));
    }

    private void QueueEvaluation(TimeSpan delay, bool force, string reason)
    {
        CancellationTokenSource scheduled;
        lock (_scheduleSync)
        {
            _pendingForce |= force;
            _pendingReason = reason;
            _scheduledEvaluation?.Cancel();
            _scheduledEvaluation?.Dispose();
            _scheduledEvaluation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            scheduled = _scheduledEvaluation;
        }

        _ = RunScheduledEvaluationAsync(delay, scheduled);
    }

    private async Task RunScheduledEvaluationAsync(TimeSpan delay, CancellationTokenSource scheduled)
    {
        try
        {
            await Task.Delay(delay, scheduled.Token);
            bool force;
            string reason;
            lock (_scheduleSync)
            {
                if (!ReferenceEquals(_scheduledEvaluation, scheduled))
                {
                    return;
                }

                force = _pendingForce;
                reason = _pendingReason;
                _pendingForce = false;
            }

            await await _dispatcher.InvokeAsync(() => EvaluateAsync(force, reason));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task EvaluateAsync(bool force, string reason)
    {
        await _evaluationLock.WaitAsync(_lifetime.Token);
        try
        {
            RefreshLocalState();
            _decision = _decisionEngine.Decide(new DecisionInput(_powerSource, _wifi.Ssids, _settings));
            UpdateTray();
            if (_decision.Mode is BatteryMode desiredMode && _commandGate.ShouldApply(desiredMode, force))
            {
                await ApplyDecisionAsync(desiredMode, force, reason);
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
        finally
        {
            _evaluationLock.Release();
        }
    }

    private async Task ApplyDecisionAsync(BatteryMode desiredMode, bool force, string reason)
    {
        BatteryMode? modeChanged = null;
        _lastResponse = await _serviceClient.SendAsync(
            new IpcRequest(IpcOperation.ApplyMode, desiredMode, force), _lifetime.Token);
        if (_lastResponse.Success && _lastResponse.ApplyResult?.Outcome == ApplyOutcome.Success)
        {
            var previousMode = _commandGate.LastSuccessfulMode;
            _commandGate.RecordSuccess(desiredMode);
            _lastNotifiedError = null;
            if (previousMode != desiredMode)
            {
                modeChanged = desiredMode;
            }
        }
        else
        {
            NotifyErrorOnce(
                $"{reason}: {ServiceText.LocalizeKnownMessage(_lastResponse.Message, Strings.CurrentCulture)}");
        }

        UpdateTray();
        if (modeChanged is BatteryMode changedMode)
        {
            NotifyModeChanged(changedMode);
        }
    }

    private async Task<TrayDiagnostics> GetDiagnosticsAsync()
    {
        RefreshLocalState();
        _decision = _decisionEngine.Decide(new DecisionInput(_powerSource, _wifi.Ssids, _settings));
        var response = await _serviceClient.SendAsync(new IpcRequest(IpcOperation.GetStatus), _lifetime.Token);
        var attempt = _lastResponse?.ApplyResult ?? response.Status?.LastAttempt;
        var attemptText = attempt is null
            ? _lastResponse is null
                ? Strings.Get("Tray_NoApplyAttempts")
                : ServiceText.LocalizeKnownMessage(_lastResponse.Message, Strings.CurrentCulture)
            : $"{attempt.AttemptedAt.ToString("g", Strings.CurrentCulture)} — " +
              ServiceText.DescribeAttempt(attempt, Strings.CurrentCulture);
        return new TrayDiagnostics(
            _wifi,
            _powerSource,
            _decision,
            _commandGate.LastSuccessfulMode,
            response.Status,
            attemptText);
    }

    private void RefreshLocalState()
    {
        _powerSource = _systemMonitor?.GetPowerSource() ?? PowerSource.Unknown;
        _wifi = _wifiProvider.GetConnectedNetworks();
    }

    private void UpdateTray()
    {
        if (_notifyIcon is null || _automaticItem is null || _statusItem is null)
        {
            return;
        }

        var displayedMode = _commandGate.LastSuccessfulMode ?? _decision.Mode;
        var displayedProfile = displayedMode is BatteryMode mode ? BatteryProfiles.Get(mode) : null;
        var modeName = displayedMode is BatteryMode localizedMode
            ? Strings.GetModeName(localizedMode)
            : Strings.Get("Tray_NoProfile");
        var powerName = _powerSource switch
        {
            PowerSource.Ac => "AC",
            PowerSource.Battery => Strings.Get("Tray_Battery"),
            _ => Strings.Get("Tray_UnknownPower")
        };
        var ssid = _wifi.Ssids.FirstOrDefault() ?? Strings.Get("Tray_UnknownNetwork");
        _automaticItem.Text = Strings.Format("Tray_AutoState",
            Strings.Get(_settings.AutomaticMode ? "Tray_On" : "Tray_Off"));
        _automaticItem.Checked = _settings.AutomaticMode;
        _statusItem.Text = _commandGate.LastSuccessfulMode is BatteryMode
            ? Strings.Format("Tray_StatusApplied", modeName, displayedProfile!.StopChargePercent, powerName, ssid)
            : _decision.Mode is BatteryMode
                ? Strings.Format("Tray_StatusPending", modeName, powerName, ssid)
                : Strings.Format("Tray_StatusNoChange", powerName, ssid);
        var unsupported = _lastResponse?.ApplyResult?.Outcome == ApplyOutcome.Unsupported;
        foreach (var pair in _modeItems)
        {
            pair.Value.Checked = displayedMode is BatteryMode checkedMode && pair.Key == checkedMode;
            pair.Value.Enabled = !unsupported;
        }

        if (_reapplyItem is not null)
        {
            var waitingForAc = _settings.AutomaticMode && _powerSource != PowerSource.Ac;
            _reapplyItem.Enabled = !unsupported && !waitingForAc;
            _reapplyItem.ToolTipText = waitingForAc
                ? Strings.Get("Tray_ReapplyPlugInHint")
                : Strings.Get("Tray_ReapplyHint");
        }

        var hasError = _lastResponse is { Success: false } || _lastResponse?.ApplyResult?.Outcome is
            ApplyOutcome.Failed or ApplyOutcome.PartialFailure or ApplyOutcome.Unsupported;
        _notifyIcon.Icon = _commandGate.LastSuccessfulMode is BatteryMode activeMode
            ? GetModeIcon(activeMode, hasError)
            : hasError
                ? SystemIcons.Error
                : _applicationIcon ?? SystemIcons.Application;
        var tooltip = hasError
            ? Strings.Format("Tray_TooltipError", modeName)
            : _commandGate.LastSuccessfulMode is BatteryMode
                ? Strings.Format("Tray_TooltipApplied", modeName, displayedProfile!.StopChargePercent)
                : _decision.Mode is BatteryMode
                    ? Strings.Get("Tray_TooltipPending")
                    : Strings.Get("Tray_TooltipBattery");
        _notifyIcon.Text = tooltip.Length <= 63 ? tooltip : tooltip[..63];
    }

    private void NotifyErrorOnce(string message)
    {
        if (_notifyIcon is null || string.Equals(_lastNotifiedError, message, StringComparison.Ordinal))
        {
            return;
        }

        _lastNotifiedError = message;
        _notifyIcon.ShowBalloonTip(
            6000, Strings.Get("Tray_ApplyFailedTitle"), message, Forms.ToolTipIcon.Error);
    }

    private void NotifyModeChanged(BatteryMode mode)
    {
        if (_notifyIcon is null)
        {
            return;
        }

        var profile = BatteryProfiles.Get(mode);
        _notifyIcon.ShowBalloonTip(
            5000,
            Strings.Get("Tray_ProfileChangedTitle"),
            Strings.Format("Tray_ProfileChangedMessage", Strings.GetModeName(mode),
                profile.ResumeChargePercent, profile.StopChargePercent),
            Forms.ToolTipIcon.None);
    }

    private Icon GetModeIcon(BatteryMode mode, bool hasError)
    {
        var key = (mode, hasError);
        if (!_modeIcons.TryGetValue(key, out var icon))
        {
            var systemIconSize = Math.Max(16, Forms.SystemInformation.SmallIconSize.Width);
            icon = AppIconRenderer.CreateModeIcon(
                BatteryProfiles.Get(mode).StopChargePercent,
                hasError,
                systemIconSize);
            _modeIcons[key] = icon;
        }

        return icon;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (_dispatcher.CheckAccess())
        {
            ApplyTrayMenuTheme();
        }
        else
        {
            _dispatcher.BeginInvoke(ApplyTrayMenuTheme);
        }
    }

    private void ApplyTrayMenuTheme()
    {
        if (_trayMenu is null)
        {
            return;
        }

        var dark = ThemeManager.IsDark;
        var surface = dark ? Color.FromArgb(23, 26, 34) : Color.White;
        var text = dark ? Color.FromArgb(243, 245, 250) : Color.FromArgb(24, 32, 51);
        _trayMenu.Renderer = new TrayMenuRenderer(dark);
        _trayMenu.BackColor = surface;
        _trayMenu.ForeColor = text;
        foreach (Forms.ToolStripItem item in _trayMenu.Items)
        {
            item.BackColor = surface;
            item.ForeColor = text;
        }
    }

    private void ApplyTrayLocalization()
    {
        if (_headerItem is null || _automaticItem is null || _statusItem is null)
        {
            return;
        }

        _headerItem.Text = Strings.Get("App_Name");
        _reapplyItem!.Text = Strings.Get("Tray_Reapply");
        _diagnosticsItem!.Text = Strings.Get("Tray_Diagnostics");
        _exitItem!.Text = Strings.Get("Tray_Exit");
        foreach (var pair in _modeItems)
        {
            pair.Value.Text = Strings.GetModeName(pair.Key);
        }

        if (_notifyIcon is not null)
        {
            _notifyIcon.Text = Strings.Get("App_Name");
        }

        UpdateTray();
    }

    private void RestoreTrayIcon()
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Visible = true;
        }
    }

    private void TryUpdateAutostart(bool enabled)
    {
        try
        {
            AutostartManager.SetEnabled(enabled);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            NotifyErrorOnce(Strings.Get("Tray_AutostartFailed"));
        }
    }

    public void Dispose()
    {
        ThemeManager.ThemeChanged -= OnThemeChanged;
        _lifetime.Cancel();
        lock (_scheduleSync)
        {
            _scheduledEvaluation?.Cancel();
            _scheduledEvaluation?.Dispose();
            _scheduledEvaluation = null;
        }

        _systemMonitor?.Dispose();
        if (_settingsWindow is not null)
        {
            _settingsWindow.AllowClose = true;
            _settingsWindow.Close();
        }

        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        _applicationIcon?.Dispose();
        foreach (var icon in _modeIcons.Values)
        {
            icon.Dispose();
        }
        _modeIcons.Clear();

        _evaluationLock.Dispose();
        _lifetime.Dispose();
    }
}

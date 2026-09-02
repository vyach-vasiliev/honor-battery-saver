using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using HonorBatterySaver.Core;
using Microsoft.Win32;

namespace HonorBatterySaver.Tray;

public sealed class SystemStateMonitor : IDisposable
{
    private const int WmPowerBroadcast = 0x0218;
    private const int PbtApmResumeAutomatic = 0x0012;
    private const int PbtPowerSettingChange = 0x8013;
    private const int DeviceNotifyWindowHandle = 0;
    private static readonly Guid AcDcPowerSource = new("5D3E9A59-E9D5-4B00-A6BD-FF34FF516548");

    private readonly HwndSource _source;
    private readonly int _taskbarCreatedMessage;
    private IntPtr _powerNotification;

    public SystemStateMonitor()
    {
        var parameters = new HwndSourceParameters("HonorBatterySaver.Events")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WindowProcedure);
        var powerSourceGuid = AcDcPowerSource;
        _powerNotification = RegisterPowerSettingNotification(
            _source.Handle, ref powerSourceGuid, DeviceNotifyWindowHandle);
        _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public event EventHandler? NetworkChanged;
    public event EventHandler<PowerSource>? PowerSourceChanged;
    public event EventHandler? Resumed;
    public event EventHandler? TaskbarCreated;

    public PowerSource GetPowerSource()
    {
        if (!GetSystemPowerStatus(out var status))
        {
            return PowerSource.Unknown;
        }

        return status.AcLineStatus switch
        {
            1 => PowerSource.Ac,
            0 => PowerSource.Battery,
            _ => PowerSource.Unknown
        };
    }

    private IntPtr WindowProcedure(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == _taskbarCreatedMessage)
        {
            TaskbarCreated?.Invoke(this, EventArgs.Empty);
        }
        else if (message == WmPowerBroadcast && wParam.ToInt32() == PbtPowerSettingChange && lParam != IntPtr.Zero)
        {
            var setting = Marshal.PtrToStructure<PowerBroadcastSetting>(lParam);
            if (setting.PowerSetting == AcDcPowerSource && setting.DataLength >= sizeof(int))
            {
                var value = Marshal.ReadInt32(lParam, Marshal.SizeOf<PowerBroadcastSetting>());
                var source = value switch
                {
                    0 => PowerSource.Ac,
                    1 => PowerSource.Battery,
                    _ => PowerSource.Unknown
                };
                PowerSourceChanged?.Invoke(this, source);
            }
        }
        else if (message == WmPowerBroadcast && wParam.ToInt32() == PbtApmResumeAutomatic)
        {
            Resumed?.Invoke(this, EventArgs.Empty);
        }

        return IntPtr.Zero;
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e) => NetworkChanged?.Invoke(this, EventArgs.Empty);

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            Resumed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        if (_powerNotification != IntPtr.Zero)
        {
            UnregisterPowerSettingNotification(_powerNotification);
            _powerNotification = IntPtr.Zero;
        }

        _source.RemoveHook(WindowProcedure);
        _source.Dispose();
    }

#pragma warning disable CS0649
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PowerBroadcastSetting
    {
        public Guid PowerSetting;
        public int DataLength;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte AcLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }
#pragma warning restore CS0649

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus systemPowerStatus);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr RegisterPowerSettingNotification(IntPtr recipient, ref Guid powerSettingGuid, int flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterPowerSettingNotification(IntPtr handle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegisterWindowMessage(string message);
}

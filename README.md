# Honor Battery Saver

Honor Battery Saver is a small Windows 11 application that switches the OEM
battery-protection profile on compatible HONOR laptops based on the current
Wi-Fi network and power source.

The application consists of two processes:

- `HonorBatterySaver.Tray` runs as the current user, displays the tray menu and
  settings, and detects the SSID and AC/DC state;
- `HonorBatterySaver.Service` runs as a Windows service under `LocalSystem`,
  accepts a restricted command set through a local named pipe, and applies the
  profile through HONOR OEM WMI.

The application sends no network telemetry and stores no passwords, BSSIDs, or
Wi-Fi scan results.

## System requirements

- Windows 11 x64, build 22000 or later;
- a compatible HONOR laptop with HONOR PC Manager;
- the `ROOT\WMI:OemWMIMethod` OEM WMI class with the `OemWMIfun` method;
- **Microsoft .NET 10 Desktop Runtime x64**. The regular .NET Runtime is not
  sufficient.

The application uses an undocumented OEM WMI interface. BIOS, driver, or HONOR
PC Manager updates may change its behavior. Missing OEM registry keys are never
created, and an incompatible WMI signature is reported as an unsupported
device.

## Charging profiles

Only these three commands are confirmed:

| Profile | Resume charging | Stop charging | OEM payload |
|---|---:|---:|---|
| Home | 40% | 70% | `03 10 28 46` |
| Office | 70% | 90% | `03 10 46 5A` |
| Travel | 95% | 100% | `03 10 5F 64` |

Automatic mode is enabled by default:

- battery or unknown power leaves the hardware profile unchanged;
- on AC power, the first enabled rule with an exact SSID match is applied;
- an unknown network, disabled Wi-Fi, or no matching rule selects Travel.

Disconnecting AC updates the status immediately but sends no OEM command.
Connecting AC and changing Wi-Fi use a four-second debounce before selecting
and applying a profile. Startup and resume force an apply only while AC power
is available. A successful command is not repeated unless the user selects
**Synchronize profile now**.

Selecting Home, Office, or Travel from the tray menu disables automatic mode,
saves the choice, and applies it immediately. A successful change produces a
Windows notification, while the tray icon displays the active upper threshold:
`70`, `90`, or `100`. **Synchronize profile now** reapplies the selected profile
after a manual change in HONOR PC Manager. Exiting stops only the tray process;
the service stays idle and never changes a profile by itself.

At startup, the tray checks service IPC. If the registered service is stopped
or disabled, the application requests elevation, restores delayed automatic
startup, and starts it. In a development environment without an installer, it
can find `HonorBatterySaver.Service.exe` next to the tray executable or in the
build output and run it as a hidden elevated process. A second service process
is not started while one is running or still starting.

Light and dark themes follow the Windows app theme and update without restarting
the settings window.

The interface language can be set to Russian, English, or **Use Windows
language**. The choice is saved and applied without restart to the window, tray
menu, notifications, hardware diagnostics, and service responses. Unsupported
Windows display languages fall back to English (`en-US`).

HONOR PC Manager may change the profile manually. Honor Battery Saver does not
poll or continuously overwrite that choice; it applies a new decision only for
a monitored event, startup/resume, or an explicit user command.

## Wi-Fi rules

Double-click the tray icon or select **Settings and diagnostics**. Rules are
checked from top to bottom. SSIDs are matched exactly, without wildcards or
trimming leading or trailing whitespace.

**Add rule** opens an editable list ordered by connected and currently
available networks first, followed by profiles saved in Windows. Any SSID can
still be entered manually. The list is read only while opening the editor and
is not persisted by the application.

If Windows denies SSID access, open **Diagnostics** and select **Open settings**.
Location permission is used only to determine the network name. When access is
denied, the application remains functional, allows manual SSID entry, and
safely selects Travel.

Closing the window hides it without stopping the tray process. Starting the
application again activates the existing instance.

## Diagnostics

The Diagnostics tab shows:

- SSIDs and Windows permission state;
- the actual AC/DC power source;
- the selected and last successfully applied profiles;
- service and OEM WMI availability;
- current OEM registry values;
- the time and result of the last apply attempt.

Hardware diagnostics run in a separate elevated process. Before making a real
change, the dialog shows the selected profile, exact payload, and previous
registry values and requires explicit confirmation. The application never
probes unknown OEM commands.

On the verified OEM interface, the four-byte command is placed at the beginning
of a 64-byte input buffer for `ACPI\PNP0C14\HWMI_0`; all remaining bytes are
zero. This is transport framing, not an additional OEM payload.

Some provider versions declare a Boolean result but do not return a scalar
value. In that case, the confirmed OEM convention in `u8Output[0]` is used:
`0` means success, and a non-zero value means failure or an unsupported command.
When the Boolean value is present, it takes precedence.

Application files:

- user settings: `%LocalAppData%\HonorBatterySaver\settings.json`;
- service log: `%ProgramData%\HonorBatterySaver\service.log`;
- corrupt settings backup: the settings path with a `.broken.<timestamp>`
  suffix.

The service log rotates at 1 MB, keeps up to three archives, and never receives
SSID data from the tray process.

On the verified machine, HONOR PC Manager stores `PowerSafeManagerStatus` and
`PowerSafeManagerMode` as `REG_SZ`; other versions may use `DWORD`. The service
accepts both confirmed numeric representations and preserves the existing type
when synchronizing each value.

## Development

Localized strings live in
`src/HonorBatterySaver.Core/Resources/Strings.resx` (neutral `en-US`) and
`Strings.ru.resx` (`ru` satellite assembly). Do not place user-facing strings
directly in XAML or C#. XAML uses `{i18n:Localized Key}`; code uses
`Strings.Get(...)` and `Strings.Format(...)`. Keep complete formatted sentences
in resources so translations can reorder arguments.

.NET 10 SDK x64 is required. From the repository root:

```powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release
dotnet publish src/HonorBatterySaver.Tray -c Release -r win-x64 --self-contained false
dotnet publish src/HonorBatterySaver.Service -c Release -r win-x64 --self-contained false
```

Automated tests use mock WMI and registry implementations only. They never send
an OEM command or modify `HKLM\SOFTWARE\PCManager\MBAPowerManager`.

The project has been verified with .NET SDK 10.0.400 and .NET Desktop Runtime
10.0.11. Release builds complete without warnings, all automated tests pass,
and framework-dependent tray and service publications are produced for
`win-x64`. Hardware scenarios must be tested separately and sequentially on a
target laptop.

## Installation and removal

Installer development is intentionally excluded from this stage. No
`installer/` directory or Inno Setup script is included.

For a temporary development setup, publish both processes, register the service
from an elevated PowerShell using the absolute path to
`HonorBatterySaver.Service.exe`, configure delayed automatic startup, and run
the tray as the normal user. To remove the setup, exit the tray, stop and delete
the service through standard Windows tools, and then remove only the published
directory. Preserve user settings unless they should be removed explicitly.

A future production installer should verify the .NET 10 Desktop Runtime x64,
register the service with delayed automatic startup, launch the tray without
elevation, and remove the service and autostart entry cleanly. Those operations
are not implemented in this repository.

## Manual release checklist

1. With no rules, first launch selects Travel while on AC power.
2. A matching home rule on AC selects Home; disconnecting AC sends no command.
3. A matching office rule on AC selects Office; an unknown network selects
   Travel.
4. A short Wi-Fi reconnect does not produce a burst of OEM commands.
5. Sleep and resume result in one forced reevaluation.
6. Manual selection disables automatic mode and survives restart.
7. Denied location permission does not crash the application.
8. Russian, English, and Use Windows language persist and update all UI
   surfaces without restart.
9. A stopped service and an unsupported device are displayed as errors.
10. Hardware results are verified against the WMI response, registry, and HONOR
    PC Manager.

Changing the registry alone does not prove that the battery controller accepted
the thresholds. Success requires a positive OEM WMI result followed by registry
synchronization.

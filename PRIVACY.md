# Privacy policy

Last updated: 2026-09-02

Honor Battery Saver is a local Windows application. It does not include
telemetry, analytics, advertising, cloud accounts, or an application-operated
network service. The application does not transmit user data to the project
maintainers.

## Data processed locally

The tray application may read the current Wi-Fi network name (SSID), saved
Windows Wi-Fi profile names, AC/DC power state, Windows theme and language,
service status, and compatible HONOR OEM WMI and registry state. Windows may
require location permission before exposing the current SSID.

SSID values are used locally to evaluate user-created charging rules. They are
not sent to the Windows service or written to the service log. The application
does not collect Wi-Fi passwords, BSSIDs, or Wi-Fi scan history.

## Data stored locally

- User preferences and Wi-Fi rules are stored in
  `%LocalAppData%\HonorBatterySaver\settings.json`.
- Service diagnostics are stored in
  `%ProgramData%\HonorBatterySaver\service.log` with rotation at 1 MB and up to
  three archives. The service log does not contain SSIDs.
- A malformed settings file may be preserved beside the settings file with a
  `.broken.<timestamp>` suffix.

Uninstall removes the application, service, startup entry, and service logs.
User settings are deliberately retained for reinstall or upgrade. Delete the
`%LocalAppData%\HonorBatterySaver` directory manually to remove them.

## Third-party software and downloads

The application does not make network requests. Links in the application,
installer, or documentation open in the user's default browser only when
clicked. These include the project website hosted on Render, the GitHub
repository and issue tracker, the Canny feedback board, and the Microsoft
.NET Desktop Runtime download page. No settings, SSIDs, logs, or diagnostics
are attached to these links.

Visiting these pages or submitting feedback is governed by the respective
providers' privacy policies: [Render](https://render.com/privacy),
[GitHub](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement),
[Canny](https://canny.io/privacy), and
[Microsoft](https://privacy.microsoft.com/privacystatement). Public issues and
feedback may be visible to anyone; do not include personal data or sensitive
diagnostics in them.

## Contact

For a security-sensitive concern, use the private reporting process in
[SECURITY.md](./SECURITY.md). For other questions, use
[GitHub Issues](https://github.com/vyach-vasiliev/honor-battery-saver/issues) or
[Canny](https://thebestofflineapp.canny.io/honor-battery-saver-feedback).

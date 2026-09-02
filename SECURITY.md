# Security policy

## Supported versions

Security fixes are provided for the latest published release. Pre-release
builds are supported on a best-effort basis.

## Reporting a vulnerability

Please report suspected vulnerabilities privately through
[GitHub Security Advisories](https://github.com/vyach-vasiliev/honor-battery-saver/security/advisories/new).
Do not open a public issue for a vulnerability that has not yet been fixed.

Include the affected version, reproduction steps, expected impact, and any
suggested mitigation. You should receive an acknowledgement within seven days.
Please allow time for investigation and a coordinated fix before disclosing
the issue publicly.

## Security boundaries

The tray application runs as the current user. Hardware changes are delegated
to a Windows service running as `LocalSystem` over a locally restricted named
pipe. The service accepts only the three built-in, validated battery-profile
commands; arbitrary WMI payloads and registry paths are not accepted.

Hardware diagnostics require an explicit elevation prompt and confirmation.
The project does not probe unknown OEM commands.

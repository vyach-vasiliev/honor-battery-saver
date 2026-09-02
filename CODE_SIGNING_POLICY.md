# Code signing policy

## Current status

Release `v0.1.0` is intentionally published as an unsigned pre-release while
the project applies to the SignPath Foundation open-source program. Until the
application is accepted and the release workflow is integrated with SignPath,
users should verify the SHA-256 checksum published with every installer.

Free code signing provided by SignPath.io, certificate by SignPath Foundation.

The statement above describes the intended signing service. It does not claim
that currently published unsigned artifacts carry a SignPath Foundation
signature.

## Source and build provenance

- Canonical source: <https://github.com/vyach-vasiliev/honor-battery-saver>
- License: [MIT](./LICENSE)
- Privacy policy: [PRIVACY.md](./PRIVACY.md)
- Public builds run from committed GitHub Actions workflows on GitHub-hosted
  Windows runners.
- Release builds are triggered by protected version tags, restore dependencies,
  run the complete automated test suite, publish the tray and service projects,
  and compile the installer with a checksum-verified Inno Setup distribution.
- Every external GitHub Action is pinned to an immutable commit.

## Signed artifacts

After SignPath Foundation approval, signing will be limited to project-owned
Windows executables and the installer produced from this repository:

- `Honor Battery Saver.exe`
- `HonorBatterySaver.Service.exe`
- `HonorBatterySaverSetup.exe`

Third-party libraries, the .NET runtime, and unrelated binaries are outside the
signing scope. A release will require an explicit approval in SignPath before
signed artifacts are published.

## Project roles

- Committer: [@vyach-vasiliev](https://github.com/vyach-vasiliev)
- Reviewer: project collaborators who review non-owner pull requests before
  merge
- Release approver: [@vyach-vasiliev](https://github.com/vyach-vasiliev)

Role assignments will be kept aligned between GitHub and SignPath. Accounts
with commit, review, or approval privileges must use multi-factor
authentication.

## Release verification

Before code signing is available, compare the downloaded installer's SHA-256
digest with `HonorBatterySaverSetup.exe.sha256` from the same GitHub release.
After integration, also verify the Windows signature and that the publisher is
`SignPath Foundation`.

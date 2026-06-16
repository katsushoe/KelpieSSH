# KelpieSSH MSI Installer

KelpieSSH uses WiX Toolset to build a per-user MSI installer.

The MSI is intended to install files under:

```text
%LocalAppData%\Programs\KelpieSSH
```

This keeps `config`, `profiles`, `keys`, `dat`, and `logs` writable by the current user. Installing under `Program Files` is not the default because `kelpie init` creates and updates files under the Kelpie home directory.

## Prerequisites

Install the WiX CLI:

```powershell
dotnet tool install --global wix --version 6.*
```

## Build

From the repository root:

```powershell
.\scripts\Build-Msi.ps1
```

The script publishes the three executable entry points and builds:

```text
.artifacts\msi\KelpieSSH-<version>-x64.msi
```

The installer registers the installed `bin` directory in the current user's `PATH`.

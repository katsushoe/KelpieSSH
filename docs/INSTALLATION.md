# Windows Command Installation

For Japanese documentation, see [INSTALLATION.ja.md](ja/INSTALLATION.ja.md).

This guide makes `kelpie` and `kelpiemcp` available from any working directory for the current Windows user. The ZIP installer and the generated MSI register a user-level `PATH` entry. The installed `bin` directory contains both commands; the MCP server remains under `bin/mcp`.

## Prerequisites

Use a Windows x64 package from the release you intend to install. The current build scripts publish framework-dependent .NET 8 applications. These packages require the .NET 8 runtime and ASP.NET Core 8 runtime for Windows x64. Installing a newer major runtime alone does not supply those .NET 8 frameworks. The packaged `*.runtimeconfig.json` files specify the required frameworks.

If .NET is installed, inspect the available runtimes:

```powershell
dotnet --list-runtimes
```

For the current framework-dependent package, look for both `Microsoft.NETCore.App 8.x` and `Microsoft.AspNetCore.App 8.x`. This runtime prerequisite is separate from adding the commands to `PATH`.

## Install the Payload

### ZIP installer

Extract `KelpieSSH-<version>-win-x64.zip` to a temporary directory. Open PowerShell in the extracted directory containing `Install-Kelpie.ps1` and `bin`, then run:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-Kelpie.ps1
```

The default destination is `%LOCALAPPDATA%\Programs\KelpieSSH`. The script copies program files, samples and documentation, registers `<destination>\bin` in the current user's persistent `PATH`, and verifies `kelpie version`. It does not initialize an SSH profile or start the MCP server. It rejects an incomplete payload, the same source/destination directory, and detected Kelpie processes running from the destination.

To choose another destination, use a quoted path:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-Kelpie.ps1 -InstallDirectory 'C:\Tools\KelpieSSH'
```

To copy the payload without modifying the user `PATH`:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-Kelpie.ps1 -InstallDirectory 'C:\Tools\KelpieSSH' -NoPath
```

With `-NoPath`, invoke the executables by their full path or add that installation's `bin` directory through Windows user environment settings. Add `bin`, rather than `bin/mcp` or the installation root.

### MSI package

If the release provides an MSI, run that release's installer. The current WiX definition installs per user under `%LOCALAPPDATA%\Programs\KelpieSSH` and registers its `bin` directory in the user `PATH`. Use Windows Installed apps to manage that MSI installation. Installing an MSI does not make the commands available to other Windows accounts.

## Confirm Command Resolution

Close and reopen the terminal application after installation. A new tab in an already-running terminal or IDE can inherit its old environment. Also restart GUI or AI clients that will launch the commands.

Set the following variable to the destination actually selected during installation:

```powershell
$installRoot = Join-Path $env:LOCALAPPDATA 'Programs\KelpieSSH'
$binPath = Join-Path $installRoot 'bin'
Test-Path (Join-Path $binPath 'kelpie.exe')
Test-Path (Join-Path $binPath 'kelpiemcp.exe')
Test-Path (Join-Path $binPath 'mcp\KelpieMCPServer.exe')
```

All three checks should return `True`. For a custom destination, replace the first assignment, for example with `$installRoot = 'C:\Tools\KelpieSSH'`.

First check the chosen binaries directly:

```powershell
& (Join-Path $binPath 'kelpie.exe') version
& (Join-Path $binPath 'kelpiemcp.exe') version
```

Then check which commands the shell resolves:

```powershell
Get-Command kelpie, kelpiemcp -All | Select-Object CommandType, Name, Source, Path
where.exe kelpie
where.exe kelpiemcp
kelpie version
kelpiemcp version
```

Both application commands should resolve to the chosen `bin` directory. The two products have independent versions, so their version numbers need not be equal. `Get-Command -All` also exposes aliases or functions that can hide an executable.

To inspect the stored user `PATH` without changing it:

```powershell
[Environment]::GetEnvironmentVariable('Path', 'User') -split ';'
```

For an intentional one-session selection before restarting the terminal, prepend the chosen directory to this PowerShell process only:

```powershell
$env:Path = $binPath + [IO.Path]::PathSeparator + $env:Path
```

This last command does not persist a user or machine environment change. Repeat the resolution checks afterward.

## Initialize Kelpie Home

For a new installation, initialize and validate local settings before connecting:

```powershell
kelpie init
kelpie config check
```

Kelpie home is derived from the installed command directory, so changing the shell's working directory does not select another home. With the standard layout, the home is the parent of `bin`. Keep `config`, `profiles`, `keys`, `dat` and `logs` under that home. Explicit directory overrides are described in [CLI_OPTIONS.md](../CLI_OPTIONS.md).

To create and validate a named profile, follow [PROFILE_GUIDE.md](../PROFILE_GUIDE.md). `kelpie config check` validates local configuration without opening SSH. MCP client setup and server startup are covered in [MCP_GUIDE.md](../MCP_GUIDE.md).

## Update or Change Location

For a ZIP installation, stop processes using the destination and rerun the installer from a separately extracted new payload with the same `-InstallDirectory`. The script copies program/documentation files and does not remove existing `config`, `profiles`, `keys`, `dat` or `logs`. Existing files not present in the new payload are not automatically removed.

If the MCP server runs as a Windows Service, stop it through its service-management workflow before updating, then start it again in the same mode. The ZIP installer does not stop or reconfigure the service. See the service commands in [COMMANDS.md](../COMMANDS.md).

Selecting a different installation directory creates a different home. It does not migrate user data or remove the old directory's `PATH` entry. Back up and deliberately migrate the required configuration, profiles, keys and persistent data; check all resolved executable paths before connecting. Do not mix CLI files from one installation with an MCP server from another.

## Remove an Installation

For an MSI installation, use Windows Installed apps. The generated MSI owns its registered user `PATH` entry and is configured to remove that entry on uninstall.

For a ZIP installation, stop its processes, preserve the user data you need, and remove only that installation's exact `bin` entry from the Windows user `PATH`. Remove its program/documentation files when no longer needed. Deleting the entire home also deletes its profiles, keys and persistent data. The ZIP installer does not provide an uninstall switch.

Reopen terminals and clients, then use `Get-Command -All` and `where.exe` to confirm that command resolution matches the installations you kept.

## Troubleshooting

| Symptom | Check and action |
| :--- | :--- |
| Full-path `version` works, but the command name is not found | Check the stored user `PATH`, the selected `bin` directory and the terminal/client's inherited environment. |
| An older version runs | Inspect all results from `Get-Command -All` and `where.exe`; resolve the old installation's entry or a shadowing alias/function. |
| The executable asks to install or update .NET | Compare its `runtimeconfig.json` with `dotnet --list-runtimes`; supply the matching .NET 8 and ASP.NET Core 8 frameworks for the current package. |
| The installer reports a running process or a locked file | Stop the processes belonging to that destination, including its Windows Service when applicable, and retry the same installation. |
| Commands work in a terminal but not in a GUI client | Restart the client or use the installed absolute executable path in its configuration. |
| Profiles seem missing after changing location | Confirm the resolved executable's directory and derived Kelpie home before initializing or connecting. |

Package layout and dependencies are documented in [PACKAGES.md](../PACKAGES.md).

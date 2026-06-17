# KelpieSSH Commands

Last updated: 2026-06-17

This file is the English command reference for commands run directly from a terminal, such as `kelpie` and `kelpiemcp`.
For Japanese documentation, see [docs/ja/COMMANDS.ja.md](docs/ja/COMMANDS.ja.md).
For MCP callable tool details, see [MCP_COMMANDS.md](MCP_COMMANDS.md).

## Command Groups

| Group | Commands | Purpose |
| :--- | :--- | :--- |
| MCP server control | `kelpiemcp start`, `kelpiemcp stop`, `kelpiemcp status` | Start, stop, and inspect `KelpieMCPServer`. |
| MCP Windows Service | `kelpiemcp service register`, `kelpiemcp service unregister`, `kelpiemcp service status` | Register, unregister, and inspect the Windows Service entry. |
| MCP password session | `kelpiemcp password`, `kelpiemcp forget` | Store or clear an SSH password in the running MCP server session. |
| Initialization | `kelpie init [profile]` | Create the local Kelpie home directory layout and sample configuration files. |
| Profile/session | `kelpie open`, `kelpie login`, `kelpie logout`, `kelpie profiles`, `kelpie sessions`, `kelpie kill` | Select profiles and manage interactive SSH sessions. |
| Mode/UI | `kelpie gui`, `kelpie cli`, `kelpie login --console`, `kelpie login --desktop` | Switch CLI/GUI mode or choose a temporary launch mode. |
| Diagnostics | `kelpie profile show`, `kelpie status`, `kelpie diag`, `kelpie logs` | Show profile information, MCP server status, SSH diagnostics, and service logs. |
| Environment | `kelpie env keys`, `kelpie env peek`, `kelpie env set`, `kelpie env list`, `kelpie env persist`, `kelpie env remove` | List, read, temporarily set, or persist remote environment variables under profile policy. |
| Help/version | `kelpie version`, `kelpie help`, `kelpie --help`, `kelpie --version` | Show version and help text. |

## Common Rules

- Commands read configuration from the Kelpie home directory created by `kelpie init`.
- `kelpie` reads `config/kelpie.json`.
- `kelpiemcp` and `KelpieMCPServer` read `config/kelpiemcp.json`.
- SSH profile files are stored under `profiles/`.
- Secrets, private keys, real host names, real user names, and production profiles must not be committed.
- Direct `root` SSH login is not allowed.

## Commands

### `kelpie init [profile]`

Creates the local Kelpie home directory layout.

Syntax:

```powershell
kelpie init
kelpie init vps01
```

When a profile name is supplied, a named sample profile is created under `profiles/<profile>.json`.
Existing configuration files are not overwritten.

### `kelpie version`

Shows the `kelpie` command version.

```powershell
kelpie version
kelpie --version
```

### `kelpie help`

Shows command help.

```powershell
kelpie help
kelpie --help
```

### `kelpie profiles`

Lists configured SSH profiles.

```powershell
kelpie profiles
```

### `kelpie profile show <profile>`

Shows a sanitized profile summary.
Secret values are not printed.

```powershell
kelpie profile show vps01
```

### `kelpie open <profile>`

Stores the selected profile name in local runtime state for later commands that use the open profile.

```powershell
kelpie open vps01
```

### `kelpie status <profile>`

Shows MCP server status and a sanitized profile summary.

```powershell
kelpie status vps01
```

### `kelpie diag <profile>`

Runs high-level read-oriented diagnostics over SSH.

```powershell
kelpie diag vps01
```

This command requires a reachable SSH target and valid authentication.
For password profiles, the CLI prompts for the password once and reuses it for all diagnostic commands in the current `kelpie diag` process.

### `kelpie logs <profile> <service> [lines]`

Reads recent logs for a systemd service over SSH.

```powershell
kelpie logs vps01 nginx.service
kelpie logs vps01 nginx.service 200
```

The service name and line count are validated before command execution.
For password profiles, the CLI prompts for the password for the current `kelpie logs` process.

### `kelpie env keys <profile>`

Lists remote environment variable names visible to the selected SSH user.

```powershell
kelpie env keys vps01
```

This command requires `AllowPeekEnvironmentKeys` in the profile `Capabilities`.
Keys marked `Hidden` in `EnvironmentValues` are filtered from the output.
Values are never printed by this command.

Example:

```text
HOME
LANG
PATH
SHELL
```

### `kelpie env peek <profile> <key>`

Reads one remote environment variable value when the profile permits it.

```powershell
kelpie env peek vps01 PATH
```

This command requires `AllowPeekEnvironmentValues` in `Capabilities`.
The requested key must be listed in `EnvironmentValues` with `PeekCommon`, `PeekSecret`, or `Masked`.
`Masked` returns a masked value and length only.
`Hidden` and unlisted keys cannot be read.

Masked example:

```text
************ (length=12)
```

### `kelpie env set <profile> <key> <value> -- <command>`

Runs one command with one environment variable value set for that execution only.
Before running the command, Kelpie sources `~/.kelpie/.env` if it exists.
The `<key> <value>` pair then overrides that environment for the single command execution only.
It does not persist the new value on the remote host.

```powershell
kelpie env set vps01 APP_ENV production -- printenv APP_ENV
```

This command requires `AllowSetEnvironmentValues` in `Capabilities`.
The requested key must be listed in `EnvironmentValues` with `SetCommon` or `SetSecret`.
The command after `--` is checked by the same CLI raw-command policy used by `kelpie login`.
Environment variable values must not be pasted into public logs or issues.

### `kelpie env list <profile>`

Lists environment variable keys stored in the remote Kelpie env file.

```powershell
kelpie env list vps01
```

The remote file is:

```text
~/.kelpie/.env
```

This command requires `AllowPeekEnvironmentKeys` in `Capabilities`.
Keys marked `Hidden` in `EnvironmentValues` are filtered from the output.
Values are never printed by this command.

### `kelpie env persist <profile> <key> <value>`

Writes one environment variable value to the remote Kelpie env file.

```powershell
kelpie env persist vps01 APP_ENV production
```

Kelpie writes the value to:

```text
~/.kelpie/.env
```

Before writing, Kelpie creates a timestamped backup such as:

```text
~/.kelpie/.env.20260617T120000Z.kelpie
```

This command requires `AllowSetEnvironmentValues` in `Capabilities`.
The requested key must be listed in `EnvironmentValues` with `SetCommon` or `SetSecret`.
The generated file is intended to be sourced by shells, cron jobs, or Kelpie-managed executions.
Existing processes are not updated automatically.

### `kelpie env remove <profile> <key>`

Removes one environment variable from the remote Kelpie env file.

```powershell
kelpie env remove vps01 APP_ENV
```

Before writing, Kelpie creates a timestamped `.kelpie` backup.
This command requires `AllowSetEnvironmentValues` in `Capabilities`.
The requested key must be listed in `EnvironmentValues` with `SetCommon` or `SetSecret`.

### `kelpie cli`

Switches Kelpie to CLI mode.

```powershell
kelpie cli
```

### `kelpie gui`

Starts or switches to GUI mode when a GUI frontend is available.

```powershell
kelpie gui
```

### `kelpiemcp start`

Starts the local MCP server.
On Windows, if `KelpieMCPServer` is registered as a Windows Service, this command starts the Windows Service. Run it from a terminal running as administrator in that case.
Otherwise, it starts a normal local process.

```powershell
kelpiemcp start
```

Example when the Windows Service is registered:

```text
Windows Service start requested: KelpieMCPServer
```

### `kelpiemcp stop`

Stops the local MCP server process.

```powershell
kelpiemcp stop
```

### `kelpiemcp status`

Shows whether the local MCP server is running. The output also shows whether `KelpieMCPServer` is registered as a Windows Service.

```powershell
kelpiemcp status
```

Example:

```text
KelpieMCPServer: running
MCP URL: http://127.0.0.1:45432/mcp
Health URL: http://127.0.0.1:45432/health
Control pipe: KelpieMCPServer.Control
Registered as Windows service: yes
```

Stopped example:

```text
KelpieMCPServer: stopped
Registered as Windows service: yes
```

### `kelpiemcp service register`

Registers `KelpieMCPServer` as an automatic-start Windows Service and sets its service description. Run from a terminal running as administrator.

```powershell
kelpiemcp service register
```

### `kelpiemcp service unregister`

Unregisters the `KelpieMCPServer` Windows Service. Stop the service before unregistering it. Run from a terminal running as administrator.

```powershell
kelpiemcp service unregister
```

### `kelpiemcp service status`

Shows whether the `KelpieMCPServer` Windows Service is registered.

```powershell
kelpiemcp service status
```

### `kelpiemcp password <profile>`

Prompts for a password and stores it only in the running `KelpieMCPServer` process memory.

```powershell
kelpiemcp password vps01
```

Passwords must not be stored in JSON files.

### `kelpiemcp forget <profile>`

Clears the in-memory password session for a profile.

```powershell
kelpiemcp forget vps01
```

## Safety Notes

- KelpieSSH starts from read-oriented diagnostics and allow-listed commands.
- Dangerous operations require dedicated commands, policy checks, and confirmation strings.
- Passwords are session-only for the MCP server process.
- Production profile files and private keys must stay outside the public repository.

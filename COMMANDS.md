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

## Return Value Specification

Terminal commands return information through the process exit code, standard output, and standard error. They do not return JSON directly; JSON samples in this section are documentation objects that represent the process return values.

Unless a command section states a more specific contract:

- exit code `0` means the requested command completed successfully;
- a non-zero exit code means validation failed, a required local service was unavailable, an SSH operation failed, a Windows Service operation failed, or the command was rejected by policy;
- standard output contains the user-facing result shown in the command section's result sample;
- standard error contains error messages intended for an interactive terminal user;
- secrets, private keys, passphrases, and raw password values are not valid return values and must not be printed.

Result samples in this document are representative output shapes. Host names, profile names, URLs, service names, paths, handles, and timestamps are examples unless they are fixed product strings.

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

Return value:

- Exit code `0` when initialization completes.
- Standard output describes created and existing directories/files. The structured internal result is `KelpieHomeInitializationResult` with `HomeDirectory`, `ProfileName`, `CreatedDirectories`, `CreatedFiles`, and `ExistingFiles`.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
```

### `kelpie version`

Shows the `kelpie` command version.

```powershell
kelpie version
kelpie --version
```

Return value:

- Exit code `0` when the version is printed.
- Standard output contains the `kelpie` product version string.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
```

### `kelpie help`

Shows command help.

```powershell
kelpie help
kelpie --help
```

Return value:

- Exit code `0` when help text is printed.
- Standard output contains terminal help text for the available command set.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
```

### `kelpie profiles`

Lists configured SSH profiles.

```powershell
kelpie profiles
```

Return value:

- Exit code `0` when the profile list is read.
- Standard output contains configured profile names and sanitized summary information only.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
```

### `kelpie profile show <profile>`

Shows a sanitized profile summary.
Secret values are not printed.

```powershell
kelpie profile show vps01
```

Return value:

- Exit code `0` when the profile exists and the sanitized summary is printed.
- Standard output contains profile metadata safe for terminal display. Secret values are not returned.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
```

### `kelpie open <profile>`

Stores the selected profile name in local runtime state for later commands that use the open profile.

```powershell
kelpie open vps01
```

Return value:

- Exit code `0` when the profile selection is saved.
- Standard output confirms the selected profile.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
```

### `kelpie status <profile>`

Shows MCP server status and a sanitized profile summary.

```powershell
kelpie status vps01
```

Return value:

- Exit code `0` when status information is collected.
- Standard output contains MCP server status and sanitized profile information.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
```

### `kelpie diag <profile>`

Runs high-level read-oriented diagnostics over SSH.

```powershell
kelpie diag vps01
```

This command requires a reachable SSH target and valid authentication.
For password profiles, the CLI prompts for the password once and reuses it for all diagnostic commands in the current `kelpie diag` process.

Return value:

- Exit code `0` when all required diagnostic steps complete successfully.
- Standard output contains read-only diagnostic summaries returned by allowed SSH commands.
- Standard error contains SSH or policy errors if the diagnostic run fails.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
```

### `kelpie logs <profile> <service> [lines]`

Reads recent logs for a systemd service over SSH.

```powershell
kelpie logs vps01 nginx.service
kelpie logs vps01 nginx.service 200
```

The service name and line count are validated before command execution.
For password profiles, the CLI prompts for the password for the current `kelpie logs` process.

Return value:

- Exit code `0` when the log command completes successfully.
- Standard output contains the bounded log output returned by the allowed SSH command.
- Standard error contains validation, SSH, policy, or remote command errors.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
```

### `kelpie env keys <profile>`

Lists remote environment variable names visible to the selected SSH user.

```powershell
kelpie env keys vps01
```

This command requires `AllowPeekEnvironmentKeys` in the profile `Capabilities`.
Keys marked `Hidden` in `EnvironmentValues` are filtered from the output.
Values are never printed by this command.

Return value:

- Exit code `0` when visible keys are listed.
- Standard output contains key names only, one per line.
- Hidden keys and values are not returned.

Example:

```text
HOME
LANG
PATH
SHELL
```

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
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

Return value:

- Exit code `0` when the key is readable under profile policy.
- Standard output contains either the value allowed by policy or a masked value with length.
- Hidden keys, unlisted keys, and policy-denied reads fail without returning the raw value.

Masked example:

```text
************ (length=12)
```

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
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

Return value:

- Exit code `0` when the allowed command runs with the temporary environment value.
- Standard output and standard error are the bounded output streams from the allowed remote command.
- The provided environment value is not a persisted return value.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
```

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

Return value:

- Exit code `0` when the remote Kelpie env file is read or treated as empty.
- Standard output contains persisted key names only.
- Values and hidden keys are not returned.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
```

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

Return value:

- Exit code `0` when the value is written to the remote Kelpie env file.
- Standard output confirms the persisted key and backup/write operation without printing secret values.
- Standard error contains validation, policy, SSH, or remote write errors.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
```

### `kelpie env remove <profile> <key>`

Removes one environment variable from the remote Kelpie env file.

```powershell
kelpie env remove vps01 APP_ENV
```

Before writing, Kelpie creates a timestamped `.kelpie` backup.
This command requires `AllowSetEnvironmentValues` in `Capabilities`.
The requested key must be listed in `EnvironmentValues` with `SetCommon` or `SetSecret`.

Return value:

- Exit code `0` when the key is removed or the remote env file is updated successfully.
- Standard output confirms the removed key and backup/write operation without printing secret values.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
```

### `kelpie cli`

Switches Kelpie to CLI mode.

```powershell
kelpie cli
```

Return value:

- Exit code `0` when CLI mode is selected.
- Standard output confirms the mode change.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
```

### `kelpie gui`

Starts or switches to GUI mode when a GUI frontend is available.

```powershell
kelpie gui
```

Return value:

- Exit code `0` when GUI mode is selected or the GUI frontend is started.
- Standard output contains a user-facing status message.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
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

Return value:

- Exit code `0` when the start request is accepted.
- Standard output reports whether a Windows Service start was requested or a local process was started.
- Standard error contains startup failures, including service-control failures.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
```

### `kelpiemcp stop`

Stops the local MCP server process.

```powershell
kelpiemcp stop
```

Return value:

- Exit code `0` when the stop request is sent successfully.
- Standard output contains a stop confirmation when available.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
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

Return value:

- Exit code `0` when status is printed.
- Standard output contains MCP process status, endpoint URLs when running, control pipe name when available, and Windows Service registration state.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
```

### `kelpiemcp service register`

Registers `KelpieMCPServer` as an automatic-start Windows Service and sets its service description. Run from a terminal running as administrator.

```powershell
kelpiemcp service register
```

Return value:

- Exit code `0` when Windows Service registration succeeds.
- Standard output contains the service-control result.
- Standard error contains Windows Service registration errors.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
```

### `kelpiemcp service unregister`

Unregisters the `KelpieMCPServer` Windows Service. Stop the service before unregistering it. Run from a terminal running as administrator.

```powershell
kelpiemcp service unregister
```

Return value:

- Exit code `0` when Windows Service unregistration succeeds.
- Standard output contains the service-control result.
- Standard error contains Windows Service unregistration errors.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
```

### `kelpiemcp service status`

Shows whether the `KelpieMCPServer` Windows Service is registered.

```powershell
kelpiemcp service status
```

Return value:

- Exit code `0` when service status is printed.
- Standard output reports whether the Windows Service is registered.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
```

### `kelpiemcp password <profile>`

Prompts for a password and stores it only in the running `KelpieMCPServer` process memory.

```powershell
kelpiemcp password vps01
```

Passwords must not be stored in JSON files.

Return value:

- Exit code `0` when the password is accepted by the running MCP server session.
- Standard output confirms that a password session was stored.
- The password itself is never returned.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
```

### `kelpiemcp forget <profile>`

Clears the in-memory password session for a profile.

```powershell
kelpiemcp forget vps01
```

Return value:

- Exit code `0` when the running MCP server clears or accepts the clear request for the profile.
- Standard output confirms the password session cleanup.
- The previous password value is never returned.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
```

## Safety Notes

- KelpieSSH starts from read-oriented diagnostics and allow-listed commands.
- Dangerous operations require dedicated commands, policy checks, and confirmation strings.
- Passwords are session-only for the MCP server process.
- Production profile files and private keys must stay outside the public repository.

# KelpieSSH Commands

Last updated: 2026-06-28

This file is the English command reference for commands run directly from a terminal, such as `kelpie` and `kelpiemcp`.
For Japanese documentation, see [docs/ja/COMMANDS.ja.md](docs/ja/COMMANDS.ja.md).
For command-line option details, see [CLI_OPTIONS.md](CLI_OPTIONS.md).
For MCP callable tool details, see [MCP_COMMANDS.md](MCP_COMMANDS.md).

## Command Groups

| Group | Commands | Purpose |
| :--- | :--- | :--- |
| [MCP server control](#mcp-server-control) | `kelpiemcp start [--reload-config]`, `kelpiemcp stop`, `kelpiemcp status` | Start, stop, and inspect `KelpieMCPServer`. |
| [MCP profile trust](#mcp-profile-trust) | `kelpiemcp profile add <profile>`, `kelpiemcp profile reload <profile>`, `kelpiemcp profile revoke <profile>`, `kelpiemcp profile-capabilities [profile]` | Add, reload, revoke, and inspect trusted SSH profile baselines. |
| [MCP Windows Service](#mcp-windows-service) | `kelpiemcp service register`, `kelpiemcp service unregister`, `kelpiemcp service status` | Register, unregister, and inspect the Windows Service entry. |
| [MCP password session](#mcp-password-session) | `kelpiemcp password`, `kelpiemcp forget`, `kelpiemcp login`, `kelpiemcp logout` | Store or clear an SSH password in the running MCP server session. |
| [Initialization](#initialization) | `kelpie init [--silent] [profile]`, `kelpie config --check` | Create and validate the local Kelpie home configuration. |
| [Profile/session](#profilesession) | `kelpie profile create`, `kelpie profile edit`, `kelpie profile delete`, `kelpie profile clean`, `kelpie profile commit`, `kelpie profile rollback`, `kelpie open`, `kelpie login`, `kelpie logout`, `kelpie profiles`, `kelpie sessions`, `kelpie kill` | Create, edit, and delete profile templates, select profiles, and manage interactive SSH sessions. |
| [Mode/UI](#modeui) | `kelpie gui`, `kelpie cli`, `kelpie login --console`, `kelpie login --desktop` | Switch CLI/GUI mode or choose a temporary launch mode. |
| [Diagnostics](#diagnostics) | `kelpie profile check`, `kelpie profile show`, `kelpie status`, `kelpie diag`, `kelpie inventory`, `kelpie logs` | Validate profiles, show profile information, MCP server status, SSH diagnostics, target inventory, and service logs. |
| [Packages](#packages) | `kelpie pkg check-updates`, `kelpie pkg info`, `kelpie pkg search`, `kelpie pkg list-installed`, `kelpie pkg simulate-install`, `kelpie pkg simulate-remove`, `kelpie pkg install`, `kelpie pkg remove` | Inspect packages and run confirmation-gated package operations through the selected SSH profile. |
| [Environment](#environment) | `kelpie env keys`, `kelpie env peek`, `kelpie env set`, `kelpie env list`, `kelpie env persist`, `kelpie env remove` | List, read, temporarily set, or persist remote environment variables under profile policy. |
| [Help/version](#helpversion) | `kelpie version`, `kelpie help`, `kelpie --help`, `kelpie --version`, `kelpiemcp version`, `kelpiemcp help` | Show version and help text. |

## Common Rules

- Commands read configuration from the Kelpie home directory created by `kelpie init`.
- `kelpie` reads `config/kelpie.json`.
- `kelpiemcp` and `KelpieMCPServer` read `config/kelpiemcp.json`.
- SSH profile files are stored under `profiles/`.
- Secrets, private keys, real host names, real user names, and production profiles must not be committed.
- Direct `root` SSH login is not allowed.

## Command-Line Options

Runtime directory overrides, dry-run previews, silent mode, and profile transaction options are documented in [CLI_OPTIONS.md](CLI_OPTIONS.md).

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

Commands are grouped by operational area. Each group section states the scope, and each command is documented in its own subsection.

### MCP server control

Start, stop, and inspect the local KelpieMCPServer process or service.

Commands in this group:

- [`kelpiemcp start`](#kelpiemcp-start)
- [`kelpiemcp stop`](#kelpiemcp-stop)
- [`kelpiemcp status`](#kelpiemcp-status)

#### `kelpiemcp start`

Starts the local MCP server.
On Windows, if `KelpieMCPServer` is registered as a Windows Service, this command starts the Windows Service. Run it from a terminal running as administrator in that case.
Otherwise, it starts a normal local process.
During startup, `KelpieMCPServer` verifies the MCP server configuration file and SSH profile file hashes against the protected trust store.

```powershell
kelpiemcp start [--reload-config]
```

Arguments:

| Argument | Required | Description |
| :--- | :---: | :--- |
| `--reload-config` | no | Explicitly accepts the current `config/kelpiemcp.json` content and updates the trust-store baseline hash for future starts. Use only after verifying the configuration change is intentional. |

Example after intentionally editing MCP server configuration:

```powershell
kelpiemcp start --reload-config
```

Example when the Windows Service is registered:

```text
Windows Service start requested: KelpieMCPServer
```

Return value:

- Exit code `0` when the start request is accepted.
- Standard output reports whether a Windows Service start was requested, a local process was started, or the MCP server was already running.
- Standard error contains startup failures, including service-control failures.
- If the protected trust store cannot be decrypted or authenticated, the MCP server startup fails. The user must inspect `kelpiemcp.json` and all profile files, move or delete the trust store, and start again. Deleting the trust store makes the current configuration and every existing profile a new trusted baseline on the next successful start.
- If the `kelpiemcp.json` hash differs from the trust store during normal startup, the MCP server startup fails.
- If one profile hash differs from the trust store during normal startup, that profile is not loaded. Other profiles may continue to load.
- `--reload-config` accepts the current MCP server configuration as the new trusted baseline.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "KelpieMCPServer start requested.",
  "stderr": ""
}
```

Execution result sample:

```text
KelpieMCPServer start requested.
```

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.
- Use `--reload-config` only after verifying that the edited MCP server configuration is intentional and safe.
- Use `kelpiemcp profile reload <profile>` only after verifying that the edited profile file is intentional and safe.
- On shared PCs, third-party-operated terminals, or VPS-hosted deployments, restrict OS permissions for `kelpiemcp`, `kelpiemcp.json`, profile JSON files, and `mcp_trusted_store.dat` to administrators or the operations group for stronger protection.

#### `kelpiemcp stop`

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

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

#### `kelpiemcp status`

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

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

### MCP profile trust

Add, reload, revoke, and inspect trusted SSH profile baselines in the MCP trust store.

Commands in this group:

- [`kelpiemcp profile add <profile>`](#kelpiemcp-profile-add-profile)
- [`kelpiemcp profile reload <profile>`](#kelpiemcp-profile-reload-profile)
- [`kelpiemcp profile revoke <profile>`](#kelpiemcp-profile-revoke-profile)
- [`kelpiemcp profile-capabilities [profile]`](#kelpiemcp-profile-capabilities-profile)

#### `kelpiemcp profile add <profile>`

Adds a new SSH profile JSON file to the trusted MCP store.

```powershell
kelpiemcp profile add vps02
```

Arguments:

| Argument | Required | Description |
| :--- | :---: | :--- |
| `<profile>` | yes | SSH profile name. The file must exist as `profiles/<profile>.json` and must be valid JSON for a Kelpie SSH profile. |

Processing:

- When `KelpieMCPServer` is running, the request is sent to the running server. The server validates the profile, stores the profile hash, and reloads the in-memory catalog.
- When `KelpieMCPServer` is not running, `kelpiemcp` validates the profile and updates `dat/mcp_trusted_store.dat`. The profile is loaded the next time the MCP server starts.
- The operation is denied when `ProfileOperations:Add:CLI` is `Deny`.

Return value:

- Exit code `0` when the profile is trusted.
- Non-zero exit code when the profile name is missing, the profile file is missing, the JSON is invalid, trust is disabled, `ProfileOperations:Add:CLI` is `Deny`, or the profile is already trusted.
- Standard output is a JSON `SshProfileTrustOperationResult` with `Success`, `ProfileName`, `Status`, and `Message`.

Return value sample:

```json
{
  "Success": true,
  "ProfileName": "vps02",
  "Status": "add",
  "Message": ""
}
```

Execution result sample:

```text
{"Success":true,"ProfileName":"vps02","Status":"add","Message":""}
```

Safety notes:

- Use only after verifying that the new profile file is intentional and safe.

#### `kelpiemcp profile reload <profile>`

Accepts an intentionally edited SSH profile JSON file as the new trusted baseline.

```powershell
kelpiemcp profile reload vps01
```

Arguments:

| Argument | Required | Description |
| :--- | :---: | :--- |
| `<profile>` | yes | SSH profile name. The profile must already be trusted and the current JSON must be valid. |

Processing:

- When `KelpieMCPServer` is running, the server validates the profile, updates the trusted hash, and reloads the in-memory catalog.
- When `KelpieMCPServer` is not running, `kelpiemcp` validates the profile and updates `dat/mcp_trusted_store.dat`. The edited profile is loaded the next time the MCP server starts.
- The operation is denied when `ProfileOperations:Reload:CLI` is `Deny`.

Return value:

- Exit code `0` when the edited profile is accepted.
- Non-zero exit code when the profile is missing, not trusted, invalid, `ProfileOperations:Reload:CLI` is `Deny`, or cannot be written to the trust store.
- Standard output is a JSON `SshProfileTrustOperationResult`.

Return value sample:

```json
{
  "Success": true,
  "ProfileName": "vps01",
  "Status": "reload",
  "Message": ""
}
```

Execution result sample:

```text
{"Success":true,"ProfileName":"vps01","Status":"reload","Message":""}
```

Safety notes:

- Use only after verifying that the profile change is legitimate. This command makes the current file content trusted.

#### `kelpiemcp profile revoke <profile>`

Removes one profile hash from the trusted MCP store.

```powershell
kelpiemcp profile revoke vps01
```

Arguments:

| Argument | Required | Description |
| :--- | :---: | :--- |
| `<profile>` | yes | SSH profile name to remove from the trusted profile list. |

Processing:

- When `KelpieMCPServer` is running, the server removes the trusted hash and reloads the in-memory catalog.
- When `KelpieMCPServer` is not running, `kelpiemcp` removes the profile entry from `dat/mcp_trusted_store.dat`.
- The operation is denied when `ProfileOperations:Revoke:CLI` is `Deny`.

Return value:

- Exit code `0` when the trusted entry is removed.
- Non-zero exit code when the profile name is missing, trust is disabled, `ProfileOperations:Revoke:CLI` is `Deny`, or the profile is not trusted.
- Standard output is a JSON `SshProfileTrustOperationResult`.

Return value sample:

```json
{
  "Success": true,
  "ProfileName": "vps01",
  "Status": "revoked",
  "Message": ""
}
```

Execution result sample:

```text
{"Success":true,"ProfileName":"vps01","Status":"revoked","Message":""}
```

Safety notes:

- A revoked profile is not loaded by normal MCP server startup until it is added again.

#### `kelpiemcp profile-capabilities [profile]`

Shows whether profile trust operations are currently possible for a profile.

```powershell
kelpiemcp profile-capabilities vps01
kelpiemcp profile-capabilities
```

Arguments:

| Argument | Required | Description |
| :--- | :---: | :--- |
| `[profile]` | no | SSH profile name. If omitted, `kelpiemcp` uses the profile currently opened by `kelpie open <profile>` when available. |

Processing:

The command checks the profile file, trust store, and `ProfileOperations:*:CLI` settings. It does not contact the SSH target.

Return value:

- Exit code `0` when capabilities are printed.
- Non-zero exit code when no profile is supplied and no open profile is available.
- Standard output is a JSON `SshProfileTrustCapabilities` with `ProfileName`, `AddAllowed`, `ReloadAllowed`, `RevokeAllowed`, and `Reason`.
- `AddAllowed`, `ReloadAllowed`, and `RevokeAllowed` are `true` only when both the trust-store state and the corresponding `ProfileOperations:*:CLI` setting allow the operation.

Return value sample:

```json
{
  "ProfileName": "vps01",
  "AddAllowed": false,
  "ReloadAllowed": true,
  "RevokeAllowed": true,
  "Reason": ""
}
```

Execution result sample:

```text
{"ProfileName":"vps01","AddAllowed":false,"ReloadAllowed":true,"RevokeAllowed":true,"Reason":""}
```

Safety notes:

- This is a local read-only command. It may reveal profile names, but it does not print secrets or profile file contents.

### MCP Windows Service

Register, unregister, and inspect the Windows Service entry for KelpieMCPServer.

Commands in this group:

- [`kelpiemcp service register`](#kelpiemcp-service-register)
- [`kelpiemcp service unregister`](#kelpiemcp-service-unregister)
- [`kelpiemcp service status`](#kelpiemcp-service-status)

#### `kelpiemcp service register`

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

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

#### `kelpiemcp service unregister`

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

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

#### `kelpiemcp service status`

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

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

### MCP password session

Store or clear SSH password sessions in the running MCP server process memory.

Commands in this group:

- [`kelpiemcp password <profile>`](#kelpiemcp-password-profile)
- [`kelpiemcp login <profile>`](#kelpiemcp-login-profile)
- [`kelpiemcp forget <profile>`](#kelpiemcp-forget-profile)
- [`kelpiemcp logout <profile>`](#kelpiemcp-logout-profile)

#### `kelpiemcp password <profile>`

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

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

#### `kelpiemcp login <profile>`

Compatibility alias for `kelpiemcp password <profile>`.
It prompts for a password and stores it only in the running `KelpieMCPServer` process memory.

```powershell
kelpiemcp login vps01
```

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

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

#### `kelpiemcp forget <profile>`

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

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

#### `kelpiemcp logout <profile>`

Compatibility alias for `kelpiemcp forget <profile>`.
It clears the in-memory password session for a profile in the running `KelpieMCPServer` process.

```powershell
kelpiemcp logout vps01
```

Existing SSH terminal connections are not closed by this command.
Use the relevant terminal/session close command for connection cleanup.

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

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

### Initialization

Create the local Kelpie home layout and sample configuration files.

Commands in this group:

- [`kelpie init [--silent] [profile]`](#kelpie-init---silent-profile)
- [`kelpie config --check`](#kelpie-config---check)

#### `kelpie init [--silent] [profile]`

Creates the local Kelpie home directory layout.

Syntax:

```powershell
kelpie init
kelpie init vps01
kelpie init --silent
kelpie init --silent vps01
```

When a profile name is supplied, a named sample profile is created under `profiles/<profile>.json`.
Existing configuration files are not overwritten.
By default, the command prompts for MCP server configuration values and SSH profile template values before creating new files. Press Enter to use the displayed default value.
The MCP configuration prompts cover `LogDirectory`, `Server.Port`, and `Server.ControlPipeName` in `config/kelpiemcp.json`.
Use `--silent` for non-interactive setup with the default configuration and profile template values.
Use `kelpie profile create <profile>` when the Kelpie home is already initialized and only a new profile template should be created.

Return value:

- Exit code `0` when initialization completes.
- Non-zero exit code when the profile name is invalid, an unknown option is supplied, a prompted value is invalid, or a file-system operation fails.
- Standard output describes created and existing directories/files. The structured internal result is `KelpieHomeInitializationResult` with `HomeDirectory`, `ProfileName`, `CreatedDirectories`, `CreatedFiles`, and `ExistingFiles`.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
```

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

#### `kelpie config --check`

Validates the local Kelpie CLI and MCP configuration files without opening an SSH connection.
Use this as the first local health check after `kelpie init`, after editing `config/kelpie.json` or `config/kelpiemcp.json`, and before investigating SSH-side failures.

```powershell
kelpie config --check
kelpie config check
kelpie config check --no-pager
```

Processing:

- Reads `config/kelpie.json` and `config/kelpiemcp.json`.
- Reports file existence, JSON parse status, canonical `Editor` key usage, MCP server settings, and runtime directories.
- Prints each result as `<item>: OK` or `<item>: NG (<reason>)`.
- Prints multi-value sections one item per indented line. Empty sections are printed as `(empty list): OK` unless that section requires at least one value.
- Prints `Check summary: OK=<ok-count>/<check-count> NG=<ng-count>/<check-count>` as the final line.
- In an interactive terminal, long output is paged with `-- more -- (Return to continue, q to quit)`.
- Use `--no-pager` to disable paging, or `--pager` to request paging. Redirected or non-interactive output is printed without paging.

Return value:

- Exit code `0` when all checked items are OK.
- Exit code `1` when any checked item is NG.

Execution result sample:

```text
Kelpie config file: OK
Kelpie config JSON: OK
Editor: OK
MCP config file: OK
MCP config JSON: OK
Server: OK
Server.ControlPipeName: OK
Server.Port: OK
Directories:
  config: OK
  profiles: OK
  logs: OK
  bin: OK
  keys: OK
  dat: OK
Check summary: OK=14/14 NG=0/14
```

### Profile/session

Select SSH profiles and manage interactive or temporary sessions.

Commands in this group:

- [`kelpie profiles`](#kelpie-profiles)
- [`kelpie profile create <profile>`](#kelpie-profile-create-profile)
- [`kelpie profile edit <profile>`](#kelpie-profile-edit-profile)
- [`kelpie profile delete <profile-pattern>`](#kelpie-profile-delete-profile-pattern)
- [`kelpie profile clean <profile-pattern>`](#kelpie-profile-clean-profile-pattern)
- [`kelpie profile commit <profile-pattern>`](#kelpie-profile-commit-profile-pattern)
- [`kelpie profile rollback <profile-pattern>`](#kelpie-profile-rollback-profile-pattern)
- [`kelpie profile check <profile>`](#kelpie-profile-check-profile)
- [`kelpie profile show <profile-pattern>`](#kelpie-profile-show-profile-pattern)
- [`kelpie open <profile>`](#kelpie-open-profile)
- [`kelpie login`](#kelpie-login)
- [`kelpie logout`](#kelpie-logout)
- [`kelpie sessions`](#kelpie-sessions)
- [`kelpie kill <handle>`](#kelpie-kill-handle)

#### `kelpie profiles`

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

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

#### `kelpie profile create <profile>`

Creates one new SSH profile template in an already initialized Kelpie home directory.
The command prompts for template values interactively. Press Enter to use the displayed default value.

```powershell
kelpie profile create vps02
kelpie profile create vps02 --silent
kelpie profile create vps02 --silent --host-address: demo
kelpie profile create vps02 --dry-run --host-address: demo
kelpie profile create vps02 --no-backup
```

Arguments:

| Argument | Required | Description |
| :--- | :---: | :--- |
| `<profile>` | yes | Single SSH profile name. The file is created as `profiles/<profile>.json`. Wildcards, path separators, and invalid file-name characters are rejected. |
| `--silent` | no | Create the profile without prompts, using default template values unless overridden by template options. |
| `--host-address <value>` | no | Override `Host.Address`. The form `--host-address: <value>` is also accepted. |
| `--port <value>` | no | Override `Host.Port`. Must be `1` to `65535`. |
| `--ssh-user <value>` | no | Override `DefaultUser`. |
| `--auth-method <privateKey\|password>` | no | Override `Auth.Method`. |
| `--private-key-file <value>` | no | Override `Auth.PrivateKeyFile`. Used when `--auth-method privateKey` is selected. |
| `--password-secret-name <value>` | no | Override `Auth.PasswordSecretName`. Used when `--auth-method password` is selected. |
| `--os-family <value>` | no | Override `Platform.OsFamily`. |
| `--mode <ReadOnly\|Safe\|Maintenance\|Expert>` | no | Override the generated default user's `Mode`. |
| `--read-only-root <value>` | no | Override generated read-only roots. Repeat to add multiple values. Use `-` to clear the list. |
| `--read-write-root <value>` | no | Override generated read-write roots. Repeat to add multiple values. Use `-` to clear the list. |
| `--allowed-root <key=value[;...]>` | no | Override generated `AllowedRoots` map entries. Repeatable. Values such as `ReadOnly` and `ReadWrite` are normalized to `$ReadOnly` and `$ReadWrite`; other values such as `$Write` are preserved. |
| `--deny-pattern <value>` | no | Override generated deny patterns. Repeat to add multiple values. Use `-` to clear the list. |
| `--special-path <key=value[;...]>` | no | Override generated `SpecialPaths` map entries. Repeatable. `deny`, `confirm`, and `allow` are normalized to `Deny`, `Confirm`, and `Allow`. |
| `--dry-run` | no | Print the generated profile JSON and planned backup/write operation without changing files. |
| `--no-backup` | no | When overwriting an existing profile, do not create `profiles/<profile>.json.kelpie`; write the new profile as an immediate commit. |

Processing:

- Requires an initialized Kelpie home created by `kelpie init`.
- Creates only `profiles/<profile>.json`.
- Does not create or update `config/kelpie.json`, `config/kelpiemcp.json`, directories, trust-store entries, or open-profile state.
- If `profiles/<profile>.json` already exists, asks whether to overwrite it. When overwriting, the old file is saved as `profiles/<profile>.json.kelpie`.
- If a `.kelpie` backup already exists, the command fails and asks the user to run `kelpie profile commit <profile>` or `kelpie profile rollback <profile>` first.
- With `--no-backup`, overwriting does not create a `.kelpie` backup and does not ask `Commit profile? [Y/n]:`.
- Prompts for host address, port, SSH user, authentication method, private key file or password secret name, OS family, mode, allowed roots, and deny pattern.
- With `--silent`, does not prompt for template values and writes defaults: `Host.Address = localhost`, `Host.Port = 22`, `DefaultUser = deploy`, private-key auth, `Mode = Safe`, `Platform.OsFamily = debian`, read-only root `/var/log`, read-write root `/var/www`, and deny pattern `**/.env`.
- With `--dry-run`, prints the target profile path, backup plan, and generated JSON without writing files. Dry-run template options are allowed even without `--silent`.
- Silent template options can appear before or after `<profile>`. Options can be written as `--name value`, `--name=value`, or `--name: value`.
- `--allowed-root` and `--special-path` accept semicolon-separated map entries. Use quotes around values containing `;`. In PowerShell, use single quotes when the value contains `$`, for example `--allowed-root '/srv/www=$ReadWrite;/tmp=$Write'`.
- `--allowed-root` replaces the default generated allowed-root map unless `--read-only-root` or `--read-write-root` are also specified. `--special-path` replaces the default generated special-path map unless `--deny-pattern` is also specified.
- For password authentication, the command writes only `PasswordSecretName`. It never asks for or stores the raw password.
- Optional allowed-root and deny-pattern prompts accept one value per line. Press Enter on an empty line or enter `-` to omit or finish that prompt.
- After overwriting an existing profile, asks `Commit profile? [Y/n]:`. `Y` removes the `.kelpie` backup. `n` leaves the backup pending for later `commit` or `rollback`.
- After creating a profile that should be visible to a protected MCP server, review the file and run `kelpiemcp profile add <profile>`.

Return value:

- Exit code `0` when the profile template is created.
- Non-zero exit code when the profile name is missing or invalid, Kelpie home is not initialized, overwrite is rejected, a pending backup already exists, or the file cannot be written.
- Standard output contains the created profile name and file path.
- Standard error contains validation and file-system errors.

Execution result sample:

```text
Create SSH profile template.
Press Enter to use the default value.
Host address [localhost]:
Port [22]:
SSH user [deploy]:
Authentication method (privateKey/password) [privateKey]:
Private key file [vps02_ed25519]:
OS family [debian]:
Mode (ReadOnly/Safe/Maintenance/Expert) [Safe]:
Read-only root [Return to skip]: /var/log/nginx
Read-only root [Return to skip]:
Read-write root [Return to skip]:
Deny pattern [Return to skip]: **/.secret
Deny pattern [Return to skip]:
Created profile: vps02
Profile file: D:\Kelpie\profiles\vps02.json
```

Silent sample:

```text
kelpie profile create demo --silent --host-address: demo
Created profile: demo
Profile file: D:\Kelpie\profiles\demo.json
```

Dry-run sample:

```text
kelpie profile create demo --dry-run --host-address: demo
Dry run: profile create
Would create profile: demo
Profile file: D:\Kelpie\profiles\demo.json
Would write:
{
  "Host": {
    "Address": "demo",
    "Port": 22
  }
}
No files were changed.
```

Silent map sample:

```powershell
kelpie profile create demo --silent `
  --allowed-root '/srv/www=$ReadWrite;/tmp=$Write' `
  --special-path '**/.env=Deny;**/.tmp=Allow'
```

Existing profile sample:

```text
Profile already exists: vps02. Overwrite? [Y/n]: Y
...
Commit profile? [Y/n]: n
Profile backup is pending: D:\Kelpie\profiles\vps02.json.kelpie
Run `kelpie profile commit vps02` or `kelpie profile rollback vps02`.
```

#### `kelpie profile edit <profile>`

Edits an existing SSH profile JSON file.
Without an edit operation, the command opens the configured editor and validates the profile after the editor exits.

```powershell
kelpie profile edit vps02
kelpie profile edit vps02 set Host.Port 2224
kelpie profile edit vps02 set Users.kelpie.Mode "Maintenance|WebUser|WebAdmin"
kelpie profile edit vps02 add-root /etc/nginx ReadWrite
kelpie profile edit vps02 rm-root /etc/nginx
kelpie profile edit vps02 add-deny "**/.htpasswd"
kelpie profile edit vps02 rm-deny "**/.htpasswd"
kelpie profile edit vps02 set Host.Port 2222 --no-backup
kelpie profile edit vps02 set Host.Port 2222 --dry-run
kelpie profile delete vps02
kelpie profile delete "vps-*"
kelpie profile clean vps02
kelpie profile commit vps02
kelpie profile rollback vps02
```

Arguments:

| Argument | Required | Description |
| :--- | :---: | :--- |
| `<profile>` | yes | Single SSH profile name. Wildcards are not supported because editor mode can block and edits are transactional per profile. |
| `<dotPath>` | for `set` | Scalar path to update. Supported values are `Host.Address`, `Host.Port`, `Auth.Method`, `Auth.PrivateKeyFile`, `Auth.PasswordSecretName`, `DefaultUser`, `Users.<user>.Mode`, `Platform.OsFamily`, and `Platform.PackageManager`. |
| `<value>` | for `set` | New scalar value. `Host.Port` must be an integer from `1` to `65535`. |
| `<path>` | for `add-root` / `rm-root` | Allowed root path or glob. |
| `<access>` | for `add-root` | `ReadOnly`, `ReadWrite`, `$ReadOnly`, or `$ReadWrite`. The value is normalized to the `$...` form. |
| `<pattern>` | for `add-deny` / `rm-deny` | Special path glob pattern. Patterns may contain dots, such as `**/.htpasswd`. |
| `--no-backup` | no | Do not create `profiles/<profile>.json.kelpie`; apply the edit as an immediate commit. |
| `--dry-run` | no | For explicit edit operations, validate the edit and print the JSON that would be written without changing files. |

Processing:

- `set` only accepts scalar paths. Object, dictionary, and array paths are rejected; use `add-root`, `rm-root`, `add-deny`, or `rm-deny` for dictionary settings.
- `profile edit` requires a single profile name. Wildcards are rejected.
- `add-root`, `rm-root`, `add-deny`, and `rm-deny` edit the default user's rule object when `Users.<DefaultUser>` is an object; otherwise they edit the profile-level object.
- Before changing an existing profile, the current file is saved as `profiles/<profile>.json.kelpie`. If that backup already exists, editing fails until the profile is committed or rolled back.
- After a successful edit, asks `Commit profile? [Y/n]:`. `Y` deletes the backup. `n` keeps the backup so `kelpie profile commit <profile>` or `kelpie profile rollback <profile>` can be run later.
- With `--no-backup`, the command does not create a `.kelpie` backup and does not ask `Commit profile? [Y/n]:`.
- With `--dry-run`, explicit edit operations `set`, `add-root`, `rm-root`, `add-deny`, and `rm-deny` validate the edit and print the JSON that would be written without changing files. Editor mode (`kelpie profile edit <profile>`) does not support `--dry-run`.
- `kelpie profile commit <profile-pattern>` deletes pending `.kelpie` backups and treats the current profile JSON states, including pending deletions, as committed.
- `kelpie profile rollback <profile-pattern>` restores `.kelpie` backups over the current profile JSON files. For pending deletions, it restores deleted profile files. It fails if no backup matches.
- The full profile is reloaded and validated before any non-editor update is written.
- Non-editor updates are written with a temporary file followed by replace, using UTF-8 without BOM and LF line endings.
- Editor mode resolves the editor from `config/kelpie.json` `Editor`, `KELPIE_EDITOR`, `VISUAL`, `EDITOR`, then OS default (`notepad` on Windows, `vi` on Unix).
- If `config/kelpie.json` still contains legacy lowercase `editor`, every `kelpie` command prints a standard-output warning asking the user to rename it to `Editor`.
- The editor command alias `vscode` is interpreted as the VS Code `code` CLI. On Windows, Kelpie resolves `code` from `PATH` / `PATHEXT` when available, so `"Editor": "vscode --wait"` can use the installed `code.cmd` path without hard-coding it.
- The special editor value `default` is case-insensitive and opens the profile file with the application associated with `.json` files. The value `Notepad` is also case-insensitive and starts Windows Notepad.
- Editor mode waits for the editor process to exit. Editors that return immediately should be configured with a wait option, for example `"Editor": "code --wait"`.
- If editor validation fails, the user can re-edit or abort. Abort restores the original file content.
- Editor mode requires an interactive console and fails when input is redirected.

Return value:

- Exit code `0` when the profile is updated and validated.
- Non-zero exit code when the profile is missing, a pending backup already exists, the path or value is invalid, profile validation fails, editor launch fails, or editor mode is used non-interactively.
- Standard output contains the updated profile name and resolved profile file path.
- Standard error contains validation and editor errors.
- Secrets, private keys, passphrases, and raw password values are not printed.

Execution result sample:

```text
Updated profile: vps02
Profile file: D:\Kelpie\profiles\vps02.json
Commit profile? [Y/n]:
```

Dry-run sample:

```text
kelpie profile edit vps02 set Host.Port 2222 --dry-run
Dry run: profile edit
Would update profile: vps02
Profile file: D:\Kelpie\profiles\vps02.json
Would create backup: D:\Kelpie\profiles\vps02.json.kelpie
Would write:
{
  "Host": {
    "Address": "localhost",
    "Port": 2222
  }
}
No files were changed.
```

Missing profile sample:

```text
SSH profile was not found: vps02
Use `kelpie profile create vps02` to create it.
```

#### `kelpie profile delete <profile-pattern>`

Deletes one or more existing SSH profiles through the same `.kelpie` transaction flow used by profile create and edit.

```powershell
kelpie profile delete vps02
kelpie profile delete "vps-*"
kelpie profile delete "vps-*" --no-backup
kelpie profile delete "vps-*" --dry-run
```

Arguments:

| Argument | Required | Description |
| :--- | :---: | :--- |
| `<profile-pattern>` | yes | SSH profile name or wildcard pattern. `*` matches zero or more characters and `?` matches one character. Path separators and invalid file-name characters other than `*` and `?` are rejected. |
| `--no-backup` | no | Do not create `.kelpie` backups; delete matching profiles as an immediate commit. |
| `--dry-run` | no | Print matching profiles, backup plan, and delete plan without changing files. |

Processing:

- Without wildcards, requires an existing `profiles/<profile>.json`.
- With wildcards, resolves matching `profiles/*.json` file names in the configured Kelpie home. The command prints the matched profile names before asking for confirmation.
- If a matching `.kelpie` backup already exists, the command prints a warning and skips that profile. Other matching profiles without pending backups can still be deleted.
- For a single exact profile, asks `Delete profile: <profile>? [Y/n]:` before changing files.
- For a wildcard pattern, asks ``Delete <count> profiles matching `<profile-pattern>`? [Y/n]:`` before changing files.
- On confirmation, saves each current profile as `profiles/<profile>.json.kelpie`, then deletes `profiles/<profile>.json`.
- Asks `Commit profile? [Y/n]:` for one exact profile, or `Commit profiles? [Y/n]:` for multiple wildcard matches. `Y` deletes the backup and finalizes deletion. `n` leaves backups pending so `kelpie profile rollback <profile>` can restore deleted profiles or `kelpie profile commit <profile>` can finalize deletion later.
- With `--no-backup`, the command does not create `.kelpie` backups and does not ask for commit after deletion.
- With `--dry-run`, the command does not ask for confirmation and does not create backups or delete profile files.

Return value:

- Exit code `0` when the profile deletion is created or canceled by the user.
- Non-zero exit code when neither profile files nor pending backups match, the profile pattern is invalid, or a file cannot be backed up or deleted.
- Standard output contains the matched and deleted profile names, file paths, and pending transaction guidance.
- Standard error contains validation and file-system errors.

Execution result sample:

```text
Delete profile: vps02? [Y/n]: Y
Deleted profile: vps02
Profile file: D:\Kelpie\profiles\vps02.json
Commit profile? [Y/n]: n
Profile backup is pending: D:\Kelpie\profiles\vps02.json.kelpie
Run `kelpie profile commit vps02` or `kelpie profile rollback vps02`.
```

Wildcard sample:

```text
Matched profiles: 2
  vps-alpha
  vps-beta
Delete 2 profiles matching `vps-*`? [Y/n]: Y
Deleted profiles: 2
  vps-alpha: D:\Kelpie\profiles\vps-alpha.json
  vps-beta: D:\Kelpie\profiles\vps-beta.json
Commit profiles? [Y/n]: n
Profile backups are pending:
  D:\Kelpie\profiles\vps-alpha.json.kelpie
  D:\Kelpie\profiles\vps-beta.json.kelpie
Run `kelpie profile commit <profile>` or `kelpie profile rollback <profile>` for each pending profile.
```

#### `kelpie profile clean <profile-pattern>`

Deletes profile files and their pending `.kelpie` backup files together.
This is an immediate cleanup command; it does not create a new backup and the cleaned profile cannot be restored with `kelpie profile rollback`.

```powershell
kelpie profile clean vps02
kelpie profile clean "vps-*"
kelpie profile clean "vps-*" --dry-run
```

Arguments:

| Argument | Required | Description |
| :--- | :---: | :--- |
| `<profile-pattern>` | yes | SSH profile name or wildcard pattern. `*` matches zero or more characters and `?` matches one character. Path separators and invalid file-name characters other than `*` and `?` are rejected. |
| `--dry-run` | no | Print matching profile and backup files that would be removed without changing files. |

Processing:

- Without wildcards, removes `profiles/<profile>.json` when it exists and `profiles/<profile>.json.kelpie` when it exists.
- With wildcards, resolves the union of matching `profiles/*.json` and `profiles/*.json.kelpie` file names in the configured Kelpie home.
- Prints the matched profile names before asking for confirmation.
- For a single exact profile, asks `Clean profile and backup: <profile>? [Y/n]:` before changing files.
- For a wildcard pattern, asks ``Clean <count> profiles and backups matching `<profile-pattern>`? [Y/n]:`` before changing files.
- On confirmation, deletes each matching profile JSON file and each matching `.kelpie` backup file if present.
- With `--dry-run`, the command does not ask for confirmation and does not delete profile or backup files.

Return value:

- Exit code `0` when the cleanup is applied or canceled by the user.
- Non-zero exit code when neither profile files nor pending backups match, the profile pattern is invalid, or a file cannot be deleted.
- Standard output contains the matched and cleaned profile names and file paths.
- Standard error contains validation and file-system errors.

Execution result sample:

```text
Clean profile and backup: vps02? [Y/n]: Y
Cleaned profile: vps02
Removed profile file: D:\Kelpie\profiles\vps02.json
Removed backup: D:\Kelpie\profiles\vps02.json.kelpie
```

Wildcard sample:

```text
Matched profiles: 2
  vps-alpha
  vps-beta
Clean 2 profiles and backups matching `vps-*`? [Y/n]: Y
Cleaned profiles: 2
  vps-alpha: D:\Kelpie\profiles\vps-alpha.json
  vps-beta: D:\Kelpie\profiles\vps-beta.json
```

#### `kelpie profile commit <profile-pattern>`

Commits pending profile transactions by deleting matching `.kelpie` backup files.

```powershell
kelpie profile commit vps02
kelpie profile commit "vps-*"
kelpie profile commit "vps-*" --dry-run
```

Arguments:

| Argument | Required | Description |
| :--- | :---: | :--- |
| `<profile-pattern>` | yes | SSH profile name or wildcard pattern. `*` matches zero or more characters and `?` matches one character. Path separators and invalid file-name characters other than `*` and `?` are rejected. |
| `--dry-run` | no | Print matching backups that would be removed without changing files. |

Processing:

- Without wildcards, requires `profiles/<profile>.json.kelpie`.
- With wildcards, resolves matching pending backups from `profiles/*.json.kelpie`.
- With `--dry-run`, the command does not ask for confirmation and does not delete backup files.
- Without `--dry-run`, exact commit removes the backup immediately; wildcard commit asks for confirmation before removing matching backups.

#### `kelpie profile rollback <profile-pattern>`

Rolls back pending profile transactions by restoring matching `.kelpie` backup files to their profile JSON paths.

```powershell
kelpie profile rollback vps02
kelpie profile rollback "vps-*"
kelpie profile rollback "vps-*" --dry-run
```

Arguments:

| Argument | Required | Description |
| :--- | :---: | :--- |
| `<profile-pattern>` | yes | SSH profile name or wildcard pattern. `*` matches zero or more characters and `?` matches one character. Path separators and invalid file-name characters other than `*` and `?` are rejected. |
| `--dry-run` | no | Print matching backups that would be restored without changing files. |

Processing:

- Without wildcards, requires `profiles/<profile>.json.kelpie`.
- With wildcards, resolves matching pending backups from `profiles/*.json.kelpie`.
- With `--dry-run`, the command does not ask for confirmation and does not restore or delete files.
- Without `--dry-run`, exact rollback restores the backup immediately; wildcard rollback asks for confirmation before restoring matching backups.

#### `kelpie profile check <profile>`

Validates one SSH profile file without opening an SSH connection.
Wildcards are not supported.
Use this before `kelpie open`, after profile edits, and before trusting or reloading an MCP profile baseline.

```powershell
kelpie profile check vps01
kelpie profile check vps01 --no-pager
```

Processing:

- Reads `profiles/<profile>.json`.
- Reports file existence, JSON parse status, profile schema validation, connection fields, authentication references, command provider support, profile policy lists, user entries, and pending `.kelpie` backup state.
- Prints each result as `<item>: OK` or `<item>: NG (<reason>)`.
- Prints multi-value sections one item per indented line, matching `kelpie profile show` style. Empty list sections are printed as `(empty list): OK`.
- Prints `Check summary: OK=<ok-count>/<check-count> NG=<ng-count>/<check-count>` as the final line.
- In an interactive terminal, long output is paged with `-- more -- (Return to continue, q to quit)`.
- Use `--no-pager` to disable paging, or `--pager` to request paging. Redirected or non-interactive output is printed without paging.
- Fails `User` or `Users` entries that use direct `root` login.
- For private-key authentication, checks that the resolved private key file exists.

Return value:

- Exit code `0` when all checked items are OK.
- Exit code `1` when any checked item is NG.

Execution result sample:

```text
Profile file: OK
Profile JSON: OK
Profile schema: OK
Host.Address: OK
Host.Port: OK
User: OK
Auth.Method: OK
Auth.PrivateKeyFile: OK
Platform.OsFamily: OK
Platform.PackageManager: OK
Mode: OK
Command providers:
  DebianDiagnosticCommandProvider: OK
Capabilities:
  (empty list): OK
Roles:
  Safe: OK
Allowed roots:
  /var/www: OK
Special paths:
  **/.env: OK
Users:
  deploy: OK
Pending backup: OK
Check summary: OK=18/18 NG=0/18
```

#### `kelpie profile show <profile-pattern>`

Shows one or more sanitized profile summaries.
Secret values are not printed.

```powershell
kelpie profile show vps01
kelpie profile show vps01 --no-pager
```

Return value:

- Exit code `0` when the profile exists and the sanitized summary is printed.
- Standard output contains profile metadata safe for terminal display. Secret values are not returned.
- List-style fields such as `Command providers`, `Capabilities`, `Roles`, `Allowed roots`, `Special paths`, `Services`, and `Users` are printed one entry per indented line.
- Empty list-style fields are printed as `(empty list)`. Map-like list fields use `=>` between key and value, and the key column is padded so the value column lines up.
- In an interactive terminal, long output is paged with `-- more -- (Return to continue, q to quit)`.
- Use `--no-pager` to disable paging, or `--pager` to request paging. Redirected or non-interactive output is printed without paging.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
```

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

#### `kelpie open <profile>`

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

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

#### `kelpie login`

Starts an interactive SSH login for the currently open profile.

```powershell
kelpie open vps01
kelpie login
```

`kelpie login` does not accept a profile argument. Select a profile first with `kelpie open <profile>`.
When GUI mode is enabled, the command may start the desktop frontend instead of a console session.

Return value:

- Exit code `0` when the interactive login starts and exits normally.
- Non-zero exit code when no profile is open, the profile cannot be resolved, authentication fails, or SSH rejects the connection.
- Standard output and standard error are produced by the interactive SSH session and local validation messages.
- Password values are not returned.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<interactive SSH terminal output>",
  "stderr": ""
}
```

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

#### `kelpie logout`

Attempts to leave an interactive SSH session.

```powershell
kelpie logout
```

The top-level command does not own a persistent interactive session after `kelpie login` exits, so this command currently reports that no interactive SSH session is active.
Inside an active interactive SSH shell, use the remote shell's normal `exit` or `logout` command.

Return value:

- Non-zero exit code when no interactive SSH session is active.
- Standard error contains the local status message.

Return value sample:

```json
{
  "exitCode": 1,
  "stdout": "",
  "stderr": "No interactive SSH session is active."
}
```

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

#### `kelpie sessions`

Lists temporary SSH sessions held by the running MCP server process.

```powershell
kelpie sessions
```

The command talks to the configured MCP control pipe and prints in-memory sessions such as password sessions.

Return value:

- Exit code `0` when the running MCP server returns the session list.
- Standard output contains either a table of session handles or `No SSH sessions.`.
- Secret values are not returned.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "Handle      Profile  Kind      StartedAt\nssh-abc123  vps01    password  2026-06-05 01:02:03Z",
  "stderr": ""
}
```

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

#### `kelpie kill <handle>`

Clears a temporary SSH session by handle in the running MCP server process.

```powershell
kelpie kill ssh-abc123
```

Return value:

- Exit code `0` when the session handle is found and cleared.
- Non-zero exit code when the handle is missing or not found by the server.
- Standard output confirms the cleared handle on success.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "SSH session killed: ssh-abc123",
  "stderr": ""
}
```

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

### Mode/UI

Switch between CLI and GUI behavior or choose a temporary launch mode.

Commands in this group:

- [`kelpie login --console`](#kelpie-login---console)
- [`kelpie login --desktop`](#kelpie-login---desktop)
- [`kelpie cli`](#kelpie-cli)
- [`kelpie gui`](#kelpie-gui)

#### `kelpie login --console`

Starts a separate Windows console login window for the currently open profile.

```powershell
kelpie open vps01
kelpie login --console
```

This option is supported on Windows. It starts a new console process and leaves the current command after the launch request.

Return value:

- Exit code `0` when the console launch request succeeds.
- Non-zero exit code when no profile is open or the console process cannot be started.
- Standard output confirms the console launch.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "Kelpie login console started: vps01",
  "stderr": ""
}
```

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

#### `kelpie login --desktop`

Starts the desktop frontend for the currently open profile.

```powershell
kelpie open vps01
kelpie login --desktop
```

Return value:

- Exit code `0` when the desktop launch request is accepted.
- Non-zero exit code when no profile is open or the desktop frontend cannot be started.
- Standard output contains the user-facing launch status when the launcher prints one.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<desktop launch status>",
  "stderr": ""
}
```

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

#### `kelpie cli`

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

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

#### `kelpie gui`

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

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

### Diagnostics

Show MCP status, sanitized profile information, SSH diagnostics, and service logs.

Commands in this group:

- [`kelpie status <profile>`](#kelpie-status-profile)
- [`kelpie diag <profile>`](#kelpie-diag-profile)
- [`kelpie inventory <profile>`](#kelpie-inventory-profile)
- [`kelpie logs <profile> <service> [lines]`](#kelpie-logs-profile-service-lines)

#### `kelpie status <profile>`

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

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

#### `kelpie diag <profile>`

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
- SSH connection failures are reported as short standard-error messages instead of raw stack traces.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
```

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

#### `kelpie inventory <profile>`

Runs the read-only target inventory probe over SSH.

```powershell
kelpie inventory vps01
```

This command executes the same underlying `target_inventory` SSH command that backs the MCP `get_target_inventory` tool.
The output includes `/etc/os-release` and one `ITEM` row per probed helper or software command, including `python3`, `php`, `node`, `systemctl`, `journalctl`, `findmnt`, `ss`, and `ip`.
The result is an execution-time probe; Kelpie does not write detected capabilities back to the profile file.

Return value:

- Exit code `0` when the OS probe and command inventory complete.
- Standard output contains tab-separated `OS` and `ITEM` rows.
- Standard error contains SSH or policy errors if the inventory run fails.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "OS\tUbuntu\t24.04\tubuntu\nITEM\thelper\tPython\tpython3\t0\tPython 3.12.3\nITEM\tsoftware\tsystemctl\tsystemctl\t0\tsystemd 255\n",
  "stderr": ""
}
```

Execution result sample:

```text
# target_inventory
OS	Ubuntu	24.04	ubuntu
ITEM	helper	Python	python3	0	Python 3.12.3
ITEM	helper	PHP	php	127	command not found
ITEM	software	Node.js	node	127	command not found
ITEM	software	systemctl	systemctl	0	systemd 255 (255.4-1ubuntu8)
ITEM	software	journalctl	journalctl	0	systemd 255 (255.4-1ubuntu8)
ITEM	software	findmnt	findmnt	0	findmnt from util-linux 2.39.3
ITEM	software	ss	ss	0	ss utility, iproute2-6.1.0
ITEM	software	ip	ip	0	ip utility, iproute2-6.1.0
```

Safety notes:

- Inventory output may reveal installed software names and versions. Do not paste production inventory into public issues without review.
- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

#### `kelpie logs <profile> <service> [lines]`

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
- SSH connection failures are reported as short standard-error messages instead of raw stack traces.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
```

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

### Packages

Inspect packages and run confirmation-gated package changes over SSH.

Commands in this group:

- [`kelpie pkg check-updates <profile>`](#kelpie-pkg-check-updates-profile)
- [`kelpie pkg info <profile> <package>`](#kelpie-pkg-info-profile-package)
- [`kelpie pkg search <profile> <query> [limit]`](#kelpie-pkg-search-profile-query-limit)
- [`kelpie pkg list-installed <profile> <filter> [limit]`](#kelpie-pkg-list-installed-profile-filter-limit)
- [`kelpie pkg simulate-install <profile> <package>`](#kelpie-pkg-simulate-install-profile-package)
- [`kelpie pkg simulate-remove <profile> <package>`](#kelpie-pkg-simulate-remove-profile-package)
- [`kelpie pkg install <profile> <package> [--confirm <token>]`](#kelpie-pkg-install-profile-package---confirm-token)
- [`kelpie pkg remove <profile> <package> [--confirm <token>]`](#kelpie-pkg-remove-profile-package---confirm-token)

#### `kelpie pkg check-updates <profile>`

Checks available package updates through the profile's package provider.

```powershell
kelpie pkg check-updates vps01
```

#### `kelpie pkg info <profile> <package>`

Shows package metadata through the profile's package provider.

```powershell
kelpie pkg info vps01 nginx
```

#### `kelpie pkg search <profile> <query> [limit]`

Searches packages and prints at most `limit` rows. The default limit is `20`.

```powershell
kelpie pkg search vps01 nginx 20
```

#### `kelpie pkg list-installed <profile> <filter> [limit]`

Lists installed packages matching `filter` and prints at most `limit` rows. The default limit is `50`.

```powershell
kelpie pkg list-installed vps01 nginx 50
```

#### `kelpie pkg simulate-install <profile> <package>`

Runs the provider dry-run command for package installation.

```powershell
kelpie pkg simulate-install vps01 nginx
```

#### `kelpie pkg simulate-remove <profile> <package>`

Runs the provider dry-run command for package removal.

```powershell
kelpie pkg simulate-remove vps01 nginx
```

#### `kelpie pkg install <profile> <package> [--confirm <token>]`

Without `--confirm`, returns a confirmation token and does not install the package.
With the exact token, runs the provider install command after profile policy is rechecked.

```powershell
kelpie pkg install vps01 nginx
kelpie pkg install vps01 nginx --confirm pkg_install:nginx
```

#### `kelpie pkg remove <profile> <package> [--confirm <token>]`

Without `--confirm`, returns a confirmation token and does not remove the package.
With the exact token, runs the provider remove command after profile policy is rechecked.

```powershell
kelpie pkg remove vps01 nginx
kelpie pkg remove vps01 nginx --confirm pkg_remove:nginx
```

Return value:

- Read-only package commands return the remote package-manager output.
- `install` and `remove` without `--confirm` return `Requires confirmation: true`, a `Confirmation` token, and the command preview without changing the target.
- `install` and `remove` with an empty or mismatched token return non-zero and do not change the target.
- `install` and `remove` with the exact token run only if profile mode and policy allow the operation.

Safety notes:

- Run `simulate-install` or `simulate-remove` before confirmed changes.
- Use disposable SSH targets for first confirmation-gated package tests.
- Do not paste package-manager raw logs containing host, repository, or customer details into public issues.

### Environment

List, read, temporarily set, or persist remote environment variables under profile policy.

Commands in this group:

- [`kelpie env keys <profile>`](#kelpie-env-keys-profile)
- [`kelpie env peek <profile> <key>`](#kelpie-env-peek-profile-key)
- [`kelpie env set <profile> <key> <value> -- <command>`](#kelpie-env-set-profile-key-value----command)
- [`kelpie env list <profile>`](#kelpie-env-list-profile)
- [`kelpie env persist <profile> <key> <value>`](#kelpie-env-persist-profile-key-value)
- [`kelpie env remove <profile> <key>`](#kelpie-env-remove-profile-key)

#### `kelpie env keys <profile>`

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

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

#### `kelpie env peek <profile> <key>`

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

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

#### `kelpie env set <profile> <key> <value> -- <command>`

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

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

#### `kelpie env list <profile>`

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

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

#### `kelpie env persist <profile> <key> <value>`

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

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

#### `kelpie env remove <profile> <key>`

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

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

### Help/version

Show command help and version information.

Commands in this group:

- [`kelpie version`](#kelpie-version)
- [`kelpie help`](#kelpie-help)
- [`kelpiemcp version`](#kelpiemcp-version)
- [`kelpiemcp help`](#kelpiemcp-help)

#### `kelpie version`

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

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

#### `kelpie help`

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

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

#### `kelpiemcp version`

Shows the `kelpiemcp` command version.

```powershell
kelpiemcp version
kelpiemcp --version
kelpiemcp -v
```

Return value:

- Exit code `0` when the version is printed.
- Standard output contains the `kelpiemcp` product version string.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
```

Execution result sample:

```text
kelpiemcp 0.3.4.0
```

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

#### `kelpiemcp help`

Shows `kelpiemcp` command help.

```powershell
kelpiemcp help
kelpiemcp --help
kelpiemcp -h
```

Return value:

- Exit code `0` when help text is printed.
- Standard output contains terminal help text for the available `kelpiemcp` command set.

Return value sample:

```json
{
  "exitCode": 0,
  "stdout": "<command-specific terminal output>",
  "stderr": ""
}
```

Execution result sample:

The terminal execution result is represented by the return value sample above: process exit code, standard output, and standard error.

Safety notes:

- Do not include real host names, user names, secrets, production paths, or customer data in committed examples.

## Safety Notes

- KelpieSSH starts from read-oriented diagnostics and allow-listed commands.
- Dangerous operations require dedicated commands, policy checks, and confirmation strings.
- Passwords are session-only for the MCP server process.
- Production profile files and private keys must stay outside the public repository.

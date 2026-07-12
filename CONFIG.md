# KelpieSSH Configuration

Last updated: 2026-07-12

This file is the English reference for KelpieSSH configuration file locations and host-level settings.
For Japanese documentation, see [docs/ja/CONFIG.ja.md](docs/ja/CONFIG.ja.md).

SSH profile settings are documented separately in [PROFILE_GUIDE.md](PROFILE_GUIDE.md).
Japanese profile guidance is available in [docs/ja/PROFILE_GUIDE.ja.md](docs/ja/PROFILE_GUIDE.ja.md).

## Configuration Directory

KelpieSSH uses a local Kelpie home directory. With the default manual layout, the directory is:

```text
D:\Kelpie
```

The usual layout is:

```text
D:\Kelpie
├─ config
│  ├─ kelpie.json
│  └─ kelpiemcp.json
├─ profiles
│  └─ sample.json
├─ keys
├─ dat
├─ logs
└─ bin
```

Kelpie home is resolved in this order:

1. If `--bin-dir <dir>` is specified as a runtime path override, the parent directory of `<dir>` is Kelpie home.
2. If `KELPIE_HOME` is set and the directory exists, `KELPIE_HOME` is Kelpie home.
3. Otherwise, the parent directory of the startup directory is Kelpie home.

KelpieSSH does not read `KELPIEPRO_HOME`.

## File Generation

`kelpie init` creates the local directory layout and sample files.
Existing files are not overwritten.

Public sample files are stored under `config_samples/` in this repository.
They are examples only and must not contain real hosts or secrets.

```text
config_samples/
├─ kelpie.json
├─ kelpiemcp.json
└─ servers/
   └─ vps01.json
```

For a safe validation workflow, copy `config_samples/servers/vps01.json` to `KelpieHome/profiles/vps01.json`, edit it for a disposable SSH target such as a local Docker SSH container, and run:

```powershell
kelpie config check
kelpie profile check vps01
```

The check commands validate local configuration and profile files before opening an SSH connection. Do not copy real host names, real user names, private keys, passwords, passphrases, or raw operational logs into sample files.

## Main Settings

### `config/kelpie.json`

Used by the `kelpie` command.

Important values:

| Setting | Required | Initial value | Purpose |
| :--- | :---: | :--- | :--- |
| `LogDirectory` | no | `KelpieHome\logs` | Directory for CLI logs. |
| `OpenProfile` | no | none | Last selected profile name for commands that use the open profile. Runtime state is normally stored in `dat/kelpie_client_state.json`. |
| `Server:ControlPipeName` | no | none | Local named pipe used by `kelpie` / `kelpiemcp` to control the server. Usually configured in `kelpiemcp.json`; commands that contact the server require an effective value. |
| `Commands:ExecutablePath` | no | none | Optional explicit `kelpie` command path. |
| `Commands:WorkingDirectory` | no | none | Optional command working directory. |
| `Editor` | no | empty string | Optional editor command used by `kelpie profile edit <profile>` editor mode. Arguments are allowed. |

Minimal example:

```json
{
  "LogDirectory": "D:\\Kelpie\\logs",
  "Editor": ""
}
```

`kelpie profile edit <profile>` resolves the editor in this order:

1. `config/kelpie.json` `Editor`
2. `KELPIE_EDITOR`
3. `VISUAL`
4. `EDITOR`
5. OS default: `notepad` on Windows, `vi` on Unix

Legacy lowercase `editor` is accepted for compatibility. When `kelpie.json` contains `editor`, every `kelpie` command prints a standard-output warning asking the user to rename it to `Editor`. The key is normalized to `Editor` when Kelpie updates the config file.

The editor process is started in blocking mode and Kelpie waits for it to exit before validating the profile.
Editors that normally return immediately must be configured with a wait option, for example:

```json
{
  "Editor": "code --wait"
}
```

Special values:

| Value | Meaning |
| :--- | :--- |
| `vscode` | Case-insensitive alias for the VS Code `code` CLI. On Windows, Kelpie resolves `code` from `PATH` / `PATHEXT` when available, so `"Editor": "vscode --wait"` can use the installed `code.cmd` path without hard-coding it. |
| `Notepad` | Case-insensitive. Starts Windows Notepad. |
| `default` | Case-insensitive. Opens the profile `.json` file with the application associated by the OS. |

### `config/kelpiemcp.json`

Used by `kelpiemcp` and `KelpieMCPServer`.

Important values:

| Setting | Required | Initial value | Purpose |
| :--- | :---: | :--- | :--- |
| `AllowedHosts` | no | `localhost;127.0.0.1;[::1]` | HTTP Host allow-list for the local MCP server. |
| `Server:ControlPipeName` | yes | `KelpieMCPServer.Control` | Local named pipe used by `kelpiemcp` to control the server. |
| `LogDirectory` | no | `KelpieHome\logs` | Directory for MCP server logs. |
| `Commands:ExecutablePath` | no | `KelpieHome\bin\mcp\KelpieMCPServer.exe` on Windows | Optional explicit `KelpieMCPServer` executable path. |
| `Commands:WorkingDirectory` | no | `KelpieHome\bin` | Optional server working directory. |
| `ProfileOperations` | no | CLI `Allow`, MCP `Deny` | Allows or denies profile trust operations by caller channel. Defaults allow CLI operations and deny MCP operations. |

By default, the MCP endpoint is:

```text
http://127.0.0.1:45432/mcp
```

For browser-based health checks, use:

```text
http://127.0.0.1:45432/health
```

The public port is a runtime option, not a persistent `kelpiemcp.json` setting. Start `KelpieMCPServer` with `--port <port-number>`, where the allowed range is `1` through `65535`. When `--port` is omitted, the default is `45432`. A legacy `Server.Port` value is ignored and removed the next time `kelpie init` updates the configuration.

Minimal example:

```json
{
  "LogDirectory": "D:\\Kelpie\\logs",
  "Server": {
    "Port": 45432,
    "ControlPipeName": "KelpieMCPServer.Control"
  },
  "ProfileOperations": {
    "Add": {
      "CLI": "Allow",
      "MCP": "Deny"
    },
    "Reload": {
      "CLI": "Allow",
      "MCP": "Deny"
    },
    "Revoke": {
      "CLI": "Allow",
      "MCP": "Deny"
    }
  }
}
```

### `ProfileOperations`

`ProfileOperations` controls profile trust operations by caller channel.
Each operation has a `CLI` setting and an `MCP` setting.

Allowed values:

| Value | Meaning |
| :--- | :--- |
| `Allow` | The operation is allowed for the channel. |
| `Deny` | The operation is denied for the channel. |

The current implementation also accepts legacy values for compatibility: `Allowed` and boolean `true` are treated as `Allow`, and boolean `false` is treated as `Deny`.

Default policy:

| Setting | Required | Initial value | Purpose |
| :--- | :---: | :--- | :--- |
| `ProfileOperations:Add:CLI` | no | `Allow` | Allows `kelpiemcp profile add <profile>`. |
| `ProfileOperations:Reload:CLI` | no | `Allow` | Allows `kelpiemcp profile reload <profile>`. |
| `ProfileOperations:Revoke:CLI` | no | `Allow` | Allows `kelpiemcp profile revoke <profile>`. |
| `ProfileOperations:Add:MCP` | no | `Deny` | MCP profile add is not exposed. |
| `ProfileOperations:Reload:MCP` | no | `Deny` | Controls both `profile_reload` execution and the `ReloadAllowed` value returned by `ssh_profile_capabilities`. |
| `ProfileOperations:Revoke:MCP` | no | `Deny` | MCP profile revoke is not exposed. |

When a CLI operation is denied, the corresponding command returns a JSON result with `Success: false` and `Status: disabled-by-config`.
`kelpiemcp profile-capabilities [profile]` returns `AddAllowed`, `ReloadAllowed`, and `RevokeAllowed` after applying both the trust-store state and the `ProfileOperations:*:CLI` settings.

When `ProfileOperations:Reload:MCP` is `Deny`, `profile_reload` returns `Status: forbidden` without changing the catalog, and `ssh_profile_capabilities` returns `ReloadAllowed: false` with `Reason: disabled-by-config`.
This is the recommended default because profile file changes should be accepted by explicit user-side commands:

```powershell
kelpiemcp profile add <profile>
kelpiemcp profile reload <profile>
kelpiemcp profile revoke <profile>
```

Set `ProfileOperations:Reload:MCP` to `Allow` only when the operator intentionally allows MCP clients to execute profile reload and see reload capability for the connected profile.
Even then, trusted profile hash validation still applies; editing a profile file is not accepted just because this flag is enabled.

The encrypted trust store also records a normalized authorization snapshot for each trusted profile. `kelpiemcp profile reload <profile>` rejects permission expansion until an administrator reviews the returned changed fields and repeats the command with `--approve-privilege-expansion`.

## Runtime State

### `dat/kelpie_client_state.json`

`kelpie_client_state.json` stores runtime state for the `kelpie` CLI.
It is not a user-edited configuration file.

When `kelpie_client_state.json` does not exist and the legacy `storm_state.dat` exists, Kelpie renames the legacy file once. If the canonical file already exists, Kelpie uses it and does not read or overwrite the legacy file.

Example:

```json
{
  "OpenProfile": "vps01",
  "ClientMode": "cli"
}
```

| Setting | Required | Initial value | Purpose |
| :--- | :---: | :--- | :--- |
| `OpenProfile` | no | none | Last profile opened with `kelpie open <profile>`. `kelpie login` uses this value. |
| `ClientMode` | no | none | Client mode selected by commands such as `kelpie gui` or `kelpie cli`. |

## SSH Profiles

SSH profiles are JSON files under `profiles/`.
The file name is the profile name, so `profiles/vps01.json` is profile `vps01`.

Profile details are documented in [PROFILE_GUIDE.md](PROFILE_GUIDE.md).

Common commands:

```powershell
kelpie init vps01
kelpie config check
kelpie profile check vps01
kelpie profile show vps01
kelpie open vps01
kelpie login
```

## Log Directory Resolution

Log directories are resolved in this order:

1. `LogDirectory` in the configuration file read by the current command.
2. `KelpieHome/logs`.
3. `logs` under the startup directory.
4. The startup directory.

Relative `LogDirectory` values are resolved from the configuration file directory.

## Security Notes

- Do not commit real profile files.
- Do not commit private keys, passwords, passphrases, real host names, or real user names.
- Keep production `profiles/`, `keys/`, `dat/`, and `logs/` outside this public repository.
- Do not store plain text passwords in JSON configuration files.

# KelpieSSH Configuration

Last updated: 2026-06-18

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

## Main Settings

### `config/kelpie.json`

Used by the `kelpie` command.

Important values:

| Setting | Purpose |
| :--- | :--- |
| `LogDirectory` | Directory for CLI logs. |
| `OpenProfile` | Last selected profile name for commands that use the open profile. |
| `Server:ControlPipeName` | Local named pipe used by `kelpie` / `kelpiemcp` to control the server. |
| `Commands:ExecutablePath` | Optional explicit `kelpie` command path. |
| `Commands:WorkingDirectory` | Optional command working directory. |
| `editor` | Optional editor command used by `kelpie profile edit <profile>` editor mode. Arguments are allowed. |

Minimal example:

```json
{
  "LogDirectory": "D:\\Kelpie\\logs",
  "editor": ""
}
```

`kelpie profile edit <profile>` resolves the editor in this order:

1. `config/kelpie.json` `editor`
2. `KELPIE_EDITOR`
3. `VISUAL`
4. `EDITOR`
5. OS default: `notepad` on Windows, `vi` on Unix

The editor process is started in blocking mode and Kelpie waits for it to exit before validating the profile.
Editors that normally return immediately must be configured with a wait option, for example:

```json
{
  "editor": "code --wait"
}
```

Special values:

| Value | Meaning |
| :--- | :--- |
| `Notepad` | Case-insensitive. Starts Windows Notepad. |
| `default` | Case-insensitive. Opens the profile `.json` file with the application associated by the OS. |

### `config/kelpiemcp.json`

Used by `kelpiemcp` and `KelpieMCPServer`.

Important values:

| Setting | Purpose |
| :--- | :--- |
| `AllowedHosts` | HTTP Host allow-list for the local MCP server. |
| `Server:ControlPipeName` | Local named pipe used by `kelpiemcp` to control the server. |
| `LogDirectory` | Directory for MCP server logs. |
| `Commands:ExecutablePath` | Optional explicit `KelpieMCPServer` executable path. |
| `Commands:WorkingDirectory` | Optional server working directory. |
| `ProfileOperations` | Allows or denies profile trust operations by caller channel. Defaults allow CLI operations and deny MCP operations. |

By default, the MCP endpoint is:

```text
http://127.0.0.1:45432/mcp
```

For browser-based health checks, use:

```text
http://127.0.0.1:45432/health
```

The public port is a runtime option, not a persistent `kelpiemcp.json` setting. Start `KelpieMCPServer` with `--port <port-number>`, where the allowed range is `1` through `65535`. When `--port` is omitted, the default is `45432`. A legacy `Server.Port` value may remain in an existing file, but it is ignored and is not written by the server CLI.

Minimal example:

```json
{
  "LogDirectory": "D:\\Kelpie\\logs",
  "Server": {
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

| Setting | Default | Purpose |
| :--- | :--- | :--- |
| `ProfileOperations:Add:CLI` | `Allow` | Allows `kelpiemcp profile add <profile>`. |
| `ProfileOperations:Reload:CLI` | `Allow` | Allows `kelpiemcp profile reload <profile>`. |
| `ProfileOperations:Revoke:CLI` | `Allow` | Allows `kelpiemcp profile revoke <profile>`. |
| `ProfileOperations:Add:MCP` | `Deny` | MCP profile add is not exposed. |
| `ProfileOperations:Reload:MCP` | `Deny` | Controls the `ReloadAllowed` value returned by `ssh_profile_capabilities`. |
| `ProfileOperations:Revoke:MCP` | `Deny` | MCP profile revoke is not exposed. |

When a CLI operation is denied, the corresponding command returns a JSON result with `Success: false` and `Status: disabled-by-config`.
`kelpiemcp profile-capabilities [profile]` returns `AddAllowed`, `ReloadAllowed`, and `RevokeAllowed` after applying both the trust-store state and the `ProfileOperations:*:CLI` settings.

When `ProfileOperations:Reload:MCP` is `Deny`, `ssh_profile_capabilities` returns `ReloadAllowed: false` with `Reason: disabled-by-config`.
This is the recommended default because profile file changes should be accepted by explicit user-side commands:

```powershell
kelpiemcp profile add <profile>
kelpiemcp profile reload <profile>
kelpiemcp profile revoke <profile>
```

Set `ProfileOperations:Reload:MCP` to `Allow` only when the operator intentionally allows MCP clients to see reload capability for the connected profile.
Even then, trusted profile hash validation still applies; editing a profile file is not accepted just because this flag is enabled.

## Runtime State

### `dat/storm_state.dat`

`storm_state.dat` stores runtime state for the `kelpie` CLI.
It is not a user-edited configuration file.

Example:

```json
{
  "OpenProfile": "vps01",
  "ClientMode": "cli"
}
```

| Setting | Purpose |
| :--- | :--- |
| `OpenProfile` | Last profile opened with `kelpie open <profile>`. `kelpie login` uses this value. |
| `ClientMode` | Client mode selected by commands such as `kelpie gui` or `kelpie cli`. |

## SSH Profiles

SSH profiles are JSON files under `profiles/`.
The file name is the profile name, so `profiles/vps01.json` is profile `vps01`.

Profile details are documented in [PROFILE_GUIDE.md](PROFILE_GUIDE.md).

Common commands:

```powershell
kelpie init vps01
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

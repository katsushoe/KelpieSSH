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
| `Server:Port` | Local HTTP port used by the MCP server. |
| `Server:ControlPipeName` | Local named pipe used by `kelpie` / `kelpiemcp` to control the server. |
| `Commands:ExecutablePath` | Optional explicit `kelpie` command path. |
| `Commands:WorkingDirectory` | Optional command working directory. |

Minimal example:

```json
{
  "LogDirectory": "D:\\Kelpie\\logs"
}
```

### `config/kelpiemcp.json`

Used by `kelpiemcp` and `KelpieMCPServer`.

Important values:

| Setting | Purpose |
| :--- | :--- |
| `AllowedHosts` | HTTP Host allow-list for the local MCP server. |
| `Server:Port` | Local HTTP port for the MCP endpoint. |
| `Server:ControlPipeName` | Local named pipe used by `kelpiemcp` to control the server. |
| `LogDirectory` | Directory for MCP server logs. |
| `Commands:ExecutablePath` | Optional explicit `KelpieMCPServer` executable path. |
| `Commands:WorkingDirectory` | Optional server working directory. |
| `ProfileOperations:Reload:MCP` | Whether MCP clients may use MCP-side profile reload capability. Default is `false`; intentional profile file edits are accepted with `kelpiemcp profile reload <profile>`. |

By default, the MCP endpoint is:

```text
http://127.0.0.1:45432/mcp
```

For browser-based health checks, use:

```text
http://127.0.0.1:45432/health
```

Minimal example:

```json
{
  "LogDirectory": "D:\\Kelpie\\logs",
  "Server": {
    "Port": 45432,
    "ControlPipeName": "KelpieMCPServer.Control"
  },
  "ProfileOperations": {
    "Reload": {
      "MCP": false
    }
  }
}
```

### `ProfileOperations`

`ProfileOperations` controls profile-management capabilities that are visible to MCP clients.
It does not replace the CLI trust commands.

| Setting | Default | Purpose |
| :--- | :--- | :--- |
| `ProfileOperations:Reload:MCP` | `false` | Allows MCP clients to see MCP-side reload capability for the currently connected profile. |

When `ProfileOperations:Reload:MCP` is `false`, `ssh_profile_capabilities` returns `ReloadAllowed: false` with `Reason: disabled-by-config`.
This is the recommended default because profile file changes should be accepted by an explicit user-side command:

```powershell
kelpiemcp profile reload <profile>
```

Set `ProfileOperations:Reload:MCP` to `true` only when the operator intentionally allows MCP clients to request profile reload behavior.
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

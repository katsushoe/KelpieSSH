# KelpieSSH Configuration

Last updated: 2026-06-17

This file is the English configuration reference for KelpieSSH.
For Japanese documentation, see [CONFIG.ja.md](CONFIG.ja.md).

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

### `config/kelpiemcp.json`

Used by `kelpiemcp` and `KelpieMCPServer`.

Important values:

| Setting | Purpose |
| :--- | :--- |
| `Server:Port` | Local HTTP port for the MCP endpoint. |
| `Server:ControlPipeName` | Local named pipe used by `kelpiemcp` to control the server. |
| `LogDirectory` | Directory for MCP server logs. |

By default, the MCP endpoint is:

```text
http://127.0.0.1:45432/mcp
```

## Profile Settings

SSH profiles are JSON files under `profiles/`.
The file name is the profile name.

Example:

```json
{
  "Host": {
    "Address": "example.invalid",
    "Port": 22
  },
  "Auth": {
    "UserName": "deploy",
    "Method": "privateKey",
    "PrivateKeyFile": "vps01_ed25519"
  },
  "Connection": {
    "TimeoutSeconds": 10
  },
  "Platform": {
    "OsFamily": "debian",
    "PackageManager": "apt"
  },
  "Mode": "Safe",
  "AllowedRoots": {
    "/var/log": "$ReadOnly"
  }
}
```

## Authentication

`Auth` and `Authentication` are both accepted.
`Authentication` is the formal name and `Auth` is a short alias.

Supported methods:

| Method | Required values | Notes |
| :--- | :--- | :--- |
| `privateKey` | `PrivateKeyFile` | Relative key paths are resolved under `KelpieHome/keys`. |
| `password` | `PasswordSecretName` | The actual password is stored only in the running MCP server session. |

Plain text passwords must not be stored in JSON files.

## Policy

`Mode` controls the permission preset.
Supported values are:

- `ReadOnly`
- `Safe`
- `Maintenance`
- `Expert`

`Capabilities` are CLI-only overrides.
MCP execution ignores `Capabilities` and evaluates `Mode` only.

## Allowed Roots

`AllowedRoots` limits path-based operations.
Supported access expressions include:

- `$ReadOnly`
- `$ReadWrite`
- `$ALL`
- raw flags such as `@Read|@List|@Write|@CD`

`*` and `**` are explicit global path values.
If `AllowedRoots` is omitted or empty, path-based operations are not allowed by policy.

## Security Notes

- Do not commit real profile files.
- Do not commit private keys, passwords, passphrases, real host names, or real user names.
- Keep production `profiles/`, `keys/`, `dat/`, and `logs/` outside this public repository.


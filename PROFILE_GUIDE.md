# KelpieSSH Profile Guide

Last updated: 2026-06-17

This guide explains how to configure SSH profiles for KelpieSSH.
For Japanese documentation, see [docs/ja/PROFILE_GUIDE.ja.md](docs/ja/PROFILE_GUIDE.ja.md).

General configuration files such as `kelpie.json` and `kelpiemcp.json` are documented in [CONFIG.md](CONFIG.md).

## What Is a Profile?

An SSH profile is one saved SSH connection setting.
Profiles are stored as JSON files under `KelpieHome\profiles`.

The file name is the profile name:

```text
D:\Kelpie\profiles\vps01.json
```

This profile is used as `vps01`:

```powershell
kelpie open vps01
kelpie profile show vps01
kelpie status vps01
```

## Creating a Profile

Create a named profile with:

```powershell
kelpie init vps01
```

Then edit:

```text
<KelpieHome>\profiles\vps01.json
```

Before connecting, set at least:

- target host
- SSH user
- authentication method
- private key file name or password secret name
- target platform

For private key authentication, place the private key under:

```text
<KelpieHome>\keys
```

The matching public key must already be registered on the server, usually in the remote user's `~/.ssh/authorized_keys`.

## Minimal Private Key Profile

```json
{
  "Host": {
    "Address": "203.0.113.10",
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
    "OsFamily": "alma"
  },
  "Mode": "Safe"
}
```

Required local key file:

```text
<KelpieHome>\keys\vps01_ed25519
```

## Minimal Password Profile

Do not store the plain text password in the profile.
Store only a secret reference name.

```json
{
  "Host": {
    "Address": "203.0.113.10",
    "Port": 22
  },
  "Auth": {
    "UserName": "deploy",
    "Method": "password",
    "PasswordSecretName": "kelpie:vps01"
  },
  "Connection": {
    "TimeoutSeconds": 10
  },
  "Platform": {
    "OsFamily": "alma"
  },
  "Mode": "Safe"
}
```

After opening the profile, enter the password into the running session:

```powershell
kelpie open vps01
kelpie login
```

For MCP server session storage, use:

```powershell
kelpiemcp start
kelpiemcp password vps01
```

To clear it:

```powershell
kelpiemcp forget vps01
```

## Full Example

```json
{
  "Host": {
    "Address": "203.0.113.10",
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
    "OsFamily": "alma",
    "PackageManager": "dnf"
  },
  "Mode": "Safe",
  "Capabilities": [
    "AllowListPackage"
  ],
  "Rights": {
    "$WebDeploy": "$ReadWrite|@Import",
    "$LogRead": "$ReadOnly"
  },
  "AllowedRoots": {
    "/var/www": "$WebDeploy",
    "/var/log": "$LogRead"
  },
  "SpecialPaths": {
    "**/.env": "Deny",
    "**/.ssh/**": "Deny",
    "/var/www/.well-known/**": "Allow"
  },
  "Services": {
    "Nginx": {
      "Root": "/var/www/example",
      "Port": 80
    }
  }
}
```

## Field Reference

### `Host`

Target SSH endpoint.

| Field | Required | Description |
| :--- | :---: | :--- |
| `Host.Address` | yes | Host name or IP address. |
| `Host.Port` | no | SSH port. Default is `22`. |

Troubleshooting:

- If `Host.Address` is still `example.invalid`, edit it before connecting.
- If the server uses a non-standard SSH port, set `Host.Port`.

### `Auth` / `Authentication`

SSH authentication settings.
`Authentication` is the formal name.
`Auth` is the short alias used by samples.
If both are present, `Authentication` takes priority.

| Field | Required | Description |
| :--- | :---: | :--- |
| `Auth.UserName` | yes for single-user profiles | SSH login user. Direct `root` login is rejected. |
| `Auth.Method` | yes | `privateKey` or `password`. |
| `Auth.PrivateKeyFile` | yes for `privateKey` | Private key file name under `KelpieHome\keys`, or an absolute path. |
| `Auth.PrivateKeyPath` | no | Compatibility path. Prefer `PrivateKeyFile` for new profiles. |
| `Auth.PrivateKeyPassphrase` | no | Private key passphrase. Do not expose it in logs or public files. |
| `Auth.PasswordSecretName` | yes for `password` | Secret reference name. The password itself is entered at runtime. |

Troubleshooting:

- `SSH private key path is required`: set `Auth.PrivateKeyFile` or switch `Auth.Method` to `password`.
- `SSH password secret name is required`: set `Auth.PasswordSecretName`.
- Authentication fails with a private key: confirm the private key file exists under `KelpieHome\keys`, the remote public key is registered, and the SSH user is correct.
- Authentication fails with a password: run `kelpie open <profile>` then `kelpie login`, or register it with `kelpiemcp password <profile>` for the MCP server session.

### `Connection`

Connection behavior.

| Field | Required | Description |
| :--- | :---: | :--- |
| `Connection.TimeoutSeconds` | no | SSH connection timeout in seconds. Default is `10`. |

Troubleshooting:

- If slow servers time out, increase `TimeoutSeconds`.
- If the command hangs for too long, lower `TimeoutSeconds`.

### `Platform`

Target OS metadata used to select safe commands.

| Field | Required | Description |
| :--- | :---: | :--- |
| `Platform.OsFamily` | yes | Target OS family or alias. |
| `Platform.PackageManager` | no | `apt`, `dnf`, `yum`, etc. If omitted, it is inferred from `OsFamily` when possible. |

Common `OsFamily` values:

| Value | Effective family | Typical OS |
| :--- | :--- | :--- |
| `debian` | `debian` | Debian |
| `ubuntu` | `debian` | Ubuntu |
| `rhel` | `rhel` | Red Hat Enterprise Linux |
| `alma` | `rhel` | AlmaLinux |
| `almalinux` | `rhel` | AlmaLinux |
| `rocky` | `rhel` | Rocky Linux |
| `centos` | `rhel` | CentOS / CentOS Stream |
| `oraclelinux` | `rhel` | Oracle Linux |

Troubleshooting:

- If package commands are rejected or wrong, check `OsFamily` and `PackageManager`.
- If the OS is not known yet, start with `debian` or `rhel` only when it matches the target family.

### `Mode` and `Roles`

`Mode` is a compatibility key read as a role expression.
Supported policy roles:

| Role | Description |
| :--- | :--- |
| `ReadOnly` | Read-oriented diagnostics and listing. |
| `Safe` | Default safe role. Blocks dangerous changes, secret display, sudo, delete, move, and install operations. |
| `Maintenance` | Maintenance-oriented role for package and service work. |
| `Expert` | Stronger CLI permissions. MCP still blocks secret exposure. |
| `WebUser` | Allows web-root read/list/write/cd based on web public roots. |
| `WebAdmin` | Allows selected Nginx and web-server administration commands. |

Examples:

```json
{
  "Mode": "Safe"
}
```

```json
{
  "Mode": "Safe|WebUser"
}
```

Troubleshooting:

- If path operations are denied, `Mode` alone is not enough. Configure `AllowedRoots`.
- If MCP ignores a CLI-only permission, check whether it is a `Capabilities` value.

### `Capabilities`

CLI-only override flags.
MCP execution ignores `Capabilities` and evaluates mode-based permissions only.

Examples:

```json
{
  "Capabilities": [
    "AllowListPackage"
  ]
}
```

```json
{
  "Capabilities": "AllowListPackage|AllowInstallPackage"
}
```

Common values:

- `AllowAlias`
- `AllowSudo`
- `AllowShowPassword`
- `AllowShowPrivateKey`
- `AllowListPackage`
- `AllowUpdatePackageIndex`
- `AllowInstallPackage`
- `AllowRemovePackage`
- `AllowDeleteFiles`
- `AllowMoveFiles`
- `AllowMoveDirectory`

Troubleshooting:

- Unknown names are configuration errors.
- Do not use `Capabilities` to grant MCP permissions; MCP intentionally ignores them.

### `Rights`

Named access presets used by `AllowedRoots`.

Example:

```json
{
  "Rights": {
    "$WebDeploy": "$ReadWrite|@Import",
    "$LogRead": "$ReadOnly"
  }
}
```

Rules:

- User-defined names must start with `$`.
- Built-in names are `$ReadOnly`, `$ReadWrite`, and `$ALL`.
- Built-in names cannot be overridden.
- Names are case-insensitive.

### `AllowedRoots`

Path or glob rules for path-based operations.
If omitted or empty, path-based operations are not allowed by policy.

Example:

```json
{
  "AllowedRoots": {
    "/var/www": "$WebDeploy",
    "/var/log": "$LogRead",
    "/home/*": "@Read|@List",
    "/opt/apps/**": "@Read|@List|@CD"
  }
}
```

Access flags:

| Flag | Description |
| :--- | :--- |
| `@Read` | Allows reading file content. |
| `@List` | Allows listing files or directories. |
| `@Write` | Allows write/edit/delete/move candidates. |
| `@Import` | Allows local-to-remote import/upload candidates. |
| `@Export` | Allows remote-to-local export/download candidates. |
| `@CD` | Allows change-directory operations. |

Built-in presets:

| Preset | Meaning |
| :--- | :--- |
| `$ReadOnly` | `@Read|@List|@CD` |
| `$ReadWrite` | `$ReadOnly|@Write` |
| `$ALL` | `@Read|@List|@Write|@Import|@Export|@CD` |

Glob rules:

- `*` matches one path segment.
- `**` matches any depth.
- A single `*` or `**` value is explicit global permission.
- Regular expressions are not supported.

Troubleshooting:

- `AllowedRoots` written as an array gives read-only compatibility behavior. Use object form for new profiles.
- Bare values such as `Read` or `Write` are invalid. Use `@Read` or `@Write`.
- Named presets must start with `$`.

### `SpecialPaths`

Additional path rules inside `AllowedRoots`.

Example:

```json
{
  "SpecialPaths": {
    "**/.env": "Deny",
    "**/.ssh/**": "Deny",
    "**/.htaccess": "Confirm",
    "/var/www/.well-known/**": "Allow"
  }
}
```

| Value | Description |
| :--- | :--- |
| `Deny` | Denies read, write, and delete operations. |
| `Confirm` | Allows the path to become an operation candidate but requires stronger confirmation. |
| `Allow` | Uses normal `AllowedRoots`, `Mode`, and `Capabilities` evaluation. |

Troubleshooting:

- If `.env` or `.ssh` access is denied even under an allowed root, check `SpecialPaths`.
- Use `Allow` only for intentional exceptions.

### `Users` and `DefaultUser`

Use `Users` when one server profile has multiple SSH login users.
`DefaultUser` is used when commands do not specify a user.

Example:

```json
{
  "Host": {
    "Address": "203.0.113.10",
    "Port": 22
  },
  "Auth": {
    "Method": "privateKey",
    "PrivateKeyFile": "vps01_ed25519"
  },
  "DefaultUser": "deploy",
  "Users": {
    "deploy": {
      "Mode": "Safe|WebUser",
      "AllowedRoots": {
        "/var/www": "@Read|@Write|@List|@CD"
      }
    },
    "readonly": "ReadOnly"
  },
  "Platform": {
    "OsFamily": "alma"
  }
}
```

Notes:

- The recommended `Users` format is an object.
- Object keys are SSH user names.
- String values are role expressions.
- Detailed object values can override mode, roles, auth, allowed roots, and special paths.
- Shared `Auth` values are inherited by users unless overridden.

Troubleshooting:

- If a profile has multiple users and no default user, select the user explicitly or set `DefaultUser`.
- Duplicate user names are configuration errors.

### `Services`

Service-specific defaults.
Currently, `Nginx` settings are supported.

Example:

```json
{
  "Services": {
    "Nginx": {
      "User": "nginx",
      "Group": "nginx",
      "Port": 80,
      "Root": "/var/www/example"
    }
  }
}
```

| Field | Required | Description |
| :--- | :---: | :--- |
| `Services.Nginx.User` | no | Nginx worker user. |
| `Services.Nginx.Group` | no | Nginx worker group. |
| `Services.Nginx.Port` | no | Nginx listen port. Must be 1 to 65535. |
| `Services.Nginx.Root` | no | Web public root. Also used by `WebUser` role. |

## Validation Checklist

Before connecting:

- `Host.Address` is not `example.invalid`.
- `Auth.UserName` or `Users` contains the correct SSH user.
- Direct `root` login is not used.
- `Auth.Method` is `privateKey` or `password`.
- Private key profiles have a real key under `KelpieHome\keys`.
- Password profiles have `Auth.PasswordSecretName`, not a plain text password.
- `Platform.OsFamily` matches the target OS.
- `AllowedRoots` includes only paths you intend KelpieSSH to operate on.
- Sensitive paths are denied with `SpecialPaths`.

## Troubleshooting

### `SSH profile name is required`

The profile file name could not be resolved.
Use a file name such as `vps01.json` under `KelpieHome\profiles`.

### `SSH host is required`

Set `Host.Address`.
Do not leave it blank.

### `SSH user name is required`

Set `Auth.UserName`, or configure `Users` and `DefaultUser`.

### `SSH private key path is required`

For `privateKey` authentication, set `Auth.PrivateKeyFile`.
Place relative key files under `KelpieHome\keys`.

### `SSH password secret name is required`

For `password` authentication, set `Auth.PasswordSecretName`.
Then provide the actual password with `kelpie login` or `kelpiemcp password <profile>`.

### `SSH package manager is required`

Set `Platform.OsFamily` to a known family or set `Platform.PackageManager` explicitly.

### Path operation is denied

Check:

- `AllowedRoots` is present and uses object form.
- The target path is under an allowed root.
- The access flags include the requested operation.
- `SpecialPaths` is not denying the path.

### MCP does not allow a command that CLI allows

MCP ignores `Capabilities`.
Use `Mode`, roles, `AllowedRoots`, and supported MCP tools instead.

### Password login works in CLI but not MCP

CLI and MCP password sessions are separate.
For MCP, start the server and register the password:

```powershell
kelpiemcp start
kelpiemcp password vps01
```

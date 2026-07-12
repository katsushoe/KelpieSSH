# KelpieSSH Profile Guide

Last updated: 2026-07-05

This guide explains how to configure SSH profiles for KelpieSSH.
For Japanese documentation, see [docs/ja/PROFILE_GUIDE.ja.md](docs/ja/PROFILE_GUIDE.ja.md).

General configuration files such as `kelpie.json` and `kelpiemcp.json` are documented in [CONFIG.md](CONFIG.md).

## Profile Safety Responsibilities

Profiles define what KelpieSSH may connect to and what remote paths, users, policies, services, and web roots it may operate on. Treat profile edits as security-sensitive changes.

Use profiles only for systems you own or are authorized to manage. Review the target host, SSH user, authentication settings, mode, allowed roots, special path rules, web public sites, and writable executable extensions before trusting or reloading a profile.

Before using a profile against production systems, test the same profile shape in a safe environment and keep restorable backups for important servers and data. KelpieSSH policy checks reduce risk, but they do not replace operator review, backups, or recovery planning.

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

Terminal CLI commands read profile files for each command flow. `KelpieMCPServer` keeps profiles in an in-memory catalog while it is running. After editing profile JSON files for MCP usage, run `kelpiemcp profile reload <profile>` to update both the trust store and the in-memory profile catalog. The `profile_reload` MCP tool does not update trusted profile hashes and is not the acceptance path for intentional profile file edits.

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

## Host Key Pinning

Set `Host.HostKeyFingerprintSha256` to pin the SSH server host key fingerprint.
When the value is configured, KelpieSSH compares the received SSH host key fingerprint with the profile value before trusting the connection.

```json
{
  "Host": {
    "Address": "203.0.113.10",
    "Port": 22,
    "HostKeyFingerprintSha256": "SHA256:abc123"
  }
}
```

To record a fingerprint interactively, use:

```powershell
kelpie profile trust-host-key vps01
```

Only trust the displayed fingerprint after verifying it through a trusted channel such as the VPS provider console.
If the first SSH connection is intercepted, recording that fingerprint can pin an attacker's host key.

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

After opening the profile, `kelpie login` asks for the password and keeps it only for the current CLI login process:

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
  "WebPublicSites": {
    "default": {
      "Root": "/var/www/example",
      "WritableExecutableExtensions": [".php"]
    }
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

### Profile schema summary

| Field | Required | Type | Initial value | Values / constraints |
| :--- | :---: | :--- | :--- | :--- |
| `Host` | yes | object | none | SSH endpoint settings. |
| `Host.Address` | yes | string | none | Host name or IP address. Must not be empty. |
| `Host.Port` | no | integer | `22` | SSH port, normally `1` to `65535`. |
| `Host.HostKeyFingerprintSha256` | no | string | none | Pinned SSH server host key SHA256 fingerprint. Use `kelpie profile trust-host-key <profile>` or verify and enter it manually. |
| `Auth` | yes unless `Authentication` is used | object | none | Short alias for `Authentication`. Used by samples. |
| `Authentication` | yes unless `Auth` is used | object | none | Formal authentication section. Takes priority over `Auth` when both are present. |
| `Auth.UserName` / `Authentication.UserName` | yes for single-user profiles | string | none | SSH login user. Direct `root` login is rejected. |
| `Auth.UsrName` / `Authentication.UsrName` | no | string | none | Compatibility typo alias for `UserName`. Prefer `UserName`. |
| `Auth.Method` / `Authentication.Method` | yes | string enum | `privateKey` | `privateKey`: use a private key. `password`: use runtime password session by `PasswordSecretName`. |
| `Auth.PrivateKeyFile` / `Authentication.PrivateKeyFile` | yes for `privateKey` | string | none | File name under `KelpieHome\keys`, or an absolute path. |
| `Auth.PrivateKeyPath` / `Authentication.PrivateKeyPath` | no | string | none | Compatibility path. Prefer `PrivateKeyFile`. |
| `Auth.PrivateKeyPassphrase` / `Authentication.PrivateKeyPassphrase` | no | string or null | `null` | Optional private key passphrase. Do not write real secrets into public samples. |
| `Auth.PasswordSecretName` / `Authentication.PasswordSecretName` | yes for `password` | string or null | `null` | Secret reference name. Plain text passwords are not allowed in profiles. |
| `Connection` | no | object | `{ "TimeoutSeconds": 10 }` | SSH connection behavior. |
| `Connection.TimeoutSeconds` | no | integer | `10` | Must be positive. |
| `Platform` | yes | object | none | Target OS metadata used to select providers. |
| `Platform.OsFamily` | yes | string enum/alias | none | `debian`, `ubuntu`, `rhel`, `alma`, `almalinux`, `rocky`, `centos`, `oraclelinux`. Aliases resolve to an effective family. |
| `Platform.PackageManager` | no | string | inferred from `OsFamily` | `apt` for effective `debian`, `dnf` for effective `rhel`, or an explicit package manager name. |
| `Mode` | no | string role expression | `Safe` | `ReadOnly`, `Safe`, `Maintenance`, `Expert`, `WebUser`, `WebAdmin`, combined with `|`. Compatibility key read as roles. |
| `Roles` | no | string or string array | derived from `Mode` | Same role names as `Mode`. If set, it participates in role resolution. |
| `Capabilities` | no | string, string array, or object | empty | CLI-only policy flags. MCP ignores this section. See [`Capabilities`](#capabilities). |
| `Rights` | no | dictionary object | built-ins only | Keys are `$`-prefixed names. Values are access expressions using presets or `@` flags. |
| `AllowedRoots` | no | dictionary object or string array | empty | Object form maps path/glob to access expression. Array form is compatibility read-only/list/cd. |
| `SpecialPaths` | no | dictionary object | empty | Keys are path globs. Values are `Deny`, `Confirm`, or `Allow`. |
| `EnvironmentValues` | no | dictionary object | empty | Keys are environment variable names. Values are environment access expressions. |
| `DefaultUser` | no | string | `Auth.UserName` | User selected when `Users` has multiple entries and no command-level user is specified. |
| `Users` | no | dictionary object or array | single legacy user | Recommended object form maps SSH user name to role expression or detailed user object. |
| `Users.<user>` | no | string or object | inherits profile settings | String value is a role expression. Object value can override auth, roles, roots, special paths, environment values, and web public sites. |
| `Users.<user>.Method` | no | string enum | profile auth method | `privateKey` or `password`. |
| `Users.<user>.PrivateKeyFile` | no | string | profile auth value | User-level private key file override. |
| `Users.<user>.PrivateKeyPath` | no | string | profile auth value | Compatibility user-level private key path. Prefer `PrivateKeyFile`. |
| `Users.<user>.PrivateKeyPassphrase` | no | string or null | profile auth value | User-level private key passphrase override. |
| `Users.<user>.PasswordSecretName` | no | string or null | profile auth value | User-level password secret reference override. |
| `Users.<user>.Mode` | no | string role expression | profile roles | Same values as profile `Mode`. |
| `Users.<user>.Roles` | no | string or string array | profile roles | Same values as profile `Roles`. |
| `Users.<user>.Capabilities` | no | string, string array, or object | profile capabilities | CLI-only user-level policy flags. |
| `Users.<user>.AllowedRoots` | no | dictionary object or string array | profile allowed roots | Same format as profile `AllowedRoots`. |
| `Users.<user>.SpecialPaths` | no | dictionary object | profile special paths | Same format as profile `SpecialPaths`. |
| `Users.<user>.EnvironmentValues` | no | dictionary object | profile environment rules | Same format as profile `EnvironmentValues`. |
| `Users.<user>.WebPublicSites` | no | dictionary object or array | profile web public sites | Same format as profile `WebPublicSites`. |
| `Services` | no | object | empty object | Service-specific defaults. |
| `Services.Nginx` | no | object | empty object | Nginx defaults used by Nginx and web helpers. |
| `Services.Nginx.User` | no | string | none | Nginx worker user. |
| `Services.Nginx.Group` | no | string | none | Nginx worker group. |
| `Services.Nginx.Port` | no | integer | none | Must be `1` to `65535` when set. |
| `Services.Nginx.Root` | no | string | none | Web public root. Also used by the `WebUser` role when `WebPublicSites` is not configured. |
| `WebPublicSites` | no | dictionary object or array | provider default site | Provider default site is `default` at `/var/www/html` with safe static extensions. |
| `WebPublicSites.<siteKey>.SiteKey` | no in object form | string | dictionary key | Required for array items. Must not be empty. |
| `WebPublicSites.<siteKey>.DisplayName` | no | string | `siteKey` | Human-readable site label. |
| `WebPublicSites.<siteKey>.Root` / `RootPath` | yes | string | none | Safe absolute Unix web root path. `RootPath` is the alias; prefer `Root` in samples. |
| `WebPublicSites.<siteKey>.AllowedExtensions` | no | string array | built-in safe static extensions | Effective values are explicit single file extensions with a leading dot, matched case-insensitively, such as `.html` or `.png`. Use normal web asset extensions only. Do not use paths, globs, MIME types, or executable extensions. |
| `WebPublicSites.<siteKey>.WritableExecutableExtensions` | no | string array | empty | Dot-prefixed explicit executable extensions such as `.php`. Wildcards and path separators are rejected. |
| `WebPublicSites.<siteKey>.AllowedContentTypes` | no | string array or dictionary object | built-in safe content types | Array grants read/write. Object maps MIME type to access expression. |
| `WebPublicSites.<siteKey>.AllowedFiles` | no | dictionary object | empty | Keys are file globs, `file:<glob>`, or `mime:<content-type>`. Values are access expressions. |
| `WebPublicSites.<siteKey>.CreateDirectories` | no | boolean | `true` | Allows missing parent directories to be created by web write operations. |
| `WebPublicSites.<siteKey>.MaxReadBytes` | no | integer | `5242880` | Maximum bytes read by web file read operations. |
| `WebPublicSites.<siteKey>.MaxWriteBytes` | no | integer | `5242880` | Maximum bytes accepted by web file write operations. |
| `Ssh` | no | object | empty object | Legacy endpoint/auth section. Prefer `Host` and `Auth` / `Authentication`. |
| `Ssh.Host` | no | string | none | Legacy host address. Used only when `Host.Address` is not set. |
| `Ssh.Port` | no | integer | `22` | Legacy SSH port. Used only when `Host.Address` is not set. |
| `Ssh.UserName` | no | string | none | Legacy SSH user. Used only when auth user name is not set. |
| `Ssh.Authentication` | no | object | empty object | Legacy authentication section. Lowest priority. |
| `Policy` | no | object | empty object | Legacy CLI policy section. Prefer `Capabilities` and `AllowedRoots`. |
| `Policy.Level` | no | string | empty | Legacy capability expression. |
| `Policy.AllowedRoots` | no | string array | empty | Legacy read-only/list/cd allowed roots. |

Compatibility and priority:

- `Authentication` overrides `Auth`; `Auth` overrides legacy `Ssh.Authentication`.
- `Host.Address` / `Host.Port` override legacy `Ssh.Host` / `Ssh.Port`.
- `Auth.UserName` overrides `Auth.UsrName`; both override legacy `Ssh.UserName`.
- User-level settings under `Users.<user>` override profile-level settings for that selected user.
- `Root` and `RootPath` are aliases; samples prefer `Root`.

### Configuration value samples

This section gives at least one valid sample for each value shape used by profile settings.
Samples use documentation-only hosts, users, key names, and secret references.

Scalar and object settings:

```json
{
  "Host": {
    "Address": "203.0.113.10",
    "Port": 22
  },
  "Auth": {
    "UserName": "deploy",
    "Method": "privateKey",
    "PrivateKeyFile": "vps01_ed25519",
    "PrivateKeyPassphrase": "sample-passphrase"
  },
  "Connection": {
    "TimeoutSeconds": 10
  },
  "Platform": {
    "OsFamily": "ubuntu",
    "PackageManager": "apt"
  },
  "Services": {
    "Nginx": {
      "User": "www-data",
      "Group": "www-data",
      "Port": 80,
      "Root": "/var/www/html"
    }
  }
}
```

Nullable secret fields:

```json
{
  "Auth": {
    "PrivateKeyPassphrase": null,
    "PasswordSecretName": "kelpie:vps01"
  }
}
```

Role expression samples:

```json
{
  "Mode": "Maintenance|WebUser",
  "Roles": "Maintenance|WebUser"
}
```

```json
{
  "Roles": ["Maintenance", "WebUser"]
}
```

Capability samples:

```json
{
  "Capabilities": "AllowListPackage|AllowInstallPackage"
}
```

```json
{
  "Capabilities": ["AllowListPackage", "AllowInstallPackage"]
}
```

```json
{
  "Capabilities": {
    "Flags": ["AllowListPackage", "AllowInstallPackage"]
  }
}
```

Dictionary and array samples:

```json
{
  "Rights": {
    "$WebDeploy": "$ReadWrite|@Import"
  },
  "AllowedRoots": {
    "/var/www": "$WebDeploy",
    "/var/log": "$ReadOnly"
  },
  "SpecialPaths": {
    "**/.env": "Deny",
    "/var/www/.well-known/**": "Allow"
  },
  "EnvironmentValues": {
    "PATH": "Read",
    "APP_ENV": "Read|Write"
  }
}
```

```json
{
  "AllowedRoots": ["/var/log", "/etc/nginx"]
}
```

User samples:

```json
{
  "DefaultUser": "deploy",
  "Users": {
    "deploy": "Maintenance|WebUser",
    "readonly": {
      "Mode": "ReadOnly",
      "AllowedRoots": {
        "/var/log": "$ReadOnly"
      }
    }
  }
}
```

```json
{
  "Users": [
    {
      "UserName": "deploy",
      "Mode": "Safe"
    }
  ]
}
```

Web public site samples:

```json
{
  "WebPublicSites": {
    "default": "/var/www/html"
  }
}
```

```json
{
  "WebPublicSites": {
    "default": {
      "Root": "/var/www/html",
      "AllowedExtensions": [".html", ".css", ".js"],
      "WritableExecutableExtensions": [".php"],
      "AllowedContentTypes": ["text/html", "text/css"],
      "AllowedFiles": {
        "file:assets/**": "$ReadWrite",
        "mime:image/png": "$ReadOnly"
      },
      "CreateDirectories": true,
      "MaxReadBytes": 1048576,
      "MaxWriteBytes": 1048576
    }
  }
}
```

```json
{
  "WebPublicSites": [
    {
      "SiteKey": "default",
      "DisplayName": "Default site",
      "Root": "/var/www/html"
    }
  ]
}
```

`AllowedContentTypes` object form:

```json
{
  "WebPublicSites": {
    "default": {
      "Root": "/var/www/html",
      "AllowedContentTypes": {
        "text/html": "$ReadWrite",
        "image/png": "$ReadOnly"
      }
    }
  }
}
```

Legacy compatibility samples:

```json
{
  "Ssh": {
    "Host": "203.0.113.10",
    "Port": 22,
    "UserName": "deploy",
    "Authentication": {
      "Method": "privateKey",
      "PrivateKeyFile": "vps01_ed25519"
    }
  },
  "Policy": {
    "Level": "AllowListPackage",
    "AllowedRoots": ["/var/log"]
  }
}
```

### `Host`

Target SSH endpoint.

| Field | Required | Initial value | Description |
| :--- | :---: | :--- | :--- |
| `Host.Address` | yes | none | Host name or IP address. |
| `Host.Port` | no | `22` | SSH port. |

Troubleshooting:

- If `Host.Address` is still `example.invalid`, edit it before connecting.
- If the server uses a non-standard SSH port, set `Host.Port`.

### `Auth` / `Authentication`

SSH authentication settings.
`Authentication` is the formal name.
`Auth` is the short alias used by samples.
If both are present, `Authentication` takes priority.

| Field | Required | Initial value | Description |
| :--- | :---: | :--- | :--- |
| `Auth.UserName` | yes for single-user profiles | none | SSH login user. Direct `root` login is rejected. |
| `Auth.Method` | yes | `privateKey` | `privateKey` or `password`. |
| `Auth.PrivateKeyFile` | yes for `privateKey` | none | Private key file name under `KelpieHome\keys`, or an absolute path. |
| `Auth.PrivateKeyPath` | no | none | Compatibility path. Prefer `PrivateKeyFile` for new profiles. |
| `Auth.PrivateKeyPassphrase` | no | `null` | Private key passphrase. Do not expose it in logs or public files. |
| `Auth.PasswordSecretName` | yes for `password` | `null` | Secret reference name. The password itself is entered at runtime. |

Troubleshooting:

- `SSH private key path is required`: set `Auth.PrivateKeyFile` or switch `Auth.Method` to `password`.
- `SSH password secret name is required`: set `Auth.PasswordSecretName`.
- Authentication fails with a private key: confirm the private key file exists under `KelpieHome\keys`, the remote public key is registered, and the SSH user is correct.
- Authentication fails with a password: run `kelpie open <profile>` then `kelpie login`, or register it with `kelpiemcp password <profile>` for the MCP server session.

### `Connection`

Connection behavior.

| Field | Required | Initial value | Description |
| :--- | :---: | :--- | :--- |
| `Connection.TimeoutSeconds` | no | `10` | SSH connection timeout in seconds. |

Troubleshooting:

- If slow servers time out, increase `TimeoutSeconds`.
- If the command hangs for too long, lower `TimeoutSeconds`.

### `Platform`

Target OS metadata used to select safe commands.

| Field | Required | Initial value | Description |
| :--- | :---: | :--- | :--- |
| `Platform.OsFamily` | yes | none | Target OS family or alias. |
| `Platform.PackageManager` | no | inferred from `OsFamily` | `apt`, `dnf`, `yum`, etc. |

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
- `AllowPeekEnvironmentKeys`
- `AllowPeekEnvironmentValues`
- `AllowSetEnvironmentValues`
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

### `EnvironmentValues`

Per-environment-variable handling rules.
`Capabilities` controls whether an environment operation can be called at all.
`EnvironmentValues` controls what is allowed for each environment variable name.

Example:

```json
{
  "Capabilities": "AllowPeekEnvironmentKeys|AllowPeekEnvironmentValues|AllowSetEnvironmentValues",
  "EnvironmentValues": {
    "PATH": "Common|NoLog",
    "LANG": "Common|NoLog",
    "APP_ENV": "Common|SetLog",
    "GITHUB_TOKEN": "PeekSecret|PeekLog",
    "DEPLOY_TOKEN": "Masked|PeekLog",
    "MY_SECRET_KEY": "Hidden"
  }
}
```

Capability gates:

| Capability | Description |
| :--- | :--- |
| `AllowPeekEnvironmentKeys` | Allows listing environment variable names with metadata. |
| `AllowPeekEnvironmentValues` | Allows reading environment variable values when the key rule permits it. |
| `AllowSetEnvironmentValues` | Allows setting environment variable values for one command execution or persisting them to the Kelpie env file when the key rule permits it. |

`EnvironmentValues` rules:

| Rule | Type | Description |
| :--- | :--- | :--- |
| `Common` | alias | Expands to `PeekCommon|SetCommon`. |
| `Secret` | alias | Expands to `PeekSecret|SetSecret`. Loading this rule emits a warning. |
| `Log` | alias | Expands to `PeekLog|SetLog`. `Log` alone is a configuration error. |
| `PeekCommon` | permission | Allows reading a common environment variable value. |
| `SetCommon` | permission | Allows setting a common environment variable value for one command execution. |
| `PeekSecret` | permission | Allows reading a secret environment variable value. Emits warning audit logs when combined with `PeekLog`. |
| `SetSecret` | permission | Allows setting a secret environment variable value for one command execution. Emits stronger warnings when configured. |
| `Hidden` | control | Hides the key name, existence, value, and set capability. Takes priority over all other rules. |
| `Masked` | control | Shows the key name, existence, value length, and a masked value only. The real value is never returned. |
| `KeyOnly` | control | Shows only the key name. Value read and set are not allowed. |
| `PeekLog` | audit | Writes a warning audit log when the value is read or masked. |
| `SetLog` | audit | Writes a warning audit log when the value is set. |
| `NoLog` | audit | Suppresses normal access logs. Warning, denied, and configuration-error logs are not suppressed. |

Default handling:

- If a key is not listed in `EnvironmentValues`, `get_environment_keys` may show the key when `AllowPeekEnvironmentKeys` is present.
- If a key is not listed in `EnvironmentValues`, its value cannot be read and cannot be set.
- `EnvironmentValues` is therefore a value-access and set allowlist, not the only source for key listing.

Persistent environment file:

- `kelpie env persist` and `persist_environment_value` write to the remote user's `~/.kelpie/.env`.
- `kelpie env remove` and `remove_persistent_environment_value` remove keys from the same file.
- Kelpie creates a timestamped backup before writing, such as `~/.kelpie/.env.20260617T120000Z.kelpie`.
- The file uses shell-compatible assignments such as `APP_ENV='production'`.
- `kelpie env set` sources `~/.kelpie/.env` before applying its one-command override.
- Cron jobs, shell startup files, or service wrappers must explicitly source `~/.kelpie/.env` for persisted values to take effect.
- Existing processes are not updated automatically.

Control rule behavior:

- `Hidden` makes the variable appear unavailable. It should not be combined with any other rule.
- `Masked` is useful when operators need to confirm that a value exists and has the expected length without exposing it.
- `KeyOnly` is useful when operators intentionally allow only the key name to appear; this is distinguishable from an unconfigured key in metadata and audit logs.

Configuration errors:

- `Log`, `PeekLog`, `SetLog`, or `NoLog` by itself.
- Combining `Hidden` with any other rule.
- Combining `KeyOnly` with peek or set permissions.
- Combining `Masked` with real-value peek or set permissions.
- Combining `Common` and `Secret` for the same key.

Logging rules:

- Environment variable values must never be written to logs.
- `Secret`, `PeekSecret`, and `SetSecret` configuration should emit warning logs.
- `PeekLog` and `SetLog` emit warning audit logs for matching operations.
- `NoLog` suppresses normal access logs only.

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

| Field | Required | Initial value | Description |
| :--- | :---: | :--- | :--- |
| `Services.Nginx.User` | no | none | Nginx worker user. |
| `Services.Nginx.Group` | no | none | Nginx worker group. |
| `Services.Nginx.Port` | no | none | Nginx listen port. Must be 1 to 65535. |
| `Services.Nginx.Root` | no | none | Web public root. Also used by `WebUser` role. |

### `WebPublicSites`

`WebPublicSites` defines the web public roots that MCP web file tools may access.
Executable extensions such as `.php`, `.cgi`, `.py`, `.sh`, and `.exe` are denied for writing by default.

Use `WritableExecutableExtensions` only when the profile owner explicitly allows deployment of executable web files for that site.

Child settings:

- [`WebPublicSites.<siteKey>`](#webpublicsitessitekey)
- [`WebPublicSites.<siteKey>.SiteKey`](#webpublicsitessitekeysitekey)
- [`WebPublicSites.<siteKey>.DisplayName`](#webpublicsitessitekeydisplayname)
- [`WebPublicSites.<siteKey>.Root` / `RootPath`](#webpublicsitessitekeyroot--rootpath)
- [`WebPublicSites.<siteKey>.AllowedExtensions`](#webpublicsitessitekeyallowedextensions)
- [`WebPublicSites.<siteKey>.WritableExecutableExtensions`](#webpublicsitessitekeywritableexecutableextensions)
- [`WebPublicSites.<siteKey>.AllowedContentTypes`](#webpublicsitessitekeyallowedcontenttypes)
- [`WebPublicSites.<siteKey>.AllowedFiles`](#webpublicsitessitekeyallowedfiles)
- [`WebPublicSites.<siteKey>.CreateDirectories`](#webpublicsitessitekeycreatedirectories)
- [`WebPublicSites.<siteKey>.MaxReadBytes`](#webpublicsitessitekeymaxreadbytes)
- [`WebPublicSites.<siteKey>.MaxWriteBytes`](#webpublicsitessitekeymaxwritebytes)

Example:

```json
{
  "Users": {
    "deploy": {
      "Mode": "Maintenance|WebUser|WebAdmin",
      "WebPublicSites": {
        "default": {
          "Root": "/var/www/html",
          "AllowedExtensions": [".html", ".css", ".js", ".png", ".jpg", ".txt"],
          "WritableExecutableExtensions": [".php"]
        }
      }
    }
  }
}
```

| Field | Required | Initial value | Description |
| :--- | :---: | :--- | :--- |
| `WebPublicSites.<siteKey>.Root` / `RootPath` | yes | none | Web public root for the site. |
| `WebPublicSites.<siteKey>.AllowedExtensions` | no | built-in safe static extensions | Regular file extensions allowed for this site. Effective values are explicit single file extensions with a leading dot, such as `.html` or `.png`. Matching is case-insensitive. Do not specify paths, globs, MIME types, or executable extensions here. |
| `WebPublicSites.<siteKey>.WritableExecutableExtensions` | no | empty | Executable extensions allowed for writes on this site only. Values must be explicit dot-prefixed extensions such as `.php`; wildcards are rejected. |
| `WebPublicSites.<siteKey>.AllowedContentTypes` | no | built-in safe content types | MIME content types allowed for this site. Array form grants read/write; object form maps MIME type to an access expression. |
| `WebPublicSites.<siteKey>.AllowedFiles` | no | empty | File-specific allow rules. Keys are file globs, `file:<glob>`, or `mime:<content-type>`; values are access expressions. |
| `WebPublicSites.<siteKey>.CreateDirectories` | no | `true` | Allows missing parent directories to be created during write operations. |
| `WebPublicSites.<siteKey>.MaxReadBytes` | no | `5242880` | Maximum bytes returned by web file read operations. |
| `WebPublicSites.<siteKey>.MaxWriteBytes` | no | `5242880` | Maximum bytes accepted by web file write operations. |

#### `WebPublicSites.<siteKey>`

Description:

Site entry under `WebPublicSites`.

Type:

- dictionary object value
- array item object
- compatibility string value for a root path

Default and omitted behavior:

If `WebPublicSites` is omitted, Kelpie uses the provider default site when available. The built-in web public default is site key `default` with root `/var/www/html`.

Allowed values and constraints:

- Object-form keys are site keys.
- Array-form items must set `SiteKey`.
- String-form values are treated as root paths and should be used only for compatibility.

Sample:

```json
{
  "WebPublicSites": {
    "default": {
      "Root": "/var/www/html"
    }
  }
}
```

#### `WebPublicSites.<siteKey>.SiteKey`

Description:

Site identifier used when `WebPublicSites` is written as an array.

Type:

- string

Default and omitted behavior:

In object form, the dictionary key is used as the site key. In array form, `SiteKey` is required.

Allowed values and constraints:

- Must not be empty.
- Use a stable identifier such as `default`, `public`, or `admin`.
- Do not put path separators or secrets in the key.

Sample:

```json
{
  "WebPublicSites": [
    {
      "SiteKey": "default",
      "Root": "/var/www/html"
    }
  ]
}
```

#### `WebPublicSites.<siteKey>.DisplayName`

Description:

Human-readable label for a site.

Type:

- string

Default and omitted behavior:

If omitted, the site key is used as the display label.

Allowed values and constraints:

- Any non-secret display text.
- Do not include real credentials, private host names that should not be public, or secrets.

Sample:

```json
{
  "WebPublicSites": {
    "default": {
      "DisplayName": "Default site",
      "Root": "/var/www/html"
    }
  }
}
```

#### `WebPublicSites.<siteKey>.Root` / `RootPath`

Description:

Absolute Unix path to the public web root for the site. `RootPath` is a compatibility alias; use `Root` for new profiles.

Type:

- string

Default and omitted behavior:

The field is required for explicit site entries. The provider default site uses `/var/www/html`.

Allowed values and constraints:

- Must be a safe absolute Unix path.
- Must not use traversal segments.
- Must point to the web root intended for MCP web file operations.

Sample:

```json
{
  "WebPublicSites": {
    "default": {
      "Root": "/var/www/html"
    }
  }
}
```

#### `WebPublicSites.<siteKey>.AllowedExtensions`

Description:

Regular file extensions allowed for the site.

Type:

- string array

Default and omitted behavior:

If omitted or empty, Kelpie uses the built-in safe static-file extension list.

Allowed values and constraints:

- Values must be explicit single file extensions with a leading dot, such as `.html` or `.png`.
- Matching is case-insensitive.
- Do not specify paths, globs, MIME types, or executable extensions.
- This setting is for normal web assets such as HTML, CSS, JavaScript, images, text, JSON, XML, and archives.
- Built-in safe static extensions are `.html`, `.htm`, `.css`, `.js`, `.mjs`, `.txt`, `.json`, `.xml`, `.png`, `.jpg`, `.jpeg`, `.webp`, `.gif`, `.svg`, `.ico`, `.zip`, `.gz`, `.tgz`, `.tar`, `.bz2`, `.xz`, and `.br`.

Sample:

```json
{
  "WebPublicSites": {
    "default": {
      "Root": "/var/www/html",
      "AllowedExtensions": [".html", ".css", ".js", ".png", ".jpg", ".txt"]
    }
  }
}
```

#### `WebPublicSites.<siteKey>.WritableExecutableExtensions`

Description:

Executable web file extensions allowed for writes on this site only.

Type:

- string array

Default and omitted behavior:

Unset or empty `WritableExecutableExtensions` keeps the default executable-file write denial.

Allowed values and constraints:

- Values must be explicit dot-prefixed extensions such as `.php`.
- Wildcards and path separators are rejected.
- Extensions denied by default as executable or binary code are `.php`, `.cgi`, `.pl`, `.py`, `.rb`, `.sh`, `.bash`, `.exe`, `.dll`, `.so`, `.jar`, and `.war`.
- If an extension is listed here, write checks do not require the same extension to be listed in `AllowedExtensions`.
- The setting applies only to write checks. Read checks, traversal denial, dotfile denial, secret file denial, size limits, and content type checks still apply.
- The setting is site-local. Other sites and other profiles keep the default denial unless they also opt in.

Sample:

```json
{
  "WebPublicSites": {
    "default": {
      "Root": "/var/www/html",
      "WritableExecutableExtensions": [".php"]
    }
  }
}
```

#### `WebPublicSites.<siteKey>.AllowedContentTypes`

Description:

MIME content types allowed for the site.

Type:

- string array
- dictionary object

Default and omitted behavior:

If omitted or empty, Kelpie uses its built-in safe content type rules.

Allowed values and constraints:

- Array form grants read/write for each MIME type.
- Object form maps MIME type keys to access expressions.
- MIME type keys must be explicit content types such as `text/html` or `image/png`.
- Do not use file extensions, paths, or globs as MIME type keys.

Array sample:

```json
{
  "WebPublicSites": {
    "default": {
      "Root": "/var/www/html",
      "AllowedContentTypes": ["text/html", "text/css"]
    }
  }
}
```

Object sample:

```json
{
  "WebPublicSites": {
    "default": {
      "Root": "/var/www/html",
      "AllowedContentTypes": {
        "text/html": "$ReadWrite",
        "image/png": "$ReadOnly"
      }
    }
  }
}
```

#### `WebPublicSites.<siteKey>.AllowedFiles`

Description:

File-specific allow rules for the site.

Type:

- dictionary object

Default and omitted behavior:

If omitted or empty, no file-specific allowlist is applied and extension/content-type rules decide access.

Allowed values and constraints:

- Keys are file globs, `file:<glob>`, or `mime:<content-type>`.
- Values are access expressions such as `$ReadOnly`, `$ReadWrite`, or `@Read|@List`.
- File glob rules are evaluated relative to the site root.
- Use this when a site needs tighter file-level control than extension rules.

Sample:

```json
{
  "WebPublicSites": {
    "default": {
      "Root": "/var/www/html",
      "AllowedFiles": {
        "file:assets/**": "$ReadWrite",
        "mime:image/png": "$ReadOnly"
      }
    }
  }
}
```

#### `WebPublicSites.<siteKey>.CreateDirectories`

Description:

Controls whether write operations may create missing parent directories.

Type:

- boolean

Default and omitted behavior:

If omitted, the value is `true`.

Allowed values and constraints:

- `true`: web write operations may create missing parent directories.
- `false`: parent directories must already exist.

Sample:

```json
{
  "WebPublicSites": {
    "default": {
      "Root": "/var/www/html",
      "CreateDirectories": true
    }
  }
}
```

#### `WebPublicSites.<siteKey>.MaxReadBytes`

Description:

Maximum number of bytes returned by web file read operations for this site.

Type:

- integer

Default and omitted behavior:

If omitted, the value is `5242880`.

Allowed values and constraints:

- Must be a positive integer.
- Use a smaller value for tighter read limits.

Sample:

```json
{
  "WebPublicSites": {
    "default": {
      "Root": "/var/www/html",
      "MaxReadBytes": 1048576
    }
  }
}
```

#### `WebPublicSites.<siteKey>.MaxWriteBytes`

Description:

Maximum number of bytes accepted by web file write operations for this site.

Type:

- integer

Default and omitted behavior:

If omitted, the value is `5242880`.

Allowed values and constraints:

- Must be a positive integer.
- Content larger than this value is rejected before writing.

Sample:

```json
{
  "WebPublicSites": {
    "default": {
      "Root": "/var/www/html",
      "MaxWriteBytes": 1048576
    }
  }
}
```

Notes:

- `AllowedExtensions` is for normal web assets such as HTML, CSS, JavaScript, images, text, JSON, XML, and archives. It does not allow executable web extensions such as `.php`.
- Built-in safe static extensions are `.html`, `.htm`, `.css`, `.js`, `.mjs`, `.txt`, `.json`, `.xml`, `.png`, `.jpg`, `.jpeg`, `.webp`, `.gif`, `.svg`, `.ico`, `.zip`, `.gz`, `.tgz`, `.tar`, `.bz2`, `.xz`, and `.br`.
- Extensions denied as executable or binary code are `.php`, `.cgi`, `.pl`, `.py`, `.rb`, `.sh`, `.bash`, `.exe`, `.dll`, `.so`, `.jar`, and `.war`. Put only explicitly approved executable web extensions in `WritableExecutableExtensions`.
- Unset or empty `WritableExecutableExtensions` keeps the default executable-file write denial.
- If an extension is listed in `WritableExecutableExtensions`, write checks do not require the same extension to be listed in `AllowedExtensions`.
- The setting applies only to write checks. Read checks, traversal denial, dotfile denial, secret file denial, size limits, and content type checks still apply.
- The setting is site-local. Other sites and other profiles keep the default denial unless they also opt in.

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
- `WebPublicSites` and `WritableExecutableExtensions` are set only for web roots and executable file types you intentionally allow KelpieSSH to manage.
- The target system is owned by you or you have explicit authorization to manage it.
- Important servers and data have restorable backups before using write, package, service, permission, or configuration operations.
- Production changes have been tested in a safe environment first.

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
Then provide the actual password with `kelpie login` for a CLI interactive session, or `kelpiemcp password <profile>` for the running MCP server session.

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



# KelpieSSH Command-Line Options

This document summarizes command-line options that are useful across common KelpieSSH CLI workflows.
For complete command syntax and return values, see [COMMANDS.md](COMMANDS.md).

## Local Check Commands

Use `kelpie config check` and `kelpie profile check <profile>` before opening an SSH connection. These commands validate local JSON files, runtime directory settings, profile schema, authentication references, provider support, policy entries, users, and pending profile backups.

Examples:

```powershell
kelpie config check
kelpie config check --no-pager
kelpie profile check vps01
kelpie profile check vps01 --no-pager
```

`kelpie config --check` is accepted as a compatibility form of `kelpie config check`.

Example `kelpie config check` output:

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

## Silent Mode Options

Use `--silent` when you want to create a profile template without interactive prompts.
Without overrides, Kelpie writes the default template values.

```powershell
kelpie profile create demo --silent
```

Override template fields by adding options:

```powershell
kelpie profile create demo --silent --host-address: demo
```

Common silent options:

```powershell
kelpie profile create demo --silent `
  --host-address demo.example `
  --port 2222 `
  --ssh-user ops `
  --auth-method password `
  --password-secret-name kelpie:demo `
  --os-family ubuntu `
  --mode ReadOnly
```

Map-style values can be specified with `;` separated `key=value` pairs.
In PowerShell, use single quotes when the value contains `$`.

```powershell
kelpie profile create demo --silent `
  --allowed-root '/srv/www=$ReadWrite;/tmp=$Write' `
  --special-path '**/.env=Deny;**/.tmp=Allow'
```

The older shortcut options are still available:

```powershell
kelpie profile create demo --silent `
  --read-only-root /var/log/nginx `
  --read-write-root /srv/www `
  --deny-pattern '**/.secret'
```

`kelpie init --silent [profile]` is also available for non-interactive initial home creation.

## Dry-Run Options

Use `--dry-run` with profile file operations to preview local changes without writing, deleting, committing, or rolling back profile files.

Examples:

```powershell
kelpie profile create demo --dry-run --host-address: demo
kelpie profile edit demo set Host.Port 2222 --dry-run
kelpie profile delete demo --dry-run
kelpie profile clean "*" --dry-run
kelpie profile commit "vps-*" --dry-run
kelpie profile rollback "vps-*" --dry-run
```

`profile create --dry-run` prints the generated JSON.
`profile edit ... --dry-run` validates the requested edit and prints the JSON that would be written.
Delete, clean, commit, and rollback dry-runs print the files that would be changed.

Editor mode does not support dry-run:

```powershell
kelpie profile edit demo --dry-run
```

Use an explicit edit operation such as `set`, `add-root`, `rm-root`, `add-deny`, or `rm-deny`.

## Pager Options

Kelpie can pause long terminal output one screen at a time for readable commands.
When paging is active, the prompt is:

```text
-- more -- (Return to continue, q to quit)
```

Available options:

| Option | Description |
| :--- | :--- |
| `--pager` | Request paging for supported commands. |
| `--no-pager` | Print all output without paging. |

Supported commands:

```powershell
kelpie config check --pager
kelpie profile check demo --pager
kelpie profile show demo --pager
```

In an interactive terminal, supported commands page output automatically when it is longer than one screen.
When standard input or standard output is redirected, Kelpie prints all output without waiting for input.

## Runtime Directory Options

Kelpie supports directory override options for isolated tests, temporary layouts, and dry-run-style local setup checks.
These options do not make SSH operations harmless by themselves; they redirect local Kelpie configuration, profile, log, key, binary, and data paths so you can test setup commands without touching the normal `KelpieHome`.

Available options:

| Option | Scope | Description |
| :--- | :--- | :--- |
| `--config-dir <dir>` | `kelpie`, `kelpiemcp`, `KelpieMCPServer` | Overrides the directory that contains `kelpie.json` and `kelpiemcp.json`. |
| `--profiles-dir <dir>` | `kelpie`, `kelpiemcp`, `KelpieMCPServer` | Overrides the SSH profile directory. |
| `--logs-dir <dir>` | `kelpie`, `kelpiemcp`, `KelpieMCPServer` | Overrides the log directory and takes priority over `LogDirectory` in config files. |
| `--bin-dir <dir>` | `kelpie`, `kelpiemcp`, `KelpieMCPServer` | Overrides the binary directory used to derive the default `KelpieHome` and to find local helper executables. |
| `--keys-dir <dir>` | `kelpie`, `kelpiemcp`, `KelpieMCPServer` | Overrides the key directory created by `kelpie init`. |
| `--dat-dir <dir>` | `kelpie`, `kelpiemcp`, `KelpieMCPServer` | Overrides the runtime data directory, including the MCP trust store and local state files. |

The options may appear before or after the command name.
For `kelpiemcp start`, the overrides are also passed to the launched `KelpieMCPServer` process.
For `kelpie env set ... -- <command>`, arguments after `--` are treated as the remote command and are not parsed as Kelpie runtime options.

Example:

```powershell
$root = "C:\Tmp\KelpieDryRun"

kelpie `
  --config-dir "$root\config" `
  --profiles-dir "$root\profiles" `
  --logs-dir "$root\logs" `
  --bin-dir "$root\bin" `
  --keys-dir "$root\keys" `
  --dat-dir "$root\dat" `
  init --silent sample

kelpie `
  --profiles-dir "$root\profiles" `
  profile create demo --silent --host-address: demo

kelpie `
  --profiles-dir "$root\profiles" `
  profile show demo
```

`kelpiemcp` accepts the same directory options.
When used with `kelpiemcp start`, the overrides are passed to the launched `KelpieMCPServer` process.

## Transaction Options

Profile editing commands normally create `.kelpie` backup files so changes can be committed or rolled back.
Use `--no-backup` when you want an immediate commit and do not want a backup file.

```powershell
kelpie profile create demo --no-backup
kelpie profile edit demo set Host.Port 2222 --no-backup
kelpie profile delete demo --no-backup
```

Use `--no-backup` only when you are sure you do not need rollback for that operation.

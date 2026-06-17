# KelpieSSH

KelpieSSH is a local MCP server for safely assisting VPS diagnostics and maintenance over SSH.

Japanese documentation is available in [docs/ja/README.ja.md](docs/ja/README.ja.md).

Command details are documented in [COMMANDS.md](COMMANDS.md).

Configuration details are documented in [CONFIG.md](CONFIG.md).

`kelpie` reads `config/kelpie.json`; `kelpiemcp` and `KelpieMCPServer` read `config/kelpiemcp.json`.

Sample configuration files are provided under `config_samples/`:

```text
config_samples/
├─ kelpie.json
├─ kelpiemcp.json
└─ servers/
   └─ vps01.json
```

## Getting Started

Choose the setup path that matches how you want to use KelpieSSH.

### Binary users

#### 1. Installing binary (`.msi`)

For normal use, download the KelpieSSH `.msi` installer from GitHub Releases and run it.

After installation, open a new terminal.

Verify that the command is available:

```powershell
kelpie version
```

Expected output:

```text
kelpie 0.1.4.1
```

#### 2. Initializing Kelpie home and creating a profile

Execute this command in the terminal:

```powershell
kelpie init
```

To create a named SSH profile at initialization time:

```powershell
kelpie init vps01
```

Edit the generated profile before connecting. The profile file is created under:

```text
<KelpieHome>\profiles\vps01.json
```

#### 3. Connecting to server

After editing the profile, open the target server:

```powershell
kelpie open vps01
```

For password-based profiles, sign in after opening the target:

```powershell
kelpie login
```

If Windows shows an unknown publisher or SmartScreen warning, confirm that the MSI was downloaded from the official GitHub Release and compare the published checksum if one is provided.

### Zip distribution users

#### 1. Placing the zip binaries

Extract `KelpieSSH-x.x.x.x-x64.zip` to `D:\Kelpie`. The extracted directory should have this layout:

```text
D:\Kelpie
├─ bin
│  ├─ kelpie.exe
│  ├─ kelpiemcp.exe
│  └─ mcp
│     └─ KelpieMCPServer.exe
├─ config_samples
├─ docs
├─ README.md
├─ COMMANDS.md
└─ CONFIG.md
```

#### 2. Adding `PATH` and verifying commands

Add `D:\Kelpie\bin` to the user `PATH`:

```powershell
$kelpieBin = "D:\Kelpie\bin"
$userPath = [Environment]::GetEnvironmentVariable("Path", "User")
if (($userPath -split ";") -notcontains $kelpieBin) {
  $newUserPath = if ([string]::IsNullOrWhiteSpace($userPath)) { $kelpieBin } else { $userPath.TrimEnd(";") + ";" + $kelpieBin }
  [Environment]::SetEnvironmentVariable("Path", $newUserPath, "User")
}
```

Open a new terminal after updating `PATH`, then run the same command check:

```powershell
kelpie version
kelpiemcp status
```

If you do not want to update `PATH`, keep using full paths such as `D:\Kelpie\bin\kelpie.exe`.

#### 3. Initializing Kelpie home and creating a profile

Execute this command in the terminal:

```powershell
D:\Kelpie\bin\kelpie.exe init
```

With `D:\Kelpie\bin\kelpie.exe`, `kelpie init` creates files under `D:\Kelpie`:

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

To create a named profile at initialization time:

```powershell
D:\Kelpie\bin\kelpie.exe init vps01
```

Edit the generated profile before connecting:

```text
D:\Kelpie\profiles\vps01.json
```

#### 4. Connecting to server

After editing the profile, open the target server:

```powershell
kelpie open vps01
```

For password-based profiles, sign in after opening the target:

```powershell
kelpie login
```

### Developers

Visual Studio is only needed when building KelpieSSH from source.

Build and test the solution:

```powershell
dotnet build
dotnet test
```

Publish the command binaries into a local manual layout:

```powershell
dotnet publish src\KelpieClientCommand\KelpieClientCommand.csproj -c Release -o D:\Kelpie\bin
dotnet publish src\KelpieServerCommand\KelpieServerCommand.csproj -c Release -o D:\Kelpie\bin
dotnet publish src\KelpieMCPServer\KelpieMCPServer.csproj -c Release -o D:\Kelpie\bin\mcp
D:\Kelpie\bin\kelpie.exe init
```

`kelpie init` does not overwrite existing configuration files. Edit the generated host, user, key, and policy values before use.

## MCP server

Start the local MCP server before connecting from Codex.

```powershell
kelpiemcp start
```

Stop it with:

```powershell
kelpiemcp stop
```

Check the local server status with:

```powershell
kelpiemcp status
```

For password-based SSH profiles, store or clear the password in the running server session with:

```powershell
kelpiemcp password vps01
kelpiemcp forget vps01
```

Show Kelpie CLI help or version information with:

```powershell
kelpie init
kelpie init vps01
kelpie help
kelpie --help
kelpie version
kelpie --version
```

Inspect configured SSH profiles with:

```powershell
kelpie profiles
kelpie profile show vps01
kelpie status vps01
```

Run high-level VPS diagnostics or tail service logs with:

```powershell
kelpie diag vps01
kelpie logs vps01 nginx.service
kelpie logs vps01 nginx.service 200
```

At this stage, `kelpie diag` and `kelpie logs` run SSH commands directly from the CLI process and are intended for private-key profiles. `kelpiemcp password` stores password authentication only in the running `KelpieMCPServer` session.

The password is sent to the running `KelpieMCPServer` over the local control pipe and kept only in memory for that server process.

By default, `KelpieMCPServer` listens on port `45432` and exposes the MCP endpoint at:

```text
http://127.0.0.1:45432/mcp
```

The port is configured in `config/kelpiemcp.json`.

## Security

KelpieSSH is designed to start with read-oriented diagnostics and allow-list based SSH command execution.

Do not commit real host names, user names, passwords, passphrases, private keys, or production profile files. Keep real `profiles/*.json`, `keys/`, `dat/`, and `logs/` files outside the public repository.

Password authentication is session-based for the running `KelpieMCPServer` process. Plain text passwords must not be stored in JSON configuration files.

For vulnerability reporting and supported-version guidance, see [SECURITY.md](SECURITY.md).

## License

KelpieSSH is released under the MIT License. See [LICENSE](LICENSE).

Copyright (c) 2026 Akatsukisoft.

The MIT License permits commercial use, modification, redistribution, sublicensing, and sale of KelpieSSH, provided that the copyright notice and permission notice are included in copies or substantial portions of the software.

KelpiePro is planned as a paid closed-source desktop product. KelpiePro may reference KelpieSSH and Kelpie Core libraries as NuGet packages without forking or copying the OSS implementation into the closed-source product repository. This repository remains the upstream source for the OSS implementation and package metadata.

When KelpieSSH packages or binaries are redistributed with KelpiePro, include the KelpieSSH MIT license notice and the third-party notices listed in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) in the installer, application about box, bundled documentation, or an equivalent notices location.

The current runtime dependency review did not identify GPL, AGPL, LGPL, SSPL, Commons Clause, or other non-permissive dependencies in KelpieSSH runtime packages. Review `THIRD_PARTY_NOTICES.md` again whenever package versions are added or updated.

## Codex MCP configuration

Add the Streamable HTTP MCP server URL to the Codex MCP configuration.

```toml
[mcp_servers.kelpie]
url = "http://127.0.0.1:45432/mcp"
```

If `Server:Port` is changed, update the Codex URL to match.

## MCP tools

Current tools:

- `kelpie_ping`
- `get_system_info`
- `get_disk_usage`
- `get_memory_usage`
- `get_listening_ports`
- `ssh_run_allowed_command`
- `get_target_inventory`
- `ssh_get_system_info`
- `ssh_get_disk_usage`
- `ssh_get_memory_usage`
- `ssh_get_listening_ports`
- `ssh_get_failed_services`
- `ssh_tail_log`

SSH tool results keep the raw `StandardOutput` / `StandardError` strings and also expose line arrays:

- `Stdout` / `Stderr`: output split by line, preserving ANSI escape sequences.
- `StdoutPlain` / `StderrPlain`: output split by line after ANSI escape sequences are removed.

## SSH profiles

SSH connection profiles are configured as one JSON file per server under `KelpieHome/profiles`.

Runtime configuration does not set a default SSH profile. Specify the profile explicitly with commands such as `kelpie open vps01` or MCP tool `profileName`.

Profiles are saved SSH connection settings supported by the KelpieSSH library. `SshConnectionProfile`, `SshConnectionProfileFileLoader`, and `SshConnectionProfileCatalog` are public library-level profile APIs used by CLI, MCP, and products such as KelpiePro.

The Application API also accepts `SshRemoteOperation`, which represents one SSH operation with endpoint, credential, policy, operation, and options. `SshRemoteOperation` is useful when a caller wants to execute a one-off operation without relying on a saved profile. Existing CLI and MCP profile loaders can convert saved profiles into `SshRemoteOperation` before execution.

Product-specific concepts such as profile count limits, edition limits, license state, ads, support, display order, notes, and customer data stay outside KelpieSSH. KelpiePro can implement Free/Standard differences by limiting how many OSS profiles it loads or exposes, without adding edition policy to the OSS profile model.

```json
{
  "LogDirectory": "D:\\Kelpie\\logs"
}
```

Each `KelpieHome/profiles/*.json` file is one profile. The file name is the profile name, so `profiles/vps01.json` is profile `vps01`.
The public sample profile is `config_samples/servers/vps01.json`.

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
    "OsFamily": "debian"
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
  }
}
```

`Auth` and `Authentication` are both accepted for SSH authentication settings. `Authentication` is the formal name; `Auth` is the short hand-written alias.
`Auth.PrivateKeyFile` / `Authentication.PrivateKeyFile` is resolved under `KelpieHome/keys` when it is relative. Do not commit real hosts, users, private key files, passphrases, or passwords.
`Auth.Method` / `Authentication.Method` supports `privateKey` and `password`. Password authentication uses `PasswordSecretName`; plain text passwords must not be stored in JSON files. The current password provider is an in-memory session store populated by `kelpiemcp password <profile>`.
`Platform.OsFamily`, optional `Platform.PackageManager`, `Mode`, and `Capabilities` are used to select and evaluate safe command behavior for each profile.
`Mode` is the shared permission preset used by CLI and MCP. Supported modes are `ReadOnly`, `Safe`, `Maintenance`, and `Expert`.
`Capabilities` are CLI-only overrides. MCP execution ignores `Capabilities` and evaluates only `Mode`-based permissions. Secrets are never shown through MCP, even in `Expert` mode.
`Capabilities` may be an array such as `["AllowAlias", "AllowSudo"]` or a pipe-separated string such as `"AllowAlias|AllowSudo"`. Unknown capability names are configuration errors.
`AllowedRoots` limits path-based operations. Use pipe-separated raw flags such as `@Read|@List|@Write|@CD`, `$`-prefixed named presets from `Rights`, or `$ALL` for `@Read|@List|@Write|@Import|@Export|@CD`. Built-in `$ReadOnly`, `$ReadWrite`, and `$ALL` are available and cannot be overridden. Bare tokens such as `Read`, `Write`, and `ALL` are configuration errors. `*` and `**` are explicit global path values; omitted or empty `AllowedRoots` means path-based operations are not allowed by policy.
Real `profiles/*.json` files should not be committed.

# KelpieSSH

KelpieSSH is a local MCP server for safely assisting VPS diagnostics and maintenance over SSH.

Japanese documentation is available in [README.ja.md](README.ja.md).

Command details are documented in [COMMANDS.md](COMMANDS.md).

MCP command details are documented in [MCP_COMMANDS.md](MCP_COMMANDS.md).

Configuration details are documented in [CONFIG.md](CONFIG.md).

SSH profile setup is documented in [PROFILE_GUIDE.md](PROFILE_GUIDE.md).

AI MCP server setup is documented in [MCP_GUIDE.md](MCP_GUIDE.md).

Provider support and implementation status are documented in [PROVIDERS.md](PROVIDERS.md).

`kelpie` reads `config/kelpie.json`.

Sample configuration files are provided under `config_samples/`:

```text
config_samples/
├─ kelpie.json
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
kelpie 0.3.1.1
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

Example interactive output:

```text
Create MCP server configuration.
Press Enter to use the default value.
MCP log directory [D:\Kelpie\logs]: D:\Kelpie\logs
MCP server port [45432]: 45432
MCP control pipe name [KelpieMCPServer.Control]: KelpieMCPServer.Control
Create SSH profile template.
Press Enter to use the default value.
Host address [localhost]: example.org
Port [22]: 2222
SSH user [deploy]: ops
Authentication method (privateKey/password) [privateKey]: password
Password secret name [kelpie:vps01]: kelpie:vps01
OS family [debian]: ubuntu
Mode (ReadOnly/Safe/Maintenance/Expert) [Safe]: ReadOnly
Read-only root, '-' to omit [/var/log]: /var/log/nginx
Read-write root, '-' to omit [/var/www]: -
Deny pattern, '-' to omit [**/.env]: **/.secret
Kelpie home: D:\Kelpie
Profile: vps01
Created directories:
  D:\Kelpie\config
  D:\Kelpie\profiles
  D:\Kelpie\keys
  D:\Kelpie\dat
  D:\Kelpie\bin\mcp
Created files:
  D:\Kelpie\config\kelpie.json
  D:\Kelpie\config\kelpiemcp.json
  D:\Kelpie\profiles\vps01.json
```

Edit the generated profile before connecting. The profile file is created under:

```text
<KelpieHome>\profiles\vps01.json
```

For profile syntax and field details, see [PROFILE_GUIDE.md](PROFILE_GUIDE.md).

Set the target host, SSH user, authentication method, and key or password secret reference in that file. For private key authentication, place the private key file under `<KelpieHome>\keys` and set `Auth.PrivateKeyFile` to that file name. The matching public key must already be registered on the server. For password authentication, set `Auth.Method` to `password` and set `Auth.PasswordSecretName`; do not store the plain text password in the profile.

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

Extract `KelpieSSH-x.x.x.x-x64.zip` to `D:\Kelpie`. The CLI-related files are placed like this:

```text
D:\Kelpie
├─ bin
│  ├─ kelpie.exe
│  └─ kelpiemcp.exe
├─ config_samples
├─ docs
├─ README.md
├─ README.ja.md
├─ COMMANDS.md
├─ CONFIG.md
├─ MCP_GUIDE.md
└─ PROFILE_GUIDE.md
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

Open a new terminal after updating `PATH`.

Verify that the command is available:

```powershell
kelpie version
```

Expected output:

```text
kelpie 0.3.1.1
```

If you do not want to update `PATH`, keep using full paths such as `D:\Kelpie\bin\kelpie.exe`.

#### 3. Initializing Kelpie home and creating a profile

Execute this command in the terminal:

```powershell
D:\Kelpie\bin\kelpie.exe init
```

With `D:\Kelpie\bin\kelpie.exe`, `kelpie init` creates the CLI-related files under `D:\Kelpie`:

```text
D:\Kelpie
├─ config
│  └─ kelpie.json
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

For profile syntax and field details, see [PROFILE_GUIDE.md](PROFILE_GUIDE.md).

Set the target host, SSH user, authentication method, and key or password secret reference in that file. For private key authentication, place the private key file under `D:\Kelpie\keys` and set `Auth.PrivateKeyFile` to that file name. The matching public key must already be registered on the server. For password authentication, set `Auth.Method` to `password` and set `Auth.PasswordSecretName`; do not store the plain text password in the profile.

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
D:\Kelpie\bin\kelpie.exe init
```

To create a named profile, initialize it before connecting:

```powershell
D:\Kelpie\bin\kelpie.exe init vps01
```

`kelpie init` does not overwrite existing configuration files. Edit the generated profile before use:

```text
D:\Kelpie\profiles\vps01.json
```

For profile syntax and field details, see [PROFILE_GUIDE.md](PROFILE_GUIDE.md).

Set the host, user, authentication method, and private key file name or password secret reference before running `kelpie open vps01`.

### AI users

When using Kelpie as an AI MCP server, configure and start the server by following [MCP_GUIDE.md](MCP_GUIDE.md).
MCP server shutdown and password-session cleanup are also covered in [MCP_GUIDE.md](MCP_GUIDE.md).

### Disconnecting and logging out

To close an interactive SSH session started with `kelpie login`, type `logout` or `exit` in the session:

```text
logout
```

## Kelpie command-line tools

The `kelpie` command-line tools do not require the MCP server for normal terminal use. Use them directly from a terminal to initialize local settings, inspect profiles, open a target profile, start an interactive SSH session, run diagnostics, or tail service logs.

KelpieSSH uses provider modules to expose bounded SSH operations for each target OS, package manager, service, and web public root. Providers are allow-list based; they add named, parameter-validated operations instead of opening arbitrary shell access. For the current provider list and implementation status, see [PROVIDERS.md](PROVIDERS.md).

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
kelpie status vps01
```

Run an interactive SSH session with:

```powershell
kelpie open vps01
kelpie login
```

Run high-level VPS diagnostics or tail service logs with:

```powershell
kelpie diag vps01
kelpie logs vps01 nginx.service
kelpie logs vps01 nginx.service 200
```

`kelpie login`, `kelpie diag`, and `kelpie logs` run SSH operations directly from the CLI process. For password profiles, the CLI asks for the password at runtime and keeps it only in the current command process. `kelpie status` can also report whether the local MCP server is running, but the server is not required for the command-line tools above.

## How to confirm profile

Use `kelpie profile show <profile>` to inspect a sanitized summary of a configured SSH profile.

```powershell
kelpie profile show vps01
```

Example `kelpie profile show vps01` output:

```text
Profile: vps01
Host: example.invalid
Port: 22
User: deploy
OS family: debian
Package manager: apt
Command OS family: debian
Command providers:
  CommonDiagnosticCommandProvider
  DebianAptCommandProvider
Capabilities:
  (empty list)
Roles:
  Safe
Effective mode: Safe
Allowed roots:
  /var/www  => @Read|@List|@Write|@CD
Special paths:
  **/.env  => Deny
Services:
  (empty list)
Users:
  deploy  => Safe
Authentication: privateKey
Private key: (configured)
```

## How to create new profile in initialized directory

After `KelpieHome` has already been initialized, use `kelpie profile create <profile>` to add one new profile template without recreating configuration files or directories.

```powershell
kelpie profile create vps02
```

Example interactive output:

```text
Create SSH profile template.
Press Enter to use the default value.
Host address [localhost]: example.org
Port [22]: 2222
SSH user [deploy]: ops
Authentication method (privateKey/password) [privateKey]: password
Password secret name [kelpie:vps02]: kelpie:vps02
OS family [debian]: ubuntu
Mode (ReadOnly/Safe/Maintenance/Expert) [Safe]: ReadOnly
Read-only root [Return to skip]: /var/log/nginx
Read-only root [Return to skip]:
Read-write root [Return to skip]:
Deny pattern [Return to skip]: **/.secret
Deny pattern [Return to skip]:
Created profile: vps02
Profile file: D:\Kelpie\profiles\vps02.json
```

If `profiles\vps02.json` already exists, the command asks whether to overwrite it. When overwritten, the old file is kept as `profiles\vps02.json.kelpie` until you commit or roll back the profile change.

## Contributing

Contributions are welcome. For small fixes such as documentation updates, typo fixes, tests, and narrow bug fixes, feel free to open a pull request.

For larger changes, new commands, security-related behavior, SSH policy changes, MCP tool changes, or changes that affect compatibility, please open an issue first so the scope and safety requirements can be discussed before implementation.

When reporting issues, do not include real host names, user names, passwords, passphrases, private keys, production profile files, or raw logs that may contain secrets.

## Contact

For project questions, contribution discussions, and non-sensitive support inquiries, use GitHub Issues when possible.

For direct contact, email [shoe0604@akatsukisoft.com](mailto:shoe0604@akatsukisoft.com).

## Security

KelpieSSH is designed to start with read-oriented diagnostics and allow-list based SSH command execution.

Do not commit real host names, user names, passwords, passphrases, private keys, or production profile files. Keep real `profiles/*.json`, `keys/`, `dat/`, and `logs/` files outside the public repository.

Password authentication is runtime-only. CLI SSH commands ask for the password for the current command process, and `kelpiemcp password <profile>` stores it only in the running `KelpieMCPServer` process. Plain text passwords must not be stored in JSON configuration files.

Do not report vulnerabilities or secret-bearing details in public issues. For vulnerability reporting and supported-version guidance, see [SECURITY.md](SECURITY.md).

## Disclaimer

KelpieSSH is provided as-is, without warranties of any kind.

KelpieSSH can execute operations that may change server configuration, packages, services, files, permissions, and other system state. You are responsible for reviewing profiles, permissions, confirmations, commands, target hosts, and backups before using the software.

The authors and contributors are not responsible for data loss, service outage, security incidents, configuration damage, business interruption, or any other damage caused by use or misuse of KelpieSSH.

Use KelpieSSH only on systems you own or are authorized to manage. Test changes in a safe environment before applying them to production systems, and keep restorable backups for important servers and data.

## License

KelpieSSH is released under the Apache License 2.0. See [LICENSE](LICENSE).

Copyright (c) 2026 Akatsukisoft.

The Apache License 2.0 permits commercial use, modification, redistribution, sublicensing, and patent use under its license terms. It also includes an explicit contributor patent grant and patent termination clause.

KelpiePro is planned as a paid closed-source desktop product. KelpiePro may reference KelpieSSH and Kelpie Core libraries as NuGet packages without forking or copying the OSS implementation into the closed-source product repository. This repository remains the upstream source for the OSS implementation and package metadata.

When KelpieSSH packages or binaries are redistributed with KelpiePro, include the KelpieSSH Apache License 2.0 notice and the third-party notices listed in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) in the installer, application about box, bundled documentation, or an equivalent notices location.

The current runtime dependency review did not identify GPL, AGPL, LGPL, SSPL, Commons Clause, or other non-permissive dependencies in KelpieSSH runtime packages. Review `THIRD_PARTY_NOTICES.md` again whenever package versions are added or updated.

## SSH profiles

SSH connection profiles are configured as one JSON file per server under `KelpieHome/profiles`.

Runtime configuration does not set a default SSH profile. Specify the profile explicitly with commands such as `kelpie open vps01` or MCP tool `profileName`.

Profiles are saved SSH connection settings supported by the KelpieSSH library. `SshConnectionProfile`, `SshConnectionProfileFileLoader`, and `SshConnectionProfileCatalog` are public library-level profile APIs used by CLI, MCP, and products such as KelpiePro.

The Application API also accepts `SshRemoteOperation`, which represents one SSH operation with endpoint, credential, policy, operation, and options. `SshRemoteOperation` is useful when a caller wants to execute a one-off operation without relying on a saved profile. Existing CLI and MCP profile loaders can convert saved profiles into `SshRemoteOperation` before execution.

Product-specific concepts such as profile count limits, edition limits, license state, ads, support, display order, notes, and customer data stay outside KelpieSSH. KelpiePro can implement Free/Standard differences by limiting how many OSS profiles it loads or exposes, without adding edition policy to the OSS profile model.

Each `KelpieHome/profiles/*.json` file is one profile. The file name is the profile name, so `profiles/vps01.json` is profile `vps01`.
The public sample profile is `config_samples/servers/vps01.json`.

For profile field details, examples, validation checklist, and troubleshooting, see [PROFILE_GUIDE.md](PROFILE_GUIDE.md).
Real `profiles/*.json` files should not be committed.

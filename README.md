# KelpieSSH

KelpieSSH is a local MCP server for safely assisting VPS diagnostics and maintenance over SSH.

Japanese documentation is available in [docs/ja/README.ja.md](docs/ja/README.ja.md).

Command details are documented in [COMMANDS.md](COMMANDS.md).

MCP command details are documented in [MCP_COMMANDS.md](MCP_COMMANDS.md).

Configuration details are documented in [CONFIG.md](CONFIG.md).

SSH profile setup is documented in [PROFILE_GUIDE.md](PROFILE_GUIDE.md).

AI MCP server setup is documented in [MCP_GUIDE.md](MCP_GUIDE.md).

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
kelpie 0.1.4.1
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

At this stage, `kelpie diag` and `kelpie logs` run SSH commands directly from the CLI process and are intended for private-key profiles. `kelpie status` can also report whether the local MCP server is running, but the server is not required for the command-line tools above.

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

# KelpieSSH Release Process

Last updated: 2026-06-17

This document defines the public release procedure for the OSS KelpieSSH repository.

Release artifacts are generated under the repository-local `release/<version>/` directory. The `release/` directory is generated output and is not committed.

## Release Scope

The OSS release contains:

- `kelpie`: terminal CLI frontend from `KelpieClientCommand`;
- `kelpiemcp`: MCP server control CLI from `KelpieServerCommand`;
- `KelpieMCPServer`: Streamable HTTP MCP server from `KelpieMCPServer`;
- public documentation, sample configuration, and license files.

Product versions are managed per project:

| Product | Project | Version Source |
| :--- | :--- | :--- |
| `kelpie` | `src/KelpieClientCommand/KelpieClientCommand.csproj` | `Version` |
| `kelpiemcp` | `src/KelpieServerCommand/KelpieServerCommand.csproj` | `Version` |
| `KelpieMCPServer` | `src/KelpieMCPServer/KelpieMCPServer.csproj` | `Version` |

The release folder version is based on the `kelpie` product version unless an explicit `-Version` parameter is supplied to the release scripts.

## Preconditions

- Work from `develop` for normal release preparation.
- The worktree must be clean before release packaging, except for intentionally generated files under `release/`.
- Public documentation must not link to private `.local/` documents.
- Secrets, private keys, real host names, real user names, raw logs containing secrets, customer data, and unpublished production settings must not be included in release artifacts.
- WiX CLI is required only when creating MSI artifacts. Install it separately if MSI generation is required.

## Verification

Run the solution build and tests before generating artifacts:

```powershell
dotnet build D:\Workspace\Projects\Kelpie\KelpieSSH.sln --no-restore
dotnet test D:\Workspace\Projects\Kelpie\KelpieSSH.sln --no-restore
```

Both commands must complete with exit code `0`.

If restore is required in a fresh environment, run restore first and then repeat build/test:

```powershell
dotnet restore D:\Workspace\Projects\Kelpie\KelpieSSH.sln
```

## ZIP Artifact

Create the ZIP release payload:

```powershell
D:\Workspace\Projects\Kelpie\scripts\Build-Zip.ps1 -Configuration Release
```

Default output:

```text
D:\Workspace\Projects\Kelpie\release\<version>\files
D:\Workspace\Projects\Kelpie\release\<version>\KelpieSSH-<version>-x64.zip
```

The ZIP payload includes:

- published binaries under `bin/`;
- MCP server binaries under `bin/mcp/`;
- `config_samples/`;
- `docs/`;
- public root documents such as `README.md`, `COMMANDS.md`, `MCP_COMMANDS.md`, `RELEASE_PROCESS.md`, `SECURITY.md`, and `LICENSE`.

## MSI Artifact

Create the MSI release artifact when WiX CLI is available:

```powershell
D:\Workspace\Projects\Kelpie\scripts\Build-Msi.ps1 -Configuration Release -OutputRoot release\<version>\msi
```

Default output when `-OutputRoot release\<version>\msi` is used:

```text
D:\Workspace\Projects\Kelpie\release\<version>\msi\KelpieSSH-<version>-x64.msi
```

If WiX CLI is not available, generate only the WiX source for inspection:

```powershell
D:\Workspace\Projects\Kelpie\scripts\Build-Msi.ps1 -Configuration Release -OutputRoot release\<version>\msi -SkipWixBuild
```

This produces:

```text
D:\Workspace\Projects\Kelpie\release\<version>\msi\KelpieSSH.generated.wxs
```

## Artifact Checks

After packaging, confirm that the release folder exists and contains the expected files:

```powershell
Get-ChildItem -Force D:\Workspace\Projects\Kelpie\release\<version>
Get-ChildItem -Force D:\Workspace\Projects\Kelpie\release\<version>\files\bin
Get-ChildItem -Force D:\Workspace\Projects\Kelpie\release\<version>\files\bin\mcp
```

Minimum expected ZIP release files:

- `KelpieSSH-<version>-x64.zip`
- `files/bin/kelpie.exe`
- `files/bin/kelpiemcp.exe`
- `files/bin/mcp/KelpieMCPServer.exe`
- `files/README.md`
- `files/RELEASE_PROCESS.md`
- `files/LICENSE`

## Git Flow

1. Commit release process, documentation, version, and source changes on `develop`.
2. Push `develop`.
3. Merge or fast-forward `develop` into `main` when the release is approved.
4. Push `main`.
5. Keep generated `release/` artifacts out of Git unless the project explicitly changes that policy.

## Operational Notes

- Do not overwrite an installed runtime such as `D:\Kelpie\bin` as part of packaging unless the task explicitly asks for deployment.
- Do not start or stop a user MCP server as part of release packaging.
- If packaging fails after build/test succeeds, keep the failure output and fix the packaging step before publishing release artifacts.

# PACKAGES.md Version
2026.10.27

# Change History
- 2026.10.25
- 2026.10.13
- 2026.08.07
- 2026.07.30
- 2026.06.24
- 2026.06.17

# KelpieSSH Packages

This file is the English package and dependency reference for KelpieSSH.
For Japanese documentation, see [docs/ja/PACKAGES.ja.md](docs/ja/PACKAGES.ja.md).

## Target Projects

| Project | Target Framework | Purpose |
| :--- | :--- | :--- |
| `Kelpie.Core` | `net8.0` | Shared runtime paths, logging, and common options. |
| `KelpieSSH.Domain` | `net8.0` | Domain models and value objects. |
| `KelpieSSH.Application` | `net8.0` | Application services, command policy, profile and operation models. |
| `KelpieSSH.Infrastructure` | `net8.0` | SSH.NET based infrastructure adapters. |
| `KelpieClientCommand` | `net8.0` | `kelpie` CLI. |
| `KelpieServerCommand` | `net8.0` | `kelpiemcp` CLI. |
| `KelpieMCPServer` | `net8.0` | Streamable HTTP MCP server. |
| `KelpieWebPermissionHelper` | `net8.0` | Linux-side helper for web permission operations. |
| `KelpieSSH.Application.Tests` | `net8.0` | Automated unit and integration tests; not a runtime distribution unit. |

## Distribution Units

| Unit | Project | Version | Purpose | Placement |
| :--- | :--- | :--- | :--- | :--- |
| `kelpie` | `src/KelpieClientCommand/KelpieClientCommand.csproj` | `0.3.14.4` | VPS operation CLI. | `KelpieHome/bin/kelpie.exe` |
| `kelpiemcp` | `src/KelpieServerCommand/KelpieServerCommand.csproj` | `0.3.12.8` | MCP server control and human-only remote policy/helper administration CLI. | `KelpieHome/bin/kelpiemcp.exe` |
| `KelpieMCPServer` | `src/KelpieMCPServer/KelpieMCPServer.csproj` | `0.3.17.4` | Streamable HTTP MCP server. | `KelpieHome/bin/mcp/KelpieMCPServer.exe` |
| `kelpie-web-permission-helper` | `src/KelpieWebPermissionHelper/KelpieWebPermissionHelper.csproj` | `0.2.4.0` | Optional Linux-side helper for web permission and managed policy operations. | `/usr/local/libexec/kelpie/kelpie-web-permission-helper` |

For the Alpha release, the Windows zip is the primary binary distribution. It includes `Install-Kelpie.ps1` for per-user automatic installation and `PATH` registration. MSI may be attached when available and may be unsigned. Internal NuGet packages are separate from the Alpha end-user binary distribution.

## Package Sources

Package sources follow the NuGet.Config hierarchy of the consuming project and the standard NuGet package resolution rules. This repository does not contain a repository-level `nuget.config`.
Do not publish private feed credentials or tokens in this repository.

## Internal NuGet Packages

KelpieSSH libraries are packable where appropriate.

| Package | Project | NuGet Version | Purpose |
| :--- | :--- | :--- | :--- |
| `Akatsukisoft.Kelpie.Core` | `Kelpie.Core` | `0.3.1-alpha` | Common runtime support. |
| `Akatsukisoft.KelpieSSH.Domain` | `KelpieSSH.Domain` | `0.1.1-alpha` | Domain models and value objects. |
| `Akatsukisoft.KelpieSSH.Application` | `KelpieSSH.Application` | `0.3.10.4-alpha` | SSH application services and policy models. |
| `Akatsukisoft.KelpieSSH.Infrastructure` | `KelpieSSH.Infrastructure` | `0.1.3.0-alpha` | SSH.NET infrastructure implementation. |

## Direct Runtime Dependencies

`KelpieMCPServer` additionally references `Moyai.ProviderAuthentication` 1.0.2 (.NET 8) for ES256 v2 validation, Trust Bundle loading and durable replay prevention. Obtain the provider-distributed package and verify its published SHA-256 before restore. Supply that package directory as an additional NuGet source (`dotnet restore --source <package-directory> --source <existing-feed>`). No source copy or cross-repository ProjectReference is used. The package is not assumed to be available on nuget.org.

| Package | Version | Used by | Purpose |
| :--- | :--- | :--- | :--- |
| `Microsoft.Extensions.Configuration.Abstractions` | `10.0.8` | `Kelpie.Core` | Configuration abstractions. |
| `Microsoft.Extensions.Configuration.Json` | `10.0.8` | `KelpieClientCommand`, `KelpieServerCommand` | JSON configuration loading. |
| `Microsoft.Extensions.Logging.Abstractions` | `8.0.0` | `KelpieSSH.Application` | Logging abstractions for application services. |
| `System.Security.Cryptography.ProtectedData` | `8.0.0` | `KelpieSSH.Application` | Windows DPAPI protection for persisted trust data. |
| `SSH.NET` | `2026.0.0` | `KelpieSSH.Infrastructure` | SSH connection, authentication, command execution, and shell sessions. |
| `ModelContextProtocol` | `1.3.0` | `KelpieMCPServer` | MCP server abstractions. |
| `ModelContextProtocol.AspNetCore` | `1.3.0` | `KelpieMCPServer` | Streamable HTTP MCP transport. |
| `Microsoft.Extensions.Hosting` | `10.0.8` | `KelpieMCPServer` | ASP.NET Core hosting support. |
| `Microsoft.Extensions.Hosting.WindowsServices` | `10.0.8` | `KelpieMCPServer` | Windows Service hosting integration. |
| `Moyai.ProviderAuthentication` | `1.0.2` | `KelpieMCPServer` | Signed provider assertions, trust bundles, and durable replay prevention. |

## Test Dependencies

Test-only packages are not redistributed with runtime binaries.
They include xUnit, FluentAssertions, Microsoft.NET.Test.Sdk, and coverlet collector packages.

| Package | Purpose |
| :--- | :--- |
| `coverlet.collector` | Test coverage collection. |
| `FluentAssertions` | Assertions. |
| `Microsoft.NET.Test.Sdk` | Test execution infrastructure. |
| `xunit` | Unit test framework. |
| `xunit.runner.visualstudio` | Test discovery and execution from Visual Studio and `dotnet test`. |

## Third-Party Notices

Runtime third-party notices are listed in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
Update that file whenever runtime packages are added or updated.

## Verification Commands

```powershell
dotnet list package
dotnet list package --include-transitive
dotnet build
dotnet test
```

## Update Rules

- Prefer stable package versions unless a preview is intentionally required.
- Re-run build and tests after package updates.
- Re-check third-party notices and license risk after runtime package changes.
- Do not add local or private package feeds with secrets to public documentation.

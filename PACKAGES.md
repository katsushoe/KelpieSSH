# PACKAGES.md Version
2026.06.17

# Change History
- 2026.06.17

# KelpieSSH Packages

This file is the English package and dependency reference for KelpieSSH.
For Japanese documentation, see [docs/ja/PACKAGES.ja.md](docs/ja/PACKAGES.ja.md).

## Target Projects

| Project | Target Framework | Purpose |
| :--- | :--- | :--- |
| `Kelpie.Core` | `net8.0` | Shared runtime paths, logging, and common options. |
| `KelpieSSH.Application` | `net8.0` | Application services, command policy, profile and operation models. |
| `KelpieSSH.Infrastructure` | `net8.0` | SSH.NET based infrastructure adapters. |
| `KelpieClientCommand` | `net8.0` | `kelpie` CLI. |
| `KelpieServerCommand` | `net8.0` | `kelpiemcp` CLI. |
| `KelpieMCPServer` | `net8.0` | Streamable HTTP MCP server. |
| `KelpieWebPermissionHelper` | `net8.0` | Linux-side helper for web permission operations. |

## Package Sources

Package sources are controlled by `nuget.config` and the standard NuGet package resolution rules.
Do not publish private feed credentials or tokens in this repository.

## Internal NuGet Packages

KelpieSSH libraries are packable where appropriate.

| Package | Project | Purpose |
| :--- | :--- | :--- |
| `Akatsukisoft.Kelpie.Core` | `Kelpie.Core` | Common runtime support. |
| `Akatsukisoft.KelpieSSH.Application` | `KelpieSSH.Application` | SSH application services and policy models. |
| `Akatsukisoft.KelpieSSH.Infrastructure` | `KelpieSSH.Infrastructure` | SSH.NET infrastructure implementation. |

## Direct Runtime Dependencies

| Package | Used by | Purpose |
| :--- | :--- | :--- |
| `SSH.NET` | `KelpieSSH.Infrastructure` | SSH connection, authentication, command execution, and shell sessions. |
| `ModelContextProtocol` | `KelpieMCPServer` | MCP server abstractions. |
| `ModelContextProtocol.AspNetCore` | `KelpieMCPServer` | Streamable HTTP MCP transport. |
| `Microsoft.Extensions.Hosting` | `KelpieMCPServer` | ASP.NET Core hosting support. |
| `Microsoft.Extensions.Hosting.WindowsServices` | `KelpieMCPServer` | Windows Service hosting integration. |

## Test Dependencies

Test-only packages are not redistributed with runtime binaries.
They include xUnit, FluentAssertions, Microsoft.NET.Test.Sdk, and coverlet collector packages.

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

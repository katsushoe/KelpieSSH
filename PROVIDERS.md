# KelpieSSH Providers

This document summarizes the provider-based implementation status in the current KelpieSSH source tree.

Providers are small allow-list modules that expose a bounded set of SSH operations for a profile. A provider is selected from profile settings such as `Platform.OsFamily`, `Platform.PackageManager`, service keys, and `Services.WebPublic` site definitions. Providers do not make arbitrary shell execution available.

## Provider Categories

| Category | Purpose | Selection Basis | Public Surface |
| :--- | :--- | :--- | :--- |
| Command-processing providers | Add allowed command definitions to a profile. | `Platform.OsFamily`, `Platform.PackageManager`, and provider-specific support checks. | MCP tools and CLI operations that execute named allowed commands. |
| Service configuration providers | Discover, read, edit, test, roll back, and commit provider-approved service configuration files. | `serviceKey` such as `nginx`. | `service_config_*` MCP tools. |
| Web public file provider | Read, search, write, and adjust permissions inside provider-approved web public roots. | `Services.WebPublic` entries in the SSH profile. | `web_public_*` MCP tools. |
| Profile and credential providers | Load SSH profiles and provide runtime-only SSH passwords. | Kelpie home profile catalog, trust store, and password session state. | CLI/MCP profile operations and SSH session creation. |

## Implemented Providers

| Provider | Category | Status | Supported Targets | Main Capabilities | Notes |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `CommonDiagnosticCommandProvider` | Command-processing | Implemented | Any non-empty OS family (`*`) | System info, OS release, inventory, disk/memory/network checks, cron checks and changes, user permission changes, service status/log reads, firewall checks and changes, backups, audit checks. | Some write operations require confirmation and sudo. Service status/log operations depend on `systemctl` and `journalctl`. |
| `DebianAptCommandProvider` | Command-processing | Implemented | Debian-family profiles with `PackageManager` = `apt` | Package update check, package info/search, installed package listing, install/remove simulation, install/remove. | `pkg_install` and `pkg_remove` are confirmation-required. |
| `RhelDnfCommandProvider` | Command-processing | Implemented | RHEL-family profiles with `PackageManager` = `dnf` | Package update check, package info/search, installed package listing, install/remove simulation, install/remove. | Uses `sudo -n dnf` for install/remove paths. |
| `NginxServiceConfigCommandProvider` | Command-processing | Implemented | Any non-empty OS family (`*`) | Internal command definitions for Nginx config discovery, read, write, check, rollback, commit, test, and log read. | Intended to be used through service configuration MCP tools, not as a broad Nginx shell interface. |
| `RhelNginxCommandProvider` | Command-processing | Implemented | RHEL-family profiles | `systemctl enable --now`, reload, restart, stop, disable, and local HTTP check. | Despite the class name, the service parameter is generic. The provider is currently RHEL-family only. |
| `WebPublicFileCommandProvider` | Command-processing | Implemented | Any non-empty OS family (`*`) | Internal web public list/stat/read/head/tail/search/write and permission command definitions. | Permission-changing and owner/mode write paths use `/usr/local/libexec/kelpie/kelpie-web-permission-helper`. |
| `NginxConfigPathsProvider` | Service configuration | Implemented | `serviceKey` = `nginx` | Discovers Nginx `--conf-path`, reads approved config files, applies limited edits, creates backup files, rolls back/commits backups, runs `nginx -t`, and reads access/error logs. | Include patterns are provider-approved before use. Sensitive-value detection currently warns but does not mask content. |
| `WebPublicFileProvider` | Web public file | Implemented | Profile-defined `Services.WebPublic` sites | Lists, searches, stats, reads, writes, and changes owner/mode for approved web public paths. | Falls back to a default site rooted at `/var/www/html` when no explicit site is configured. |
| `SshConnectionProfileCatalog` | Profile catalog | Implemented | Local profile files | Loads saved SSH profiles from the profile directory. | Used by CLI and MCP profile resolution. |
| `ReloadingSshConnectionProfileCatalog` | Profile catalog | Implemented | Trusted profile files | Reloads profile data and applies trust-store gating for MCP-visible profiles. | Used by the MCP server and profile trust operations. |
| `NullSshPasswordProvider` | Credential provider | Implemented | Passwordless or non-password flows | Returns no password. | Used when password sessions are unavailable or unnecessary. |
| `InMemorySshPasswordSessionStore` | Credential provider | Implemented | Running process only | Stores password sessions in memory for the current process. | Plain text passwords are not persisted to profile/config files. |

## Not Implemented Yet

| Area | Status | Current Behavior | Expected Future Provider Shape |
| :--- | :--- | :--- | :--- |
| Debian/Ubuntu service lifecycle write provider | Not implemented | Read-oriented service status/log commands are available through the common provider. Write operations such as service enable/reload/restart/stop are not provided by a Debian-specific provider. | A Debian/systemd provider can expose confirmation-required service lifecycle operations for Debian-family targets. |
| `yum` package provider | Not implemented | RHEL-family package management currently targets `dnf`. | A legacy RHEL/CentOS provider can support `yum` if needed. |
| `apk` package provider | Not implemented | Alpine Linux package management is not available. | An Alpine provider can expose bounded `apk` info/search/install/remove commands. |
| `pacman` package provider | Not implemented | Arch Linux package management is not available. | An Arch provider can expose bounded `pacman` query/install/remove commands. |
| `zypper` package provider | Not implemented | openSUSE/SLES package management is not available. | A SUSE provider can expose bounded `zypper` info/search/install/remove commands. |
| Apache HTTP Server service config provider | Not implemented | No `apache` or `httpd` serviceKey exists. | A service config provider can discover approved Apache config files, test config, and read logs. |
| PHP-FPM service config provider | Not implemented | No `php-fpm` serviceKey exists. | A service config provider can manage approved pool/config files and test PHP-FPM configuration. |
| MySQL/MariaDB service config provider | Not implemented | No database config provider exists. | A provider can read and apply limited edits to approved database config files. |
| PostgreSQL service config provider | Not implemented | No database config provider exists. | A provider can discover approved PostgreSQL config files and apply limited edits. |
| Nginx sensitive-value masking | Partially implemented | The Nginx config provider warns when content may contain sensitive values. It does not mask the content yet. | Provider-level masking or redaction can be added before returning config content. |
| Non-systemd service provider | Not implemented | Current service diagnostics and lifecycle assumptions use `systemctl` and `journalctl`. | Providers for `service`, OpenRC, runit, or other init systems can be added separately. |

## Registration Points

| Registry | Source | Default Providers |
| :--- | :--- | :--- |
| Command-processing provider catalog | `src/KelpieSSH.Application/Ssh/CommandProcessingProviderCatalog.cs` | `CommonDiagnosticCommandProvider`, `NginxServiceConfigCommandProvider`, `WebPublicFileCommandProvider`, `DebianAptCommandProvider`, `RhelDnfCommandProvider`, `RhelNginxCommandProvider` |
| Service configuration provider catalog | `src/KelpieSSH.Application/Ssh/ServiceConfigPathsProviderCatalog.cs` | `NginxConfigPathsProvider` |
| Web public file provider | `src/KelpieSSH.Application/Ssh/WebPublicFileProvider.cs` | Single generic provider, driven by profile `Services.WebPublic` site definitions. |
| Profile catalog providers | `src/KelpieSSH.Application/Ssh/SshConnectionProfileCatalog.cs`, `src/KelpieSSH.Application/Ssh/ReloadingSshConnectionProfileCatalog.cs` | File-based profile catalog and trust-store-aware reloading catalog. |
| Password providers | `src/KelpieSSH.Application/Ssh/NullSshPasswordProvider.cs`, `src/KelpieSSH.Application/Ssh/InMemorySshPasswordSessionStore.cs` | Null provider and in-memory password session store. |

## Adding a Provider

For command-processing providers, implement `IAllowedCommandProvider` or `ICommandProcessingProvider`, validate every parameter with explicit patterns or length limits, and register the provider in `CommandProcessingProviderCatalog.CreateDefault()`.

For service configuration providers, implement `IServiceConfigPathsProvider` and only add optional interfaces for supported operations, such as `IServiceConfigFileReader`, `IServiceConfigFileWriter`, `IServiceConfigFileTester`, `IServiceConfigFileBackupManager`, `IServiceLogfileReader`, and `IServiceConfigFileAccessChecker`. Register the provider in `ServiceConfigPathsProviderCatalog.CreateDefault()`.

Provider changes that alter public CLI/MCP behavior must be reflected in `COMMANDS.md`, `MCP_COMMANDS.md`, `CONFIG.md` or profile documentation as appropriate.

# OSS scope and distribution policy

For Japanese, see [the Japanese policy](ja/OSS_SCOPE.ja.md).

## Public scope

KelpieSSH under [Apache License 2.0](../LICENSE) includes the kelpie CLI, kelpiemcp control CLI, KelpieMCPServer, Kelpie.Core, KelpieSSH.Domain, KelpieSSH.Application and KelpieSSH.Infrastructure. The optional Linux KelpieWebPermissionHelper, their source, tests, build/install scripts, public documentation and sanitized configuration samples are also OSS. Existing constrained maintenance operations remain in OSS; the product is not limited to diagnostics. See [providers](../PROVIDERS.md) for actual OS and operation support.

KelpieDesktop and advanced commercial UX, team administration, reporting and support are managed separately by KelpiePro. Basic safety, confirmation, authentication and logging already in this repository remain OSS. A feature name alone does not transfer an existing implementation to the commercial edition. The repository does not promise complete Linux distribution coverage, runit lifecycle, arbitrary root access or unrestricted MCP shell execution.

## Distribution

| Deliverable | Policy |
| :--- | :--- |
| Source | Public source repository and release source archives, with license and dependency notices. |
| Windows x64 MSI | Standard binary deliverable, built with WiX Toolset. Install, upgrade, uninstall, version and hash verification are release gates. |
| Windows x64 ZIP | Supplemental binary deliverable with Install-Kelpie.ps1; it does not replace the standard MSI. |
| Library NuGet packages | A component-consumption boundary for KelpiePro; public registry publication is not part of the initial release scope. |
| Linux privileged helper | Optional separately built/deployed component; Windows CLI/MCP installation does not provision remote helper policy or sudo rules. |

Product versions are independent; the CLI version identifies the release bundle and its notes must list included product versions. Release notes must describe actual artifacts, SHA-256 hashes, signing status, prerequisites and known limits. Build capability does not prove that an artifact has been released or validated. This policy does not authorize unsigned publication or claim that any particular release is available.

Distribute runtime dependencies with applicable [third-party notices](../THIRD_PARTY_NOTICES.md). Do not include private feeds or their credentials, real profiles, keys, passwords, user data, logs, internal project documents or generated test artifacts. Installation and upgrades must preserve existing user configuration and data; remote privileged setup remains an explicit administrator action. See [installation](INSTALLATION.md) and [packages](../PACKAGES.md).

## Contributions and publication

Accept issues and pull requests under [CONTRIBUTING.md](../CONTRIBUTING.md). Security reports use [SECURITY.md](../SECURITY.md). Scope discussion and review precede acceptance of large changes.

Maintainers prepare and validate artifacts locally before publication. Creating tags, publishing releases, uploading assets or publishing registry packages requires separate release authorization for the named products, versions and destination. A documentation update, commit, push or local build is not release publication.

# Contributing

KelpieSSH accepts issues and pull requests for the [public OSS scope](docs/OSS_SCOPE.md). For Japanese, see [the Japanese guide](docs/ja/CONTRIBUTING.ja.md).

## Before You Start

Check existing issues and pull requests. Small documentation fixes, focused bug fixes and tests can be submitted directly. Discuss new commands, providers, policy or compatibility changes in an issue before implementation. Report vulnerabilities privately through [SECURITY.md](SECURITY.md), without posting an exploit or secrets in a public issue.

Use synthetic host names, users and data. Never include production profiles, private keys, passwords, access tokens or unsanitized logs. Describe expected and actual behavior, product version, operating system and a minimal reproducible example.

## Development Setup

Use a .NET SDK that supports the projects' net8.0 target and PowerShell for repository scripts. Windows is the initial CLI/MCP host platform. Disposable Linux SSH targets are useful for provider integration tests; Docker is needed only for tests that use containers.

Restore requires the external Moyai.ProviderAuthentication package described in [PACKAGES.md](PACKAGES.md). Obtain it from its provider and verify its published hash; it is not assumed to exist on nuget.org. Do not commit feed credentials or replace the dependency with a source copy.

From the repository root:

```powershell
dotnet restore KelpieSSH.sln
dotnet build KelpieSSH.sln -c Release --no-restore
dotnet test KelpieSSH.sln -c Release --no-build --no-restore
```

If the provider package is in a separate directory, use the additional-source restore procedure in PACKAGES.md before building. Report OS-specific skips and unavailable SSH prerequisites accurately. Never run integration tests on production hosts.

## Pull Requests

Target develop with one focused change per pull request. Include the problem, resulting behavior, related issue and validation results. Maintainers review scope, compatibility, safety and test evidence before merging; submission does not guarantee acceptance or a response deadline.

Keep application/domain/infrastructure dependencies consistent with the solution. Preserve profile mode, confirmation, trust, path, timeout and secret-handling boundaries. New remote behavior needs meaningful success, failure and policy-rejection tests on disposable targets as appropriate.

Update English and Japanese documentation together. For command/tool/configuration changes, cover inputs, configuration, outputs, state transitions, errors and tests; update the relevant public references and samples. Keep internal notes and local operational data outside public changes.

Contributions are submitted under the repository's [Apache License 2.0](LICENSE); include only material you have the right to contribute and preserve third-party notices. This guide introduces no separate CLA or DCO sign-off requirement. Keep discussions respectful and focused on the change. Publishing releases and packages remains a maintainer operation.

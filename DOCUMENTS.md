# DOCUMENTS.md Version
2026.06.18

This file is the public source of truth for KelpieSSH Git-managed public document locations.

## Placement Policy

- Public, Git-managed documentation is placed at the repository root, `docs/`, or the relevant feature directory.
- Japanese public documentation is placed under `docs/ja/`, except `README.ja.md`, which is kept at the repository root next to `README.md`.
- Private or Git-ignored internal documents are intentionally not listed in this public file.
- Public documentation must not link to private or Git-ignored internal documents.
- Generated artifacts are not documented here unless they are Git-managed public deliverables.

## Project Directories

| Path | Git Managed | Purpose |
| :--- | :--- | :--- |
| `.` | Yes | Repository root. Public entry documents, solution file, license, and project-level settings. |
| `docs/` | Yes | Public supplemental documentation. |
| `docs/ja/` | Yes | Public Japanese documentation. |
| `installer/` | Yes | Installer source and installer-specific public documentation. |
| `config_samples/` | Yes | Public sample configuration files without secrets. |
| `scripts/` | Yes | Build, package, and maintenance scripts. |
| `servers/` | Yes | Public server-side sample/support files. |
| `src/` | Yes | Product source code. |
| `tests/` | Yes | Automated tests. |

## Document Inventory

| Document | Canonical Path | Git Managed | Purpose |
| :--- | :--- | :--- | :--- |
| `README.md` | `README.md` | Yes | Public entry point, setup, usage, security, and license overview. |
| `README.ja.md` | `README.ja.md` | Yes | Japanese public entry point kept next to `README.md` so both documents are updated together. |
| `DOCUMENTS.md` | `DOCUMENTS.md` | Yes | Directory and document canonical-location index. |
| `CLI_OPTIONS.md` | `CLI_OPTIONS.md` | Yes | Public command-line option guide for silent mode, dry-run, runtime directory overrides, and profile transaction options. |
| `COMMANDS.md` | `COMMANDS.md` | Yes | Public terminal CLI command reference. Japanese version is under `docs/ja/`. |
| `MCP_COMMANDS.md` | `MCP_COMMANDS.md` | Yes | Public MCP callable tool reference. Japanese version is under `docs/ja/`. |
| `MCP_GUIDE.md` | `MCP_GUIDE.md` | Yes | Public AI MCP server setup, layout, startup, and usage guide. Japanese version is under `docs/ja/`. |
| `CONFIG.md` | `CONFIG.md` | Yes | Public configuration reference. Japanese version is under `docs/ja/`. |
| `PROFILE_GUIDE.md` | `PROFILE_GUIDE.md` | Yes | Public SSH profile configuration guide. Japanese version is under `docs/ja/`. |
| `PROVIDERS.md` | `PROVIDERS.md` | Yes | Public provider support and implementation status reference. |
| `PACKAGES.md` | `PACKAGES.md` | Yes | Public package, dependency, package-source, and update-policy reference. Japanese version is under `docs/ja/`. |
| `SECURITY.md` | `SECURITY.md` | Yes | Public security policy and vulnerability reporting guidance. Japanese version is under `docs/ja/`. |
| `THIRD_PARTY_NOTICES.md` | `THIRD_PARTY_NOTICES.md` | Yes | Public third-party dependency and license notices. Japanese version is under `docs/ja/`. |
| `LICENSE` | `LICENSE` | Yes | Apache License 2.0 text. |

## Private Document Handling

Private and Git-ignored internal documents are outside the scope of this public inventory.

Do not add private document names, private document paths, real-environment notes, unpublished operational details, secrets, or AI handoff details to this public file.

## Update Rule

When adding, removing, renaming, or moving any Git-managed public document, update this file in the same change.
When updating `README.md`, update `README.ja.md` in the same change, and keep both entry documents aligned in section coverage and release-facing setup information.

# DOCUMENTS.md Version
2026.06.17

This file is the public source of truth for KelpieSSH Git-managed public document locations.

## Placement Policy

- Public, Git-managed documentation is placed at the repository root, `docs/`, or the relevant feature directory.
- Japanese public documentation is placed under `docs/ja/`.
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
| `README.md` | `README.md` | Yes | Public English entry point, setup, usage, security, and license overview. |
| `README.ja.md` | `docs/ja/README.ja.md` | Yes | Public Japanese entry point. |
| `DOCUMENTS.md` | `DOCUMENTS.md` | Yes | Directory and document canonical-location index. |
| `COMMANDS.md` | `COMMANDS.md` | Yes | Public terminal CLI command reference. |
| `COMMANDS.ja.md` | `docs/ja/COMMANDS.ja.md` | Yes | Public Japanese terminal CLI command reference. |
| `MCP_COMMANDS.md` | `MCP_COMMANDS.md` | Yes | Public MCP callable tool reference. |
| `MCP_COMMANDS.ja.md` | `docs/ja/MCP_COMMANDS.ja.md` | Yes | Public Japanese MCP callable tool reference. |
| `CONFIG.md` | `CONFIG.md` | Yes | Public configuration reference. |
| `CONFIG.ja.md` | `docs/ja/CONFIG.ja.md` | Yes | Public Japanese configuration reference. |
| `PACKAGES.md` | `PACKAGES.md` | Yes | Public package, dependency, package-source, and update-policy reference. |
| `PACKAGES.ja.md` | `docs/ja/PACKAGES.ja.md` | Yes | Public Japanese package reference. |
| `SECURITY.md` | `SECURITY.md` | Yes | Public security policy and vulnerability reporting guidance. |
| `SECURITY.ja.md` | `docs/ja/SECURITY.ja.md` | Yes | Public Japanese security policy. |
| `THIRD_PARTY_NOTICES.md` | `THIRD_PARTY_NOTICES.md` | Yes | Public third-party dependency and license notices. |
| `THIRD_PARTY_NOTICES.ja.md` | `docs/ja/THIRD_PARTY_NOTICES.ja.md` | Yes | Public Japanese third-party notices. |
| `installer/README.md` | `installer/README.md` | Yes | Public MSI installer build and layout notes. |
| `installer/README.ja.md` | `docs/ja/installer/README.ja.md` | Yes | Public Japanese MSI installer notes. |
| `LICENSE` | `LICENSE` | Yes | MIT license text. |

## Private Document Handling

Private and Git-ignored internal documents are outside the scope of this public inventory.

Do not add private document names, private document paths, real-environment notes, unpublished operational details, secrets, or AI handoff details to this public file.

## Update Rule

When adding, removing, renaming, or moving any Git-managed public document, update this file in the same change.

# DOCUMENTS.md Version
2026.06.17

This file is the source of truth for KelpieSSH document locations and document ownership inside this repository.

## Placement Policy

- Public, Git-managed documentation is placed at the repository root, `docs/`, or the relevant feature directory.
- Japanese public documentation is placed under `docs/ja/`.
- Git-ignored local standards and internal notes are placed at the repository root when tool discovery requires it, or under `.local/` otherwise.
- `.local/` is not Git-managed. It may contain internal planning, progress, real-environment notes, and operational records. Do not link to `.local/` documents from public user-facing documentation.
- Generated artifacts are placed under `.artifacts/`, `bin/`, `obj/`, or `.local/progress/` depending on their purpose and Git policy.

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
| `.local/` | No | Internal notes, progress, specifications, test records, and local-only AI handoff documents. |
| `.local/docs/` | No | Internal document source of truth for specs, progress, scenarios, operational tests, and planning. |
| `.local/progress/` | No | Internal progress visualization artifacts. |
| `.artifacts/` | No | Build, publish, packaging, and MSI artifacts. |
| `bin/` / `obj/` | No | Local build outputs. |

## Document Inventory

| Document | Canonical Path | Git Managed | Purpose |
| :--- | :--- | :--- | :--- |
| `README.md` | `README.md` | Yes | Public English entry point, setup, usage, security, and license overview. |
| `README.ja.md` | `docs/ja/README.ja.md` | Yes | Public Japanese entry point. |
| `DOCUMENTS.md` | `DOCUMENTS.md` | Yes | Directory and document canonical-location index. |
| `DOC_STANDARD.md` | `DOC_STANDARD.md` | No | Local KelpieSSH-specific documentation rules that must be discoverable from the repository root. |
| `DOC_STANDARD.ja.md` | `.local/docs/DOC_STANDARD.ja.md` | No | Japanese local documentation rules. |
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
| `AGENTS.md` | `AGENTS.md` symlink to `.local/AGENTS.md` | No | AI entry document and KelpieSSH project-specific rules. |
| `AI_PROMPT.md` | `.local/AI_PROMPT.md` | No | AI common prompt handoff operation notes. |
| `PRODUCT.md` | `.local/docs/PRODUCT.md` | No | Product concept, target users, product strategy, and scope. |
| `SPEC.md` | `.local/docs/SPEC.md` | No | Internal implementation specification and design decisions. |
| `ARCHITECTURE.md` | `.local/docs/ARCHITECTURE.md` | No | Internal architecture details. |
| `API_STANDARD.md` | `.local/docs/API_STANDARD.md` | No | Internal API design rules. |
| `ERR_HANDLING.md` | `.local/docs/ERR_HANDLING.md` | No | Internal error-handling rules. |
| `PROGRESS.md` | `.local/docs/PROGRESS.md` | No | Internal progress source of truth. |
| `TODO.md` | `.local/docs/TODO.md` | No | Internal task and implementation plan. |
| `SCENARIO.md` | `.local/docs/SCENARIO.md` | No | Internal manual and operational test scenarios. |
| `OPERATIONAL_TEST.md` | `.local/docs/OPERATIONAL_TEST.md` | No | Internal manual and operational test results. |
| `COMMAND_SCENARIO.md` | `.local/docs/COMMAND_SCENARIO.md` | No | Internal terminal CLI command test scenarios. |
| `COMMAND_SCENARIO.ja.md` | `.local/docs/COMMAND_SCENARIO.ja.md` | No | Internal Japanese terminal CLI command test scenarios. |
| `COMMAND_TEST.md` | `.local/docs/COMMAND_TEST.md` | No | Internal terminal CLI command test results. |
| `COMMAND_TEST.ja.md` | `.local/docs/COMMAND_TEST.ja.md` | No | Internal Japanese terminal CLI command test results. |
| `MCP_COMMAND_SCENARIO.md` | `.local/docs/MCP_COMMAND_SCENARIO.md` | No | Internal MCP tool test scenarios. |
| `MCP_COMMAND_SCENARIO.ja.md` | `.local/docs/MCP_COMMAND_SCENARIO.ja.md` | No | Internal Japanese MCP tool test scenarios. |
| `MCP_COMMAND_TEST.md` | `.local/docs/MCP_COMMAND_TEST.md` | No | Internal MCP tool test results. |
| `MCP_COMMAND_TEST.ja.md` | `.local/docs/MCP_COMMAND_TEST.ja.md` | No | Internal Japanese MCP tool test results. |
| `MCP_TODO.md` | `.local/docs/MCP_TODO.md` | No | Internal MCP task queue. |
| `MCP_DEFFERED_COMMANDS.md` | `.local/docs/MCP_DEFFERED_COMMANDS.md` | No | Internal deferred MCP command notes. |
| `DOCKER_TEST_ENV.md` | `.local/docs/DOCKER_TEST_ENV.md` | No | Internal Docker test environment notes. |
| `RELEASE.md` | `.local/docs/RELEASE.md` | No | Internal release notes and release process notes. |
| `COMPETITORS.md` | `.local/docs/COMPETITORS.md` | No | Internal competitor and market research. |
| `progress-chart.svg` | `.local/progress/progress-chart.svg` | No | Internal progress visualization generated from `PROGRESS.md`. |

## `.local/` Handling

`.local/` contains Git-ignored documents and artifacts that may include internal planning, real-environment observations, unpublished operational details, and AI handoff notes.

Do not commit `.local/` contents unless the project policy changes explicitly. Public documents may mention that internal documents exist, but must not depend on `.local/` links for public usage.

## Update Rule

When adding, removing, renaming, or moving any continuing document, update this file in the same change.

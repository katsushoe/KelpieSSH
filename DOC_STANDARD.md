# DOC_STANDARD.md Version
2026.06.17

# Change History
- 2026.06.17

# Purpose

This file defines KelpieSSH-specific documentation rules.
For Japanese documentation, see [docs/ja/DOC_STANDARD.ja.md](docs/ja/DOC_STANDARD.ja.md).
The shared documentation standard is maintained outside this repository and is referenced by the project rules.

# Language and File Naming

- English documentation uses `.md` at the repository root or the relevant feature directory.
- Japanese documentation uses `.ja.md` under `docs/ja/`, preserving subdirectories when useful.
- Do not use `.en.md`.
- When both languages exist, the English `.md` file is the public default entry point.
- Japanese files should link back to the matching English file when useful.

# README Rules

- `README.md` is the English public entry point.
- `docs/ja/README.ja.md` is the Japanese public entry point.
- Keep setup and first-use guidance in README files.
- Move detailed command, configuration, package, and test content into topic documents.

# Command Documentation Rules

- Terminal CLI commands are documented in `COMMANDS.md` and `COMMANDS.ja.md`.
- MCP callable tools are documented in `MCP_COMMANDS.md` and `MCP_COMMANDS.ja.md`.
- Each command or tool should describe purpose, inputs, sample arguments, output, and safety notes.

# Scenario and Test Documentation Rules

- Terminal CLI test scenarios are documented in `COMMAND_SCENARIO.md` and `COMMAND_SCENARIO.ja.md`.
- Terminal CLI test results are documented in `COMMAND_TEST.md` and `COMMAND_TEST.ja.md`.
- MCP tool scenarios are documented in `MCP_COMMAND_SCENARIO.md` and `MCP_COMMAND_SCENARIO.ja.md`.
- MCP tool test results are documented in `MCP_COMMAND_TEST.md` and `MCP_COMMAND_TEST.ja.md`.
- Do not record real host names, real user names, private keys, passwords, raw secret values, or unpublished production settings.

# Compatibility Rules for MCP Clients

KelpieSSH MCP tools are still early in the public lifecycle.
Prefer clear canonical names over adding compatibility aliases for unclear names.

Add aliases only when explicitly requested.
Examples that require confirmation:

- keeping an old tool name;
- accepting an old argument name;
- accepting an old confirmation string;
- returning both old and new result fields.

# Public Documentation Rules

- Public documents must not link to Git-ignored internal notes.
- Public examples must use placeholder hosts such as `example.invalid` or documentation-safe addresses.
- Security-sensitive details belong in [SECURITY.md](SECURITY.md).
- Third-party license notices belong in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

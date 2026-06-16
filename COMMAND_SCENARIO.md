# COMMAND_SCENARIO.md Version
2026.06.17

# Change History
- 2026.06.17

# KelpieSSH Command Scenarios

This file is the English scenario reference for terminal CLI commands.
For Japanese documentation, see [COMMAND_SCENARIO.ja.md](COMMAND_SCENARIO.ja.md).
MCP callable tool scenarios are documented separately in [MCP_COMMAND_SCENARIO.md](MCP_COMMAND_SCENARIO.md).

# Preconditions

- KelpieSSH binaries are available either from the MSI installer, a manual binary layout, or a local build.
- The working environment has PowerShell available.
- Real SSH tests require a safe test host and a non-root SSH user.
- Tests must not use production hosts unless explicitly approved.

# Scenario List

| ID | Command | Purpose | Real SSH Required |
| :--- | :--- | :--- | :--- |
| `CT-001` | `kelpie version` | Verify version output. | No |
| `CT-002` | `kelpie help` | Verify help output. | No |
| `CT-003` | `kelpie init` | Verify directory and sample file generation. | No |
| `CT-004` | `kelpie init <profile>` | Verify named profile sample generation. | No |
| `CT-005` | `kelpie profiles` | Verify profile listing. | No |
| `CT-006` | `kelpie profile show <profile>` | Verify sanitized profile display. | No |
| `CT-007` | `kelpie status <profile>` | Verify MCP status and profile summary output. | No |
| `CT-008` | `kelpie cli` | Verify CLI mode selection. | No |
| `CT-009` | `kelpie gui` | Verify GUI launch path when available. | Usually |
| `CT-010` | `kelpie open <profile>` | Verify open profile state. | No |
| `CT-011` | `kelpie login` | Verify interactive SSH login. | Yes |
| `CT-012` | `kelpie login --console` | Verify console login launch. | Yes |
| `CT-013` | `kelpie login --desktop` | Verify desktop login launch. | Yes |
| `CT-014` | `kelpie sessions` | Verify session listing. | No |
| `CT-015` | `kelpie kill <session>` | Verify safe session termination handling. | No |
| `CT-016` | `kelpie diag <profile>` | Verify high-level SSH diagnostics. | Yes |
| `CT-017` | `kelpie logs <profile> <service>` | Verify service log retrieval. | Yes |
| `CT-018` | `kelpie logs <profile> bad;service` | Verify unsafe service-name rejection. | No |
| `CT-019` | `kelpiemcp status` | Verify MCP server status output. | No |
| `CT-020` | `kelpiemcp start` | Verify MCP server startup. | No |
| `CT-021` | `kelpiemcp stop` | Verify MCP server shutdown. | No |
| `CT-022` | `kelpiemcp password <profile>` | Verify password session storage flow. | Interactive |
| `CT-023` | `kelpiemcp forget <profile>` | Verify password session clearing. | No |

# Safety Notes

- Direct root login is not allowed.
- Passwords must not be stored in JSON files.
- Tests that modify real systems require explicit confirmation and a disposable target.
- Keep raw logs and secrets out of committed test results.


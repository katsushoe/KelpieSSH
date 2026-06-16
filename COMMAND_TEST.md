# COMMAND_TEST.md Version
2026.06.17

# Change History
- 2026.06.17

# KelpieSSH Command Test Results

This file is the English summary of terminal CLI command test results.
For Japanese documentation, see [docs/ja/COMMAND_TEST.ja.md](docs/ja/COMMAND_TEST.ja.md).
Detailed scenarios are maintained in [COMMAND_SCENARIO.md](COMMAND_SCENARIO.md).

# Result Codes

| Code | Meaning |
| :--- | :--- |
| `OK` | The command behaved as expected. |
| `NG` | The command did not behave as expected. |
| `SKIP` | The test was not applicable or required unavailable prerequisites. |
| `PENDING` | The test has not been run yet. |

# Result Summary

| Date | Target Version | Tester | Environment | OK | NG | SKIP | PENDING | Notes |
| :--- | :--- | :--- | :--- | ---: | ---: | ---: | ---: | :--- |
| 2026.06.11 | `kelpie 0.1.3.3`, `kelpiemcp 0.1.1.2`, `KelpieMCPServer 0.1.4.2` | Codex | Windows PowerShell / `C:\Tmp\KelpieCommandTest` | 15 | 0 | 10 | 0 | Automated checks were rerun after NG fixes where possible. `dotnet build` completed with 0 warnings and 0 errors. `dotnet test` passed 139 tests. |

# Tested Areas

The command test set covers:

- `kelpie version` and help output;
- `kelpie init` and profile sample generation;
- profile listing and sanitized profile display;
- open profile state;
- CLI/GUI mode commands where automation is safe;
- MCP server status/start/stop behavior;
- password session commands where safe to automate;
- rejection of unsafe service-name input;
- behavior that requires real SSH is marked `SKIP`.

# Safety Notes

Tests must not record real host names, real user names, passwords, private keys, passphrases, raw logs containing secrets, or unpublished production settings.

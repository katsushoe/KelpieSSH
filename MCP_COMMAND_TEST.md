# MCP_COMMAND_TEST.md Version
2026.06.17

# Change History
- 2026.06.17

# KelpieSSH MCP Tool Test Results

This file is the English summary of MCP callable tool test results.
For Japanese documentation, see [MCP_COMMAND_TEST.ja.md](MCP_COMMAND_TEST.ja.md).
MCP tool test scenarios are documented in [MCP_COMMAND_SCENARIO.md](MCP_COMMAND_SCENARIO.md).

# Result Codes

| Code | Meaning |
| :--- | :--- |
| `OK` | The tool behaved as expected. |
| `NG` | The tool did not behave as expected. |
| `SKIP` | The test was not applicable or required unavailable prerequisites. |
| `PENDING` | The test has not been run yet. |

# Result Summary

| Date | Target Version | Tester | Environment | OK | NG | SKIP | PENDING | Notes |
| :--- | :--- | :--- | :--- | ---: | ---: | ---: | ---: | :--- |
| 2026.06.17 | `KelpieMCPServer 0.1.29.0` and related packages | Codex | Local MCP endpoint and safe SSH targets | 120 | 0 | 0 | 0 | `get_target_inventory` / `target_inventory` were verified through MCP and `ssh_run_allowed_command`. |

# Verified Areas

- MCP initialize and server info.
- `tools/list` includes expected tools.
- Profile-based SSH diagnostic tools.
- Target inventory collection.
- Dedicated confirmation behavior for maintenance operations.
- Safe rejection for missing profiles and unreachable sample profiles.
- Sanitized public error responses.

# Safety Notes

Do not record real host details, real user names, passwords, private keys, raw file bodies, or raw log bodies in test results.


# MCP_COMMAND_SCENARIO.md Version
2026.06.17

# Change History
- 2026.06.17

# KelpieSSH MCP Tool Scenarios

This file is the English scenario reference for MCP callable tools.
For Japanese documentation, see [docs/ja/MCP_COMMAND_SCENARIO.ja.md](docs/ja/MCP_COMMAND_SCENARIO.ja.md).
Tool details are documented in [MCP_COMMANDS.md](MCP_COMMANDS.md).

# Preconditions

- `KelpieMCPServer` is running.
- The MCP endpoint is reachable, usually at `http://127.0.0.1:45432/mcp`.
- Profile-based SSH tests require a safe SSH profile.
- Change-oriented tests require disposable targets and explicit confirmations.

# Scenario Groups

| Group | Purpose |
| :--- | :--- |
| Server health | Verify initialize, server info, health, and basic local diagnostics. |
| SSH read diagnostics | Verify allow-listed read-only SSH commands. |
| Inventory | Verify target OS, helper, and software inventory. |
| Package tools | Verify package query, dry-run, and confirmation-gated changes. |
| Service tools | Verify systemd state queries and confirmation-gated service changes. |
| Service config/log tools | Verify provider-approved configuration and logfile access. |
| Web file tools | Verify provider-approved web file and permission operations. |
| Audit/backup/support tools | Verify safe collection and confirmation behavior. |

# Core Scenarios

| ID | Tool | Purpose | Real SSH Required | Real Change Required |
| :--- | :--- | :--- | :--- | :--- |
| `MT-001` | `kelpie_ping` | Verify server reachability. | No | No |
| `MT-002` | `get_system_info` | Verify local host system information. | No | No |
| `MT-003` | `get_disk_usage` | Verify local disk usage output. | No | No |
| `MT-004` | `get_memory_usage` | Verify local memory usage output. | No | No |
| `MT-005` | `get_listening_ports` | Verify local listening port output. | No | No |
| `MT-010` | `ssh_get_system_info` | Verify SSH system information. | Yes | No |
| `MT-011` | `ssh_get_os_release` | Verify SSH OS release output. | Yes | No |
| `MT-012` | `ssh_get_disk_usage` | Verify SSH disk usage output. | Yes | No |
| `MT-013` | `ssh_get_memory_usage` | Verify SSH memory usage output. | Yes | No |
| `MT-014` | `ssh_tail_log` | Verify safe service log tailing. | Yes | No |
| `MT-020` | `ssh_run_allowed_command` | Verify generic allow-listed command execution. | Yes | No |
| `MT-021` | `ssh_run_remote_operation` | Verify operation-based execution without a saved profile name. | Yes | No |
| `MT-022` | `get_target_inventory` | Verify structured target inventory. | Yes | No |
| `MT-030` | `service_config_file_check_read` | Verify non-mutating read access check. | Yes | No |
| `MT-031` | `service_config_file_check_write` | Verify non-mutating write access check. | Yes | No |
| `MT-032` | `service_config_file_write` | Verify confirmation-gated write. | Yes | Yes |
| `MT-040` | `web_file_list` | Verify provider-approved web file listing. | Yes | No |
| `MT-041` | `web_file_read` | Verify provider-approved web file read. | Yes | No |
| `MT-042` | `web_file_write` | Verify confirmation-gated web file write. | Yes | Yes |

# Expected Safety Behavior

- Unknown or missing profiles return sanitized errors.
- Missing confirmation strings do not mutate remote state.
- Provider-denied paths are rejected.
- Root login is rejected.
- Secrets are not returned through MCP results.
- Dedicated maintenance commands cannot be executed through generic `ssh_run_allowed_command` when a dedicated MCP tool exists.

# Cleanup Rules

- If `service_config_file_write` is executed, close the backup workflow with `service_config_file_commit` or `service_config_file_rollback`.
- Web file and permission changes must be limited to disposable test paths.
- Do not run recursive permission changes against production site roots.
- Record only sanitized summaries in committed test result documents.

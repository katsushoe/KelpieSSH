# KelpieSSH MCP Commands

Last updated: 2026-06-17

This file is the English reference for MCP callable tools exposed by `KelpieMCPServer`.
For Japanese documentation, see [MCP_COMMANDS.ja.md](MCP_COMMANDS.ja.md).
Terminal CLI commands are documented in [COMMANDS.md](COMMANDS.md).

# Tool Groups

| Group | Tools | Purpose |
| :--- | :--- | :--- |
| Server health | `kelpie_ping` | Verify that the MCP server is reachable. |
| Local diagnostics | `get_system_info`, `get_disk_usage`, `get_memory_usage`, `get_listening_ports` | Inspect the local host running `KelpieMCPServer`. |
| SSH diagnostics | `ssh_get_system_info`, `ssh_get_os_release`, `ssh_get_uptime`, `ssh_get_disk_usage`, `ssh_get_memory_usage`, `ssh_get_process_summary`, `ssh_get_inode_usage`, `ssh_get_mounts`, `ssh_get_network_addresses`, `ssh_get_routes`, `ssh_get_dns_config`, `ssh_get_listening_ports`, `ssh_get_failed_services`, `ssh_get_journal_recent`, `ssh_tail_log` | Run allow-listed read-oriented diagnostics over SSH. |
| Inventory and capabilities | `ssh_get_capabilities`, `get_target_inventory` | Inspect target command/tool support and installed helper/software inventory. |
| Generic execution | `ssh_run_allowed_command`, `ssh_run_remote_operation` | Run an allow-listed managed operation through policy checks. |
| Terminal | `ssh_terminal_open`, `ssh_terminal_send`, `ssh_terminal_snapshot`, `ssh_terminal_close` | Manage an interactive SSH terminal session. |
| Packages | `ssh_pkg_check_updates`, `ssh_pkg_info`, `ssh_pkg_search`, `ssh_pkg_list_installed`, `ssh_pkg_simulate_install`, `ssh_pkg_install`, `ssh_pkg_install_confirmed`, `ssh_pkg_simulate_remove`, `ssh_pkg_remove` | Inspect packages and run confirmation-gated package operations. |
| Services | `ssh_service_status`, `ssh_service_is_active`, `ssh_service_is_enabled`, `ssh_list_services`, `ssh_service_enable_now`, `ssh_service_reload`, `ssh_service_restart`, `ssh_service_stop`, `ssh_service_disable` | Inspect and safely manage systemd services. |
| Service config/logs | `service_config_paths`, `service_config_file_check_read`, `service_config_file_read`, `service_config_file_check_write`, `service_config_file_write`, `service_config_file_rollback`, `service_config_file_commit`, `service_config_test`, `service_logfile_read` | Operate on provider-approved service configuration files and logs. |
| Web files | `web_file_list`, `web_file_search_name`, `web_file_search_text`, `web_file_stat`, `web_file_check_write`, `web_file_check_permissions`, `web_file_read`, `web_file_head`, `web_file_tail`, `web_file_write`, `web_change_owner`, `web_change_owner_recursive`, `web_change_mode`, `web_change_mode_recursive` | Operate on provider-approved web roots. |

# Common Inputs

Most existing SSH tools accept:

- `profileName`: the saved profile name under `KelpieHome/profiles`.
- tool-specific arguments such as `service`, `path`, `lines`, or `limit`.

`ssh_run_remote_operation` accepts an `SshRemoteOperation` directly instead of a profile name.
This operation includes endpoint, credential, policy, operation, options, and optional target metadata.

Saved profiles are host-side persistence adapters.
They are converted into `SshRemoteOperation` before execution.

# Common Result Shape

SSH command tools usually return:

- `ProfileName` or `CorrelationId`;
- `Host`;
- `Port`;
- `UserName`;
- `CommandName`;
- `CommandText`;
- `ExitCode`;
- `StandardOutput`;
- `StandardError`;
- `Stdout` / `Stderr`;
- `StdoutPlain` / `StderrPlain`;
- `StartedAt`;
- `CompletedAt`;
- `TimedOut`.

# Example: `ssh_run_allowed_command`

```json
{
  "profileName": "vps01",
  "commandName": "get_system_info",
  "arguments": {}
}
```

# Example: `ssh_run_remote_operation`

```json
{
  "operation": {
    "endpoint": {
      "host": "203.0.113.10",
      "port": 22
    },
    "credential": {
      "user_name": "deploy",
      "kind": "private_key",
      "private_key_path": "id_ed25519"
    },
    "policy": {
      "mode": "maintenance",
      "roles": ["web_admin"],
      "allowed_roots": [
        {
          "path": "/var/www/example",
          "access": ["read", "list", "write", "cd"]
        }
      ],
      "special_paths": [
        {
          "pattern": "**/.env",
          "action": "deny"
        }
      ]
    },
    "operation": {
      "kind": "managed",
      "name": "service_status",
      "arguments": {
        "service": "nginx"
      }
    },
    "options": {
      "timeout_seconds": 30,
      "correlation_id": "op-example"
    },
    "target": {
      "os_family": "debian",
      "package_manager": "apt"
    }
  }
}
```

# Confirmation Rules

Tools that can change a remote target require a `confirmation` string.
If the confirmation is missing or does not match, the tool returns a confirmation-required result and does not perform the change.

# Safety Notes

- MCP execution never displays passwords or private keys.
- Direct root SSH login is rejected.
- Managed commands must exist in the allow-listed command catalog.
- Raw operations must pass raw shell policy checks.
- Service configuration and web file operations are limited to provider-approved paths.
- Real host names, real user names, secrets, raw log bodies containing secrets, and unpublished settings must not be recorded in committed documents.


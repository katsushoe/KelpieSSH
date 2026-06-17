# KelpieSSH MCP Commands

Last updated: 2026-06-17

This file is the English command reference for MCP callable tools exposed by `KelpieMCPServer`.
For Japanese documentation, see [docs/ja/MCP_COMMANDS.ja.md](docs/ja/MCP_COMMANDS.ja.md).
Terminal CLI commands are documented in [COMMANDS.md](COMMANDS.md).

`MCP_COMMANDS.md` follows the same command-reference standard as `COMMANDS.md`: each tool group documents its purpose, input fields, input examples, execution behavior, result shape, and safety notes. Individual tool schemas are exposed by MCP `tools/list`; this document explains how those tools are intended to be used safely.

## How MCP Tools Are Called

KelpieSSH MCP tools are not REST resources. They are MCP JSON-RPC methods carried over the Streamable HTTP MCP transport.

In normal AI client usage, the user does not call these HTTP requests directly. Codex, Claude, or another MCP client connects to the local Streamable HTTP endpoint, discovers tools, and sends tool calls on the user's behalf.

The default endpoint is documented in [MCP_GUIDE.md](MCP_GUIDE.md). A typical configured endpoint is:

```text
http://127.0.0.1:45432/mcp
```

The usual flow is:

1. The MCP client sends `initialize` to establish protocol capabilities.
2. The MCP client sends `tools/list` to discover available tool names and JSON schemas.
3. The MCP client sends `tools/call` with the selected tool name and arguments.
4. `KelpieMCPServer` validates the request, resolves any saved profile or `SshRemoteOperation`, applies policy checks, runs the allowed operation, and returns the result to the MCP client.

The HTTP request body is JSON-RPC, not a REST-style resource request. For example, a direct diagnostic call has this shape:

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "get_target_inventory",
    "arguments": {
      "profileName": "vps01"
    }
  }
}
```

This document describes the `name` and `arguments` used inside `tools/call`. AI users normally only need the tool behavior and safety notes; MCP client implementers may also need the JSON-RPC flow above.

## Tool Groups

| Group | Tools | Purpose |
| :--- | :--- | :--- |
| Server health | `kelpie_ping` | Verify that the MCP server is reachable. |
| Local diagnostics | `get_system_info`, `get_disk_usage`, `get_memory_usage`, `get_listening_ports` | Inspect the local host running `KelpieMCPServer`. |
| Capabilities and inventory | `ssh_get_capabilities`, `get_target_inventory` | Inspect target command/tool support and installed helper/software inventory. |
| SSH diagnostics | `ssh_get_system_info`, `ssh_get_os_release`, `ssh_get_uptime`, `ssh_get_disk_usage`, `ssh_get_memory_usage`, `ssh_get_process_summary`, `ssh_get_inode_usage`, `ssh_get_mounts`, `ssh_get_network_addresses`, `ssh_get_routes`, `ssh_get_dns_config`, `ssh_check_http_local`, `ssh_check_tcp_connect_local`, `ssh_get_listening_ports`, `ssh_get_failed_services`, `ssh_get_journal_recent`, `ssh_tail_log` | Run allow-listed read-oriented diagnostics over SSH. |
| Cron, certificate, user, firewall, backup, and audit checks | `ssh_cron_list`, `ssh_cron_validate`, `ssh_cron_check_write`, `ssh_cron_write`, `ssh_cron_rollback`, `ssh_cert_inspect`, `ssh_cert_expiry_check`, `ssh_user_list`, `ssh_user_info`, `ssh_group_list`, `ssh_group_info`, `ssh_sudoers_check`, `ssh_user_usage_check`, `ssh_user_check_group_change`, `ssh_user_apply_group_change`, `ssh_user_rollback_group_change`, `ssh_user_check_permission_change`, `ssh_user_apply_permission_change`, `ssh_user_rollback_permission_change`, `ssh_user_file_ownership_check`, `ssh_user_service_usage_check`, `ssh_service_residual_config_check`, `ssh_support_report_collect`, `ssh_firewall_status`, `ssh_firewall_check_rule`, `ssh_firewall_apply_rule`, `ssh_backup_plan_check`, `ssh_backup_run`, `ssh_backup_verify`, `ssh_audit_verify`, `ssh_audit_export` | Inspect or change sensitive server-maintenance state through bounded checks and confirmation-gated operations. |
| Generic execution | `ssh_run_allowed_command`, `ssh_run_remote_operation` | Run an allow-listed managed operation through policy checks. |
| Terminal | `ssh_terminal_open`, `ssh_terminal_send`, `ssh_terminal_snapshot`, `ssh_terminal_close` | Manage an interactive SSH terminal session. |
| Packages | `ssh_pkg_check_updates`, `ssh_pkg_info`, `ssh_pkg_search`, `ssh_pkg_list_installed`, `ssh_pkg_simulate_install`, `ssh_pkg_install`, `ssh_pkg_install_confirmed`, `ssh_pkg_simulate_remove`, `ssh_pkg_remove` | Inspect packages and run confirmation-gated package operations. |
| Services | `ssh_service_status`, `ssh_service_is_active`, `ssh_service_is_enabled`, `ssh_list_services`, `ssh_service_enable_now`, `ssh_service_reload`, `ssh_service_restart`, `ssh_service_stop`, `ssh_service_disable` | Inspect and safely manage systemd services. |
| Service config/logs | `service_config_paths`, `service_config_file_check_read`, `service_config_file_read`, `service_config_file_check_write`, `service_config_file_write`, `service_config_file_rollback`, `service_config_file_commit`, `service_config_test`, `service_logfile_read` | Operate on provider-approved service configuration files and logs. |
| Web files | `web_file_list`, `web_file_search_name`, `web_file_search_text`, `web_file_stat`, `web_file_check_write`, `web_file_check_permissions`, `web_file_read`, `web_file_head`, `web_file_tail`, `web_file_write`, `web_change_owner`, `web_change_owner_recursive`, `web_change_mode`, `web_change_mode_recursive` | Operate on provider-approved web roots. |

## Common Inputs

Most SSH target tools accept:

- `profileName`: Saved profile name under `KelpieHome/profiles`.
- Tool-specific arguments such as `service`, `path`, `lines`, `limit`, `packageName`, `siteKey`, or `confirmation`.

`ssh_run_remote_operation` accepts `operation` instead of `profileName`. The value is an `SshRemoteOperation` containing `endpoint`, `credential`, `policy`, `operation`, `options`, and optional `target` metadata.

Saved profiles are host-side persistence adapters. They are converted into `SshRemoteOperation` before execution. Product concepts such as profile count limits, edition limits, license state, ads, support, display order, notes, and customer data are not MCP tool inputs.

## Common Result Shapes

SSH command tools usually return `SshToolResult`:

- `ProfileName`
- `Host`
- `Port`
- `UserName`
- `CommandName`
- `CommandText`
- `ExitCode`
- `StandardOutput`
- `StandardError`
- `Stdout` / `Stderr`
- `StdoutPlain` / `StderrPlain`
- `StartedAt`
- `CompletedAt`
- `TimedOut`
- `Error`

Tools that perform preflight checks usually return a result containing:

- the resolved target;
- whether the operation can proceed;
- a `confirmation` string if a later write/apply tool is required;
- `warnings` and `error` fields.

Tools that can change a remote target require an exact `confirmation` string. If the confirmation is missing or does not match, the tool returns a confirmation-required result and does not perform the change.

## Commands

### `kelpie_ping`

Purpose:

Verifies that `KelpieMCPServer` is running and can answer MCP tool calls.

Input arguments:

- None.

Argument sample:

```json
{}
```

Execution:

Returns a small text response without contacting any SSH target.

Result sample:

```text
KelpieSSH MCP server is running.
```

Safety notes:

- Read-only.

### Local diagnostics

Tools:

- `get_system_info`
- `get_disk_usage`
- `get_memory_usage`
- `get_listening_ports`

Purpose:

Inspects the local machine running `KelpieMCPServer`.

Input arguments:

- None.

Argument sample:

```json
{}
```

Execution:

Collects local OS, runtime, disk, memory, or listening-port information. These tools do not connect to SSH profiles.

Result sample:

```json
{
  "MachineName": "HOST",
  "OSDescription": "Microsoft Windows ...",
  "ProcessId": 1234,
  "BaseDirectory": "D:\\Kelpie\\bin\\mcp\\"
}
```

Safety notes:

- Read-only.
- Local listening-port data may include process IDs. Do not paste raw results into public issues if they reveal private environment details.

### `ssh_get_capabilities`

Purpose:

Checks which SSH commands and MCP tools are available for a specific profile.

Input arguments:

- `profileName`: SSH profile name.

Argument sample:

```json
{
  "profileName": "vps01"
}
```

Execution:

Runs a fixed read-only OS probe and combines the result with profile mode and provider support. This is the dynamic per-profile capability check; MCP `tools/list` only shows the static product tool list.

Result sample:

```json
{
  "ProfileName": "vps01",
  "OsFamily": "alma",
  "PackageManager": "dnf",
  "ProbeSucceeded": true,
  "Commands": [
    {
      "CommandName": "pkg_search",
      "RiskLevel": "ReadOnly",
      "RequiresConfirmation": false
    }
  ],
  "Tools": [
    {
      "ToolName": "ssh_pkg_search",
      "CommandName": "pkg_search",
      "Available": true
    }
  ]
}
```

Safety notes:

- Read-only.
- Do not run unavailable alternatives automatically. Explain the reason and ask the user before changing packages, services, or configuration.

### `get_target_inventory`

Purpose:

Returns read-only OS, helper, and software inventory for a configured SSH profile.

Input arguments:

- `profileName`: SSH profile name.

Argument sample:

```json
{
  "profileName": "vps02"
}
```

Execution:

Runs the allow-listed `target_inventory` operation. Individual helper/software probes have short per-item timeouts. A missing helper is reported as `Not Available`; the tool fails only when the SSH connection or OS probe fails.

Result sample:

```json
{
  "Profile": "vps02",
  "Os": {
    "Family": "alma",
    "Name": "AlmaLinux",
    "Version": "9.8",
    "PackageManager": "dnf"
  },
  "Helpers": [
    {
      "Name": "Python",
      "Status": "Available",
      "Version": "3.9.25"
    }
  ],
  "Software": [
    {
      "Name": "nginx",
      "Status": "Available",
      "Version": "1.20.1"
    }
  ]
}
```

Safety notes:

- Read-only.
- Does not install missing software.
- Does not return file bodies, private keys, passwords, or raw log bodies.

### SSH diagnostic tools

Tools:

- `ssh_get_system_info`
- `ssh_get_os_release`
- `ssh_get_uptime`
- `ssh_get_disk_usage`
- `ssh_get_memory_usage`
- `ssh_get_process_summary`
- `ssh_get_inode_usage`
- `ssh_get_mounts`
- `ssh_get_network_addresses`
- `ssh_get_routes`
- `ssh_get_dns_config`
- `ssh_check_http_local`
- `ssh_check_tcp_connect_local`
- `ssh_get_listening_ports`
- `ssh_get_failed_services`
- `ssh_get_journal_recent`
- `ssh_tail_log`

Purpose:

Runs bounded, allow-listed diagnostic commands on the SSH target.

Common input arguments:

- `profileName`: SSH profile name.
- `limit`: Maximum row count for bounded list tools.
- `lines`: Maximum log lines for log tools.
- `service`: systemd service name for service/log tools.
- `port`: Local port for `ssh_check_http_local` and `ssh_check_tcp_connect_local`.

Argument sample:

```json
{
  "profileName": "vps01",
  "service": "nginx.service",
  "lines": "100"
}
```

Execution:

Each tool maps to one managed command from the allow-list. Arguments are validated before command text is built. Arbitrary shell options are not accepted.

Result sample:

```json
{
  "ProfileName": "vps01",
  "CommandName": "tail_log",
  "ExitCode": 0,
  "StandardOutput": "Jun 17 12:00:00 host nginx[123]: started\n",
  "TimedOut": false
}
```

Safety notes:

- Read-oriented by default.
- Log output can contain application data. Avoid copying raw log bodies into public documents.
- Service names, limits, ports, and path-like arguments are validated.

### Generic execution tools

Tools:

- `ssh_run_allowed_command`
- `ssh_run_remote_operation`

Purpose:

Runs one allow-listed managed command, either through a saved profile or through a one-off `SshRemoteOperation`.

Input arguments:

- `profileName`: SSH profile name for `ssh_run_allowed_command`.
- `commandName`: managed command name for `ssh_run_allowed_command`.
- `arguments`: command-specific key/value arguments.
- `operation`: complete `SshRemoteOperation` for `ssh_run_remote_operation`.

Argument sample:

```json
{
  "profileName": "vps01",
  "commandName": "get_system_info",
  "arguments": {}
}
```

`SshRemoteOperation` sample:

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

Execution:

For managed operations, Kelpie resolves the command from the command catalog and evaluates mode, roles, allowed roots, special paths, and channel policy. For raw operations, raw shell policy must also pass.

Result sample:

```json
{
  "ProfileName": "vps01",
  "CommandName": "get_system_info",
  "ExitCode": 0,
  "StandardOutput": "Linux example ...\n"
}
```

Safety notes:

- Unknown `commandName` values are rejected.
- `SshRemoteOperation` is a single execution input, not a saved-profile or product-edition model.
- Product policy such as Free/Standard limits is outside this API boundary.

### Terminal tools

Tools:

- `ssh_terminal_open`
- `ssh_terminal_send`
- `ssh_terminal_snapshot`
- `ssh_terminal_close`

Purpose:

Manages a PTY-backed interactive SSH terminal session.

Input arguments:

- `profileName`: SSH profile name for `ssh_terminal_open`.
- `handle`: terminal session handle returned by `ssh_terminal_open`.
- `input`: raw input sent by `ssh_terminal_send`.
- `columns`: terminal width, optional.
- `rows`: terminal height, optional.

Argument sample:

```json
{
  "profileName": "vps01",
  "columns": 120,
  "rows": 40
}
```

Execution:

`ssh_terminal_open` creates a session and returns a rendered screen snapshot. `ssh_terminal_send` writes input to the session. `ssh_terminal_snapshot` returns the current rendered screen. `ssh_terminal_close` closes the session.

Result sample:

```json
{
  "Handle": "term-123",
  "ProfileName": "vps01",
  "Screen": {
    "Columns": 120,
    "Rows": 40,
    "Text": "..."
  }
}
```

Safety notes:

- Terminal input is interactive and may have side effects on the remote host.
- Raw shell policy still applies.
- Close unused sessions with `ssh_terminal_close`.

### Package tools

Tools:

- `ssh_pkg_check_updates`
- `ssh_pkg_info`
- `ssh_pkg_search`
- `ssh_pkg_list_installed`
- `ssh_pkg_simulate_install`
- `ssh_pkg_install`
- `ssh_pkg_install_confirmed`
- `ssh_pkg_simulate_remove`
- `ssh_pkg_remove`

Purpose:

Inspects packages and gates package changes behind dry-run and confirmation flows.

Input arguments:

- `profileName`: SSH profile name.
- `packageName`: package name for package-specific tools.
- `query`: package search text.
- `limit`: maximum row count for list/search tools.
- `confirmation`: required for confirmed install/remove execution.

Argument sample:

```json
{
  "profileName": "vps01",
  "packageName": "nginx"
}
```

Execution:

Read tools query package metadata. `ssh_pkg_simulate_install` and `ssh_pkg_simulate_remove` run dry-run commands. `ssh_pkg_install` and `ssh_pkg_remove` return a confirmation request rather than making changes directly. Confirmed execution requires the matching confirmation string.

Confirmation examples:

```text
pkg_install:nginx
pkg_remove:nginx
```

Safety notes:

- Package install/remove changes the remote target and must be explicitly confirmed.
- Package manager support depends on detected OS and configured provider.

### Service tools

Tools:

- `ssh_service_status`
- `ssh_service_is_active`
- `ssh_service_is_enabled`
- `ssh_list_services`
- `ssh_service_enable_now`
- `ssh_service_reload`
- `ssh_service_restart`
- `ssh_service_stop`
- `ssh_service_disable`

Purpose:

Inspects and safely manages systemd services.

Input arguments:

- `profileName`: SSH profile name.
- `service`: systemd service name.
- `state`: optional state filter for list tools.
- `limit`: maximum row count.
- `confirmation`: required for service state changes.

Argument sample:

```json
{
  "profileName": "vps01",
  "service": "nginx.service"
}
```

Execution:

Status tools run read-only `systemctl` checks. Change tools run `systemctl enable --now`, `reload`, `restart`, `stop`, or `disable` only after confirmation.

Confirmation examples:

```text
service_restart:nginx.service
service_stop:nginx.service
```

Safety notes:

- Service changes can interrupt workloads.
- Service names are validated and arbitrary shell fragments are rejected.

### Service configuration and log tools

Tools:

- `service_config_paths`
- `service_config_file_check_read`
- `service_config_file_read`
- `service_config_file_check_write`
- `service_config_file_write`
- `service_config_file_rollback`
- `service_config_file_commit`
- `service_config_test`
- `service_logfile_read`

Purpose:

Operates on provider-approved service configuration files and logs.

Input arguments:

- `profileName`: SSH profile name.
- `service`: supported service key or service name.
- `path`: provider-approved configuration or log path.
- `contentBase64`: replacement file content for writes.
- `confirmation`: required for write, rollback, commit, and test actions.

Argument sample:

```json
{
  "profileName": "vps01",
  "service": "nginx",
  "path": "/etc/nginx/nginx.conf"
}
```

Execution:

Check tools validate access without returning content or making changes. Read tools return bounded file content. Write tools create a backup before applying changes. Rollback restores the backup. Commit removes the Kelpie backup after the user accepts the edit.

Safety notes:

- Only provider-approved paths are accessible.
- Configuration writes are confirmation-gated.
- Log reads are bounded; avoid sharing raw logs publicly.

### Web file tools

Tools:

- `web_file_list`
- `web_file_search_name`
- `web_file_search_text`
- `web_file_stat`
- `web_file_check_write`
- `web_file_check_permissions`
- `web_file_read`
- `web_file_head`
- `web_file_tail`
- `web_file_write`
- `web_change_owner`
- `web_change_owner_recursive`
- `web_change_mode`
- `web_change_mode_recursive`

Purpose:

Operates on provider-approved web roots using site-relative absolute paths.

Input arguments:

- `profileName`: SSH profile name.
- `siteKey`: web site configuration key.
- `path`: site-relative absolute path such as `/index.html`.
- `pattern`: file-name glob for name search.
- `query`: text search query.
- `maxDepth`, `limit`, `maxBytes`, `maxLines`: bounded read/search controls.
- `contentBase64`: replacement file content for writes.
- `owner`, `group`, `mode`: optional owner/group/mode changes.
- `confirmation`: required for write and permission changes.

Argument sample:

```json
{
  "profileName": "vps01",
  "siteKey": "default",
  "path": "/index.html"
}
```

Write sample:

```json
{
  "profileName": "vps01",
  "siteKey": "default",
  "path": "/index.html",
  "contentBase64": "PGgxPkhlbGxvIEtlbHBpZTwvaDE+Cg==",
  "contentType": "text/html",
  "encoding": "utf-8",
  "confirmation": "web_file_write:default:/index.html"
}
```

Execution:

List/search/read tools resolve paths inside the configured web root and refuse traversal outside the root. Write and permission tools require confirmation and revalidate target paths before changing anything.

Confirmation examples:

```text
web_file_write:default:/index.html
web_change_owner:default:/my_dir/index.html:www-data:www-data
web_change_mode:default:/my_dir/index.html:775
```

Safety notes:

- `path` must stay inside the configured web root.
- Recursive owner/mode changes skip symlinks and reject symlink targets.
- `owner` / `group` cannot be `root` or `0`.
- `mode` must be three octal digits and cannot be world-writable.
- Owner/mode operations use the dedicated Kelpie web permission helper; do not grant broad sudo permissions to `python3`, `chown`, or `chmod`.

### Cron, certificate, user, firewall, backup, and audit tools

Tools:

- Cron: `ssh_cron_list`, `ssh_cron_validate`, `ssh_cron_check_write`, `ssh_cron_write`, `ssh_cron_rollback`
- Certificates: `ssh_cert_inspect`, `ssh_cert_expiry_check`
- Users/groups: `ssh_user_list`, `ssh_user_info`, `ssh_group_list`, `ssh_group_info`, `ssh_sudoers_check`, `ssh_user_usage_check`, `ssh_user_file_ownership_check`, `ssh_user_service_usage_check`
- User changes: `ssh_user_check_group_change`, `ssh_user_apply_group_change`, `ssh_user_rollback_group_change`, `ssh_user_check_permission_change`, `ssh_user_apply_permission_change`, `ssh_user_rollback_permission_change`
- Residual config: `ssh_service_residual_config_check`
- Support report: `ssh_support_report_collect`
- Firewall: `ssh_firewall_status`, `ssh_firewall_check_rule`, `ssh_firewall_apply_rule`
- Backup: `ssh_backup_plan_check`, `ssh_backup_run`, `ssh_backup_verify`
- Audit: `ssh_audit_verify`, `ssh_audit_export`

Purpose:

Provides bounded maintenance checks and confirmation-gated changes for sensitive server state.

Input arguments:

- `profileName`: SSH profile name.
- Tool-specific names such as `user`, `group`, `service`, `path`, `zone`, `port`, `protocol`, or `scope`.
- `confirmation`: required for write/apply/run/rollback operations.

Argument sample:

```json
{
  "profileName": "vps01",
  "user": "deploy",
  "group": "web-admin"
}
```

Execution:

Check tools inspect current state and return a safe summary plus a confirmation token when a matching apply tool exists. Apply/run/rollback tools require the exact confirmation token and revalidate the target before changing anything.

Safety notes:

- These tools can affect login, sudo, firewall, backup, audit, or scheduler behavior.
- Use the check tool first, review the returned diff or plan, then call the apply/run tool only with the returned confirmation token.
- Do not record raw sudoers content, raw audit logs, host-specific secrets, or customer data in public documents.

## Safety Notes

- MCP callable tools are separate from terminal commands such as `kelpie` and `kelpiemcp`.
- MCP execution never displays passwords or private keys.
- Direct root SSH login is rejected.
- Managed commands must exist in the allow-listed command catalog.
- Raw operations must pass raw shell policy checks.
- Confirmation-gated tools do not change the target unless the confirmation string matches exactly.
- Service configuration and web file operations are limited to provider-approved paths.
- Real host names, real user names, secrets, raw log bodies containing secrets, and unpublished settings must not be recorded in committed documents.

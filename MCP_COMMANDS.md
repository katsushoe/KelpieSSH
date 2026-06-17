# KelpieSSH MCP Commands

Last updated: 2026-06-18

This file is the English command reference for MCP callable tools exposed by `KelpieMCPServer`.
For Japanese documentation, see [docs/ja/MCP_COMMANDS.ja.md](docs/ja/MCP_COMMANDS.ja.md).
Terminal CLI commands are documented in [COMMANDS.md](COMMANDS.md).

`MCP_COMMANDS.md` follows the same command-reference standard as `COMMANDS.md`: MCP tools are grouped by operational area, and each MCP tool has its own subsection with purpose, input fields, input examples, execution behavior, return value specification and sample, execution result sample, and safety notes. Individual tool schemas are exposed by MCP `tools/list`; this document explains how those tools are intended to be used safely.

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

This document describes the `name` and `arguments` used inside `tools/call`. In each tool subsection, the call sample shows the `params` object that goes inside a JSON-RPC `tools/call` request. Return value samples show the `structuredContent` object or text content returned by the tool after execution. AI users normally only need the tool behavior and safety notes; MCP client implementers may also need the JSON-RPC flow above.

## Tool Groups

| Group | Tools | Purpose |
| :--- | :--- | :--- |
| [Server health](#server-health) | `kelpie_ping` | Verify that the MCP server is reachable. |
| [Profile management](#profile-management) | `profile_reload`, `ssh_profile_capabilities` | Reload saved SSH profiles and inspect profile operation capabilities for an open SSH terminal connection. |
| [Local diagnostics](#local-diagnostics) | `get_system_info`, `get_disk_usage`, `get_memory_usage`, `get_listening_ports` | Inspect the local host running `KelpieMCPServer`. |
| [Capabilities and inventory](#capabilities-and-inventory) | `ssh_get_capabilities`, `get_target_inventory` | Inspect target command/tool support and installed helper/software inventory. |
| [SSH diagnostics](#ssh-diagnostics) | `ssh_get_system_info`, `ssh_get_os_release`, `ssh_get_uptime`, `ssh_get_disk_usage`, `ssh_get_memory_usage`, `ssh_get_process_summary`, `ssh_get_inode_usage`, `ssh_get_mounts`, `ssh_get_network_addresses`, `ssh_get_routes`, `ssh_get_dns_config`, `ssh_check_http_local`, `ssh_check_tcp_connect_local`, `ssh_get_listening_ports`, `ssh_get_failed_services`, `ssh_get_journal_recent`, `ssh_tail_log` | Run allow-listed read-oriented diagnostics over SSH. |
| [Cron, certificate, user, firewall, backup, and audit checks](#cron-certificate-user-firewall-backup-and-audit-checks) | `ssh_cron_list`, `ssh_cron_validate`, `ssh_cron_check_write`, `ssh_cron_write`, `ssh_cron_rollback`, `ssh_cert_inspect`, `ssh_cert_expiry_check`, `ssh_user_list`, `ssh_user_info`, `ssh_group_list`, `ssh_group_info`, `ssh_sudoers_check`, `ssh_user_usage_check`, `ssh_user_check_group_change`, `ssh_user_apply_group_change`, `ssh_user_rollback_group_change`, `ssh_user_check_permission_change`, `ssh_user_apply_permission_change`, `ssh_user_rollback_permission_change`, `ssh_user_file_ownership_check`, `ssh_user_service_usage_check`, `ssh_service_residual_config_check`, `ssh_support_report_collect`, `ssh_firewall_status`, `ssh_firewall_check_rule`, `ssh_firewall_apply_rule`, `ssh_backup_plan_check`, `ssh_backup_run`, `ssh_backup_verify`, `ssh_audit_verify`, `ssh_audit_export` | Inspect or change sensitive server-maintenance state through bounded checks and confirmation-gated operations. |
| [Environment](#environment) | `get_environment_keys`, `peek_environment_value`, `set_environment_value`, `list_persistent_environment_keys`, `persist_environment_value`, `remove_persistent_environment_value` | List, read, temporarily set, or persist remote environment variables under profile policy. |
| [Generic execution](#generic-execution) | `ssh_run_allowed_command`, `ssh_run_remote_operation` | Run an allow-listed managed operation through policy checks. |
| [Terminal and session cleanup](#terminal-and-session-cleanup) | `ssh_terminal_open`, `ssh_terminal_send`, `ssh_terminal_snapshot`, `ssh_terminal_close`, `ssh_connection_close`, `ssh_logout` | Manage an interactive SSH terminal session and clear MCP password sessions. |
| [Packages](#packages) | `ssh_pkg_check_updates`, `ssh_pkg_info`, `ssh_pkg_search`, `ssh_pkg_list_installed`, `ssh_pkg_simulate_install`, `ssh_pkg_install`, `ssh_pkg_install_confirmed`, `ssh_pkg_simulate_remove`, `ssh_pkg_remove` | Inspect packages and run confirmation-gated package operations. |
| [Services](#services) | `ssh_service_status`, `ssh_service_is_active`, `ssh_service_is_enabled`, `ssh_list_services`, `ssh_service_enable_now`, `ssh_service_reload`, `ssh_service_restart`, `ssh_service_stop`, `ssh_service_disable` | Inspect and safely manage systemd services. |
| [Service config/logs](#service-configlogs) | `service_config_paths`, `service_config_file_check_read`, `service_config_file_read`, `service_config_file_check_write`, `service_config_file_write`, `service_config_file_rollback`, `service_config_file_commit`, `service_config_test`, `service_logfile_read` | Operate on provider-approved service configuration files and logs. |
| [Web files](#web-files) | `web_file_list`, `web_file_search_name`, `web_file_search_text`, `web_file_stat`, `web_file_check_write`, `web_file_check_permissions`, `web_file_read`, `web_file_head`, `web_file_tail`, `web_file_write`, `web_change_owner`, `web_change_owner_recursive`, `web_change_mode`, `web_change_mode_recursive` | Operate on provider-approved web roots. |

## Common Inputs

Most SSH target tools accept:

- `profileName`: Saved profile name under `KelpieHome/profiles`.
- Tool-specific arguments such as `service`, `path`, `lines`, `limit`, `packageName`, `siteKey`, or `confirmation`.

`ssh_run_remote_operation` accepts `operation` instead of `profileName`. The value is an `SshRemoteOperation` containing `endpoint`, `credential`, `policy`, `operation`, `options`, and optional `target` metadata.

Saved profiles are host-side persistence adapters. They are converted into `SshRemoteOperation` before execution. Product concepts such as profile count limits, edition limits, license state, ads, support, display order, notes, and customer data are not MCP tool inputs.

## Common Result Shapes

SSH command tools usually return `SshToolResult`:

- `ProfileName`: resolved SSH profile name.
- `Host`: target host from the resolved profile or operation.
- `Port`: target SSH port.
- `UserName`: target SSH user.
- `CommandName`: allowed Kelpie command name, such as `get_disk_usage`.
- `CommandText`: exact command text sent over SSH. Sensitive values are masked where the tool is designed to handle secrets.
- `ExitCode`: remote command exit code. `0` means the remote command completed successfully; non-zero values are command-specific failures from the remote tool or shell.
- `StandardOutput`: remote command stdout returned by the allowed Kelpie command.
- `StandardError`: remote command stderr returned by the allowed Kelpie command.
- `Stdout` / `Stderr`: structured or segmented stdout/stderr when the tool exposes AI-oriented segments.
- `StdoutPlain` / `StderrPlain`: plain-text stdout/stderr projection when segmented output is available.
- `StartedAt`: UTC command start timestamp.
- `CompletedAt`: UTC command completion timestamp.
- `TimedOut`: `true` when Kelpie stopped waiting because the command timeout elapsed.
- `Error`: Kelpie-side validation, policy, connection, or execution error message when the tool could not produce a normal SSH command result.

`SshToolResult` return value sample:

```json
{
  "ProfileName": "vps01",
  "Host": "example.invalid",
  "Port": 22,
  "UserName": "deploy",
  "CommandName": "get_disk_usage",
  "CommandText": "df -h",
  "ExitCode": 0,
  "StandardOutput": "Filesystem      Size  Used Avail Use% Mounted on\n/dev/sda1        40G   12G   26G  32% /\n",
  "StandardError": "",
  "StartedAt": "2026-06-17T12:00:00Z",
  "CompletedAt": "2026-06-17T12:00:01Z",
  "TimedOut": false,
  "Error": ""
}
```

Terminal snapshot tools return `SshTerminalSnapshotResult`:

- `Handle`: terminal session handle used by follow-up terminal tools.
- `ProfileName`: SSH profile name.
- `Columns` / `Rows`: rendered terminal size.
- `CursorRow` / `CursorColumn`: cursor location in the rendered screen buffer.
- `Lines`: rendered screen lines.
- `Text`: rendered screen as plain text.
- `RawOutput`: raw output read during the current operation.
- `Connected`: whether the terminal session is still connected.
- `StartedAtUtc`: UTC timestamp when the terminal session was opened.
- `CapturedAtUtc`: UTC timestamp when the snapshot was captured.

Terminal close tools return `SshTerminalCloseResult`:

- `Handle`: terminal session handle requested for close.
- `ProfileName`: profile name for the closed session, or an empty string when the handle was not found.
- `Closed`: `true` when a session was found and closed.
- `Error`: empty on success; `session-not-found` when the handle was not registered.

`ssh_logout` returns `SshLogoutResult`:

- `ProfileName`: SSH profile name.
- `LoggedOut`: `true` when a password session was removed.
- `Error`: empty on success; otherwise the reason logout could not be performed, such as a missing password secret name.

Tools that perform preflight checks usually return a result containing:

- the resolved target;
- whether the operation can proceed;
- a `confirmation` string if a later write/apply tool is required;
- `warnings` and `error` fields.

Tools that can change a remote target require an exact `confirmation` string. If the confirmation is missing or does not match, the tool returns a confirmation-required result and does not perform the change.

## Commands

MCP tools are grouped by operational area. Each group section states the scope, and each tool is documented in its own subsection.

### Server health

Verify that the MCP server is reachable.

Tools in this group:

- [`kelpie_ping`](#kelpie_ping)

#### `kelpie_ping`

Purpose:

Verifies that the KelpieSSH MCP server is running.

Input arguments:

- None.

`tools/call` params sample:

```json
{
  "name": "kelpie_ping",
  "arguments": {}
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `string`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```text
KelpieSSH MCP server is running.
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

### Profile management

Reload saved SSH profiles from disk on demand and inspect profile operation capabilities for an open SSH terminal connection.

Tools in this group:

- [`profile_reload`](#profile_reload)
- [`ssh_profile_capabilities`](#ssh_profile_capabilities)

#### `profile_reload`

Purpose:

Reloads SSH profile JSON files from the Kelpie profiles directory on demand.

Input arguments:

- None.

`tools/call` params sample:

```json
{
  "name": "profile_reload",
  "arguments": {}
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `ProfileReloadToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "Success": true,
  "ProfilesDirectory": "D:\\Kelpie\\profiles",
  "ProfileCount": 2,
  "ProfileNames": [
    "vps01",
    "vps02"
  ],
  "ErrorMessage": null
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool does not contact SSH targets. It updates only the MCP server's in-memory profile catalog, and existing terminal sessions keep their current connections.

#### `ssh_profile_capabilities`

Purpose:

Returns profile operation capabilities for an open SSH terminal connection.

Input arguments:

- `handle`: SSH terminal handle returned by `ssh_terminal_open`.

`tools/call` params sample:

```json
{
  "name": "ssh_profile_capabilities",
  "arguments": {
    "handle": "term-a1b2c3d4e5f6"
  }
}
```

Processing:

KelpieMCPServer resolves the terminal handle to the connected profile. It reads `ProfileOperations:Reload:MCP` from `kelpiemcp.json` and returns whether MCP-side profile reload capability is allowed for that connection. The tool does not contact the SSH target and does not read or print profile file contents.

Return value:

- Return type: `SshProfileCapabilitiesToolResult`.
- `Handle`: requested terminal handle.
- `ProfileName`: profile name associated with the terminal handle, or an empty string when the handle is not found.
- `ReloadAllowed`: `true` when `ProfileOperations:Reload:MCP` is `Allowed`; otherwise `false`. Legacy boolean `true` is also treated as allowed.
- `Reason`: stable reason string such as `allowed-by-config`, `disabled-by-config`, or `session-not-found`.

Return value sample:

```json
{
  "Handle": "term-a1b2c3d4e5f6",
  "ProfileName": "vps01",
  "ReloadAllowed": false,
  "Reason": "disabled-by-config"
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-only tool. It exposes only the terminal handle, profile name, and reload capability flag.

### Local diagnostics

Inspect the local host running KelpieMCPServer.

Tools in this group:

- [`get_system_info`](#get_system_info)
- [`get_disk_usage`](#get_disk_usage)
- [`get_memory_usage`](#get_memory_usage)
- [`get_listening_ports`](#get_listening_ports)

#### `get_system_info`

Purpose:

Returns basic OS, runtime, machine, and process information for the local KelpieMCPServer host.

Input arguments:

- None.

`tools/call` params sample:

```json
{
  "name": "get_system_info",
  "arguments": {}
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SystemInfoResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "MachineName": "<value>",
  "UserName": "deploy",
  "OSDescription": "<value>",
  "OSArchitecture": "<value>",
  "ProcessArchitecture": "<value>",
  "FrameworkDescription": "<value>",
  "ProcessorCount": 0,
  "Is64BitOperatingSystem": true,
  "Is64BitProcess": true,
  "ProcessId": 0,
  "BaseDirectory": "<value>"
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `get_disk_usage`

Purpose:

Returns disk usage for ready local drives on the KelpieMCPServer host.

Input arguments:

- None.

`tools/call` params sample:

```json
{
  "name": "get_disk_usage",
  "arguments": {}
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `DiskUsageResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "Drives": []
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `get_memory_usage`

Purpose:

Returns process and managed runtime memory usage for KelpieMCPServer.

Input arguments:

- None.

`tools/call` params sample:

```json
{
  "name": "get_memory_usage",
  "arguments": {}
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `MemoryUsageResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "WorkingSetBytes": 0,
  "PrivateMemoryBytes": 0,
  "VirtualMemoryBytes": 0,
  "ManagedTotalBytes": 0,
  "HeapSizeBytes": 0,
  "HighMemoryLoadThresholdBytes": 0,
  "MemoryLoadBytes": 0,
  "TotalAvailableMemoryBytes": 0
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `get_listening_ports`

Purpose:

Returns local listening TCP/UDP ports from the KelpieMCPServer host.

Input arguments:

- None.

`tools/call` params sample:

```json
{
  "name": "get_listening_ports",
  "arguments": {}
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `ListeningPortsResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "Command": "<value>",
  "Arguments": "<value>",
  "ExitCode": 0,
  "StandardError": "",
  "Ports": []
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

### Capabilities and inventory

Inspect target command/tool support and installed helper/software inventory.

Tools in this group:

- [`ssh_get_capabilities`](#ssh_get_capabilities)
- [`get_target_inventory`](#get_target_inventory)

#### `ssh_get_capabilities`

Purpose:

Checks profile-specific SSH command and MCP tool capabilities.

Input arguments:

- `profileName`: SSH profile name.

`tools/call` params sample:

```json
{
  "name": "ssh_get_capabilities",
  "arguments": {
    "profileName": "vps01"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshCapabilityResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "ProfileName": "vps01",
  "OsFamily": "<value>",
  "PackageManager": "<value>",
  "Mode": "<value>",
  "ProbeSucceeded": true,
  "ProbeCommandName": "<command-name>",
  "ProbeCommandText": "<command text>",
  "ProbeExitCode": 0,
  "Commands": [],
  "Tools": []
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `get_target_inventory`

Purpose:

Returns read-only OS, helper, and software inventory for a configured SSH profile.

Input arguments:

- `profileName`: SSH profile name.

`tools/call` params sample:

```json
{
  "name": "get_target_inventory",
  "arguments": {
    "profileName": "vps01"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `TargetInventoryResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "Profile": "vps01",
  "Os": "<value>",
  "Helpers": [],
  "Software": []
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

### SSH diagnostics

Run allow-listed read-oriented diagnostics over SSH.

Tools in this group:

- [`ssh_get_system_info`](#ssh_get_system_info)
- [`ssh_get_os_release`](#ssh_get_os_release)
- [`ssh_get_uptime`](#ssh_get_uptime)
- [`ssh_get_disk_usage`](#ssh_get_disk_usage)
- [`ssh_get_memory_usage`](#ssh_get_memory_usage)
- [`ssh_get_process_summary`](#ssh_get_process_summary)
- [`ssh_get_inode_usage`](#ssh_get_inode_usage)
- [`ssh_get_mounts`](#ssh_get_mounts)
- [`ssh_get_network_addresses`](#ssh_get_network_addresses)
- [`ssh_get_routes`](#ssh_get_routes)
- [`ssh_get_dns_config`](#ssh_get_dns_config)
- [`ssh_check_http_local`](#ssh_check_http_local)
- [`ssh_check_tcp_connect_local`](#ssh_check_tcp_connect_local)
- [`ssh_get_listening_ports`](#ssh_get_listening_ports)
- [`ssh_get_failed_services`](#ssh_get_failed_services)
- [`ssh_get_journal_recent`](#ssh_get_journal_recent)
- [`ssh_tail_log`](#ssh_tail_log)

#### `ssh_get_system_info`

Purpose:

Runs the allowed get_system_info command against a configured SSH profile.

Input arguments:

- `profileName`: SSH profile name.

`tools/call` params sample:

```json
{
  "name": "ssh_get_system_info",
  "arguments": {
    "profileName": "vps01"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_get_system_info",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_get_os_release`

Purpose:

Runs the allowed get_os_release command against a configured SSH profile.

Input arguments:

- `profileName`: SSH profile name.

`tools/call` params sample:

```json
{
  "name": "ssh_get_os_release",
  "arguments": {
    "profileName": "vps01"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_get_os_release",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_get_uptime`

Purpose:

Runs the allowed get_uptime command against a configured SSH profile.

Input arguments:

- `profileName`: SSH profile name.

`tools/call` params sample:

```json
{
  "name": "ssh_get_uptime",
  "arguments": {
    "profileName": "vps01"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_get_uptime",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_get_disk_usage`

Purpose:

Runs the allowed get_disk_usage command against a configured SSH profile.

Input arguments:

- `profileName`: SSH profile name.

`tools/call` params sample:

```json
{
  "name": "ssh_get_disk_usage",
  "arguments": {
    "profileName": "vps01"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_get_disk_usage",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_get_memory_usage`

Purpose:

Runs the allowed get_memory_usage command against a configured SSH profile.

Input arguments:

- `profileName`: SSH profile name.

`tools/call` params sample:

```json
{
  "name": "ssh_get_memory_usage",
  "arguments": {
    "profileName": "vps01"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_get_memory_usage",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_get_process_summary`

Purpose:

Runs the allowed get_process_summary command against a configured SSH profile.

Input arguments:

- `profileName`: SSH profile name.
- `sortBy`: Tool-specific argument of type `string` defined by the MCP schema.
- `limit`: Maximum number of rows to return.

`tools/call` params sample:

```json
{
  "name": "ssh_get_process_summary",
  "arguments": {
    "profileName": "vps01",
    "sortBy": "<value>",
    "limit": 40
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_get_process_summary",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_get_inode_usage`

Purpose:

Runs the allowed get_inode_usage command against a configured SSH profile.

Input arguments:

- `profileName`: SSH profile name.

`tools/call` params sample:

```json
{
  "name": "ssh_get_inode_usage",
  "arguments": {
    "profileName": "vps01"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_get_inode_usage",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_get_mounts`

Purpose:

Runs the allowed get_mounts command against a configured SSH profile.

Input arguments:

- `profileName`: SSH profile name.

`tools/call` params sample:

```json
{
  "name": "ssh_get_mounts",
  "arguments": {
    "profileName": "vps01"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_get_mounts",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_get_network_addresses`

Purpose:

Runs the allowed get_network_addresses command against a configured SSH profile.

Input arguments:

- `profileName`: SSH profile name.

`tools/call` params sample:

```json
{
  "name": "ssh_get_network_addresses",
  "arguments": {
    "profileName": "vps01"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_get_network_addresses",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_get_routes`

Purpose:

Runs the allowed get_routes command against a configured SSH profile.

Input arguments:

- `profileName`: SSH profile name.

`tools/call` params sample:

```json
{
  "name": "ssh_get_routes",
  "arguments": {
    "profileName": "vps01"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_get_routes",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_get_dns_config`

Purpose:

Runs the allowed get_dns_config command against a configured SSH profile.

Input arguments:

- `profileName`: SSH profile name.

`tools/call` params sample:

```json
{
  "name": "ssh_get_dns_config",
  "arguments": {
    "profileName": "vps01"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_get_dns_config",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_check_http_local`

Purpose:

Checks an HTTP response from 127.0.0.1 on a configured SSH profile with a validated port.

Input arguments:

- `profileName`: SSH profile name.
- `port`: TCP or UDP port number.

`tools/call` params sample:

```json
{
  "name": "ssh_check_http_local",
  "arguments": {
    "profileName": "vps01",
    "port": 443
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_check_http_local",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_check_tcp_connect_local`

Purpose:

Checks TCP connectivity to 127.0.0.1 on a configured SSH profile with a validated port.

Input arguments:

- `profileName`: SSH profile name.
- `port`: TCP or UDP port number.

`tools/call` params sample:

```json
{
  "name": "ssh_check_tcp_connect_local",
  "arguments": {
    "profileName": "vps01",
    "port": 443
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_check_tcp_connect_local",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_get_listening_ports`

Purpose:

Runs the allowed get_listening_ports command against a configured SSH profile.

Input arguments:

- `profileName`: SSH profile name.

`tools/call` params sample:

```json
{
  "name": "ssh_get_listening_ports",
  "arguments": {
    "profileName": "vps01"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_get_listening_ports",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_get_failed_services`

Purpose:

Runs the allowed get_failed_services command against a configured SSH profile.

Input arguments:

- `profileName`: SSH profile name.

`tools/call` params sample:

```json
{
  "name": "ssh_get_failed_services",
  "arguments": {
    "profileName": "vps01"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_get_failed_services",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_get_journal_recent`

Purpose:

Runs the allowed get_journal_recent command against a configured SSH profile.

Input arguments:

- `profileName`: SSH profile name.
- `lines`: Maximum number of log or terminal lines to return.

`tools/call` params sample:

```json
{
  "name": "ssh_get_journal_recent",
  "arguments": {
    "profileName": "vps01",
    "lines": 120
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_get_journal_recent",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_tail_log`

Purpose:

Runs the allowed tail_log command against a configured SSH profile for one systemd service.

Input arguments:

- `service`: systemd service name.
- `profileName`: SSH profile name.
- `lines`: Maximum number of log or terminal lines to return.

`tools/call` params sample:

```json
{
  "name": "ssh_tail_log",
  "arguments": {
    "service": "nginx.service",
    "profileName": "vps01",
    "lines": 120
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_tail_log",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

### Cron, certificate, user, firewall, backup, and audit checks

Inspect or change sensitive server-maintenance state through bounded checks and confirmation-gated operations.

Tools in this group:

- [`ssh_cron_list`](#ssh_cron_list)
- [`ssh_cron_validate`](#ssh_cron_validate)
- [`ssh_cron_check_write`](#ssh_cron_check_write)
- [`ssh_cron_write`](#ssh_cron_write)
- [`ssh_cron_rollback`](#ssh_cron_rollback)
- [`ssh_cert_inspect`](#ssh_cert_inspect)
- [`ssh_cert_expiry_check`](#ssh_cert_expiry_check)
- [`ssh_user_list`](#ssh_user_list)
- [`ssh_user_info`](#ssh_user_info)
- [`ssh_group_list`](#ssh_group_list)
- [`ssh_group_info`](#ssh_group_info)
- [`ssh_sudoers_check`](#ssh_sudoers_check)
- [`ssh_user_usage_check`](#ssh_user_usage_check)
- [`ssh_user_check_group_change`](#ssh_user_check_group_change)
- [`ssh_user_apply_group_change`](#ssh_user_apply_group_change)
- [`ssh_user_rollback_group_change`](#ssh_user_rollback_group_change)
- [`ssh_user_check_permission_change`](#ssh_user_check_permission_change)
- [`ssh_user_apply_permission_change`](#ssh_user_apply_permission_change)
- [`ssh_user_rollback_permission_change`](#ssh_user_rollback_permission_change)
- [`ssh_user_file_ownership_check`](#ssh_user_file_ownership_check)
- [`ssh_user_service_usage_check`](#ssh_user_service_usage_check)
- [`ssh_service_residual_config_check`](#ssh_service_residual_config_check)
- [`ssh_support_report_collect`](#ssh_support_report_collect)
- [`ssh_firewall_status`](#ssh_firewall_status)
- [`ssh_firewall_check_rule`](#ssh_firewall_check_rule)
- [`ssh_firewall_apply_rule`](#ssh_firewall_apply_rule)
- [`ssh_backup_plan_check`](#ssh_backup_plan_check)
- [`ssh_backup_run`](#ssh_backup_run)
- [`ssh_backup_verify`](#ssh_backup_verify)
- [`ssh_audit_verify`](#ssh_audit_verify)
- [`ssh_audit_export`](#ssh_audit_export)

#### `ssh_cron_list`

Purpose:

Lists system and current-user cron entries with a bounded result limit.

Input arguments:

- `profileName`: SSH profile name.
- `limit`: Maximum number of rows to return.

`tools/call` params sample:

```json
{
  "name": "ssh_cron_list",
  "arguments": {
    "profileName": "vps01",
    "limit": 40
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_cron_list",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_cron_validate`

Purpose:

Validates a cron expression, run user, command text, and /var/log path without changing cron files.

Input arguments:

- `profileName`: SSH profile name.
- `cronExpression`: Cron expression.
- `runUser`: User account used to run a scheduled task or command.
- `command`: Managed command text or command name accepted by policy.
- `logPath`: Tool-specific argument of type `string` defined by the MCP schema.

`tools/call` params sample:

```json
{
  "name": "ssh_cron_validate",
  "arguments": {
    "profileName": "vps01",
    "cronExpression": "*/15 * * * *",
    "runUser": "deploy",
    "command": "uptime",
    "logPath": "<value>"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_cron_validate",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_cron_check_write`

Purpose:

Checks cron write inputs, target, confirmation token, and rollback support without changing cron files.

Input arguments:

- `profileName`: SSH profile name.
- `targetType`: Target category used by the operation.
- `runUser`: User account used to run a scheduled task or command.
- `cronExpression`: Cron expression.
- `command`: Managed command text or command name accepted by policy.
- `logPath`: Tool-specific argument of type `string` defined by the MCP schema.

`tools/call` params sample:

```json
{
  "name": "ssh_cron_check_write",
  "arguments": {
    "profileName": "vps01",
    "targetType": "service",
    "runUser": "deploy",
    "cronExpression": "*/15 * * * *",
    "command": "uptime",
    "logPath": "<value>"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_cron_check_write",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `ssh_cron_write`

Purpose:

Writes one managed cron entry after explicit confirmation and creates a rollback backup.

Input arguments:

- `profileName`: SSH profile name.
- `targetType`: Target category used by the operation.
- `runUser`: User account used to run a scheduled task or command.
- `cronExpression`: Cron expression.
- `command`: Managed command text or command name accepted by policy.
- `logPath`: Tool-specific argument of type `string` defined by the MCP schema.
- `confirmation`: Exact confirmation token returned by a check or simulate tool.

`tools/call` params sample:

```json
{
  "name": "ssh_cron_write",
  "arguments": {
    "profileName": "vps01",
    "targetType": "service",
    "runUser": "deploy",
    "cronExpression": "*/15 * * * *",
    "command": "uptime",
    "logPath": "<value>",
    "confirmation": "<confirmation-token-from-check-result>"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_cron_write",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `ssh_cron_rollback`

Purpose:

Restores the latest Kelpie-managed cron backup after explicit confirmation.

Input arguments:

- `profileName`: SSH profile name.
- `targetType`: Target category used by the operation.
- `runUser`: User account used to run a scheduled task or command.
- `confirmation`: Exact confirmation token returned by a check or simulate tool.

`tools/call` params sample:

```json
{
  "name": "ssh_cron_rollback",
  "arguments": {
    "profileName": "vps01",
    "targetType": "service",
    "runUser": "deploy",
    "confirmation": "<confirmation-token-from-check-result>"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_cron_rollback",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `ssh_cert_inspect`

Purpose:

Inspects issuer, subject, dates, and SAN for a certificate under approved certificate directories.

Input arguments:

- `profileName`: SSH profile name.
- `path`: Target path validated by the tool policy or provider.

`tools/call` params sample:

```json
{
  "name": "ssh_cert_inspect",
  "arguments": {
    "profileName": "vps01",
    "path": "/var/www/example/index.html"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_cert_inspect",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_cert_expiry_check`

Purpose:

Checks whether a certificate under approved certificate directories is valid for the requested number of days.

Input arguments:

- `profileName`: SSH profile name.
- `path`: Target path validated by the tool policy or provider.
- `days`: Tool-specific argument of type `string` defined by the MCP schema.

`tools/call` params sample:

```json
{
  "name": "ssh_cert_expiry_check",
  "arguments": {
    "profileName": "vps01",
    "path": "/var/www/example/index.html",
    "days": "<value>"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_cert_expiry_check",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_user_list`

Purpose:

Lists local users with UID, GID, home directory, and shell using a bounded result limit.

Input arguments:

- `profileName`: SSH profile name.
- `limit`: Maximum number of rows to return.

`tools/call` params sample:

```json
{
  "name": "ssh_user_list",
  "arguments": {
    "profileName": "vps01",
    "limit": 40
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_user_list",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_user_info`

Purpose:

Returns UID, GID, primary group, supplementary groups, home, and shell for one local user.

Input arguments:

- `profileName`: SSH profile name.
- `user`: User account name.

`tools/call` params sample:

```json
{
  "name": "ssh_user_info",
  "arguments": {
    "profileName": "vps01",
    "user": "deploy"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_user_info",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_group_list`

Purpose:

Lists local groups with GID and member names using a bounded result limit.

Input arguments:

- `profileName`: SSH profile name.
- `limit`: Maximum number of rows to return.

`tools/call` params sample:

```json
{
  "name": "ssh_group_list",
  "arguments": {
    "profileName": "vps01",
    "limit": 40
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_group_list",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_group_info`

Purpose:

Returns GID and member names for one local group.

Input arguments:

- `profileName`: SSH profile name.
- `group`: Group name.

`tools/call` params sample:

```json
{
  "name": "ssh_group_info",
  "arguments": {
    "profileName": "vps01",
    "group": "www-data"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_group_info",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_sudoers_check`

Purpose:

Summarizes whether one user or group has sudoers evidence without returning sudoers file content.

Input arguments:

- `profileName`: SSH profile name.
- `targetType`: Target category used by the operation.
- `name`: Target name.

`tools/call` params sample:

```json
{
  "name": "ssh_sudoers_check",
  "arguments": {
    "profileName": "vps01",
    "targetType": "service",
    "name": "nginx"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_sudoers_check",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_user_usage_check`

Purpose:

Checks whether one user or group is referenced by services, cron owners, or common owned paths with bounded output.

Input arguments:

- `profileName`: SSH profile name.
- `targetType`: Target category used by the operation.
- `name`: Target name.
- `limit`: Maximum number of rows to return.

`tools/call` params sample:

```json
{
  "name": "ssh_user_usage_check",
  "arguments": {
    "profileName": "vps01",
    "targetType": "service",
    "name": "nginx",
    "limit": 40
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_user_usage_check",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_user_check_group_change`

Purpose:

Checks a user supplementary group change and returns the diff and confirmation token without applying it.

Input arguments:

- `profileName`: SSH profile name.
- `user`: User account name.
- `groups`: Comma-separated or tool-specific group list.
- `mode`: Three-digit octal mode for permission changes.

`tools/call` params sample:

```json
{
  "name": "ssh_user_check_group_change",
  "arguments": {
    "profileName": "vps01",
    "user": "deploy",
    "groups": "www-data",
    "mode": "755"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_user_check_group_change",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `ssh_user_apply_group_change`

Purpose:

Applies a user supplementary group change after explicit confirmation.

Input arguments:

- `profileName`: SSH profile name.
- `user`: User account name.
- `groups`: Comma-separated or tool-specific group list.
- `mode`: Three-digit octal mode for permission changes.
- `confirmation`: Exact confirmation token returned by a check or simulate tool.

`tools/call` params sample:

```json
{
  "name": "ssh_user_apply_group_change",
  "arguments": {
    "profileName": "vps01",
    "user": "deploy",
    "groups": "www-data",
    "mode": "755",
    "confirmation": "<confirmation-token-from-check-result>"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_user_apply_group_change",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `ssh_user_rollback_group_change`

Purpose:

Restores the latest user supplementary group backup after explicit confirmation.

Input arguments:

- `profileName`: SSH profile name.
- `user`: User account name.
- `confirmation`: Exact confirmation token returned by a check or simulate tool.

`tools/call` params sample:

```json
{
  "name": "ssh_user_rollback_group_change",
  "arguments": {
    "profileName": "vps01",
    "user": "deploy",
    "confirmation": "<confirmation-token-from-check-result>"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_user_rollback_group_change",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `ssh_user_check_permission_change`

Purpose:

Checks a user shell, login, or sudo permission change without applying it.

Input arguments:

- `profileName`: SSH profile name.
- `user`: User account name.
- `shell`: Requested login shell.
- `login`: Requested login state: enabled, disabled, or unchanged.
- `sudo`: Requested sudo state.

`tools/call` params sample:

```json
{
  "name": "ssh_user_check_permission_change",
  "arguments": {
    "profileName": "vps01",
    "user": "deploy",
    "shell": "/usr/sbin/nologin",
    "login": "unchanged",
    "sudo": "unchanged"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_user_check_permission_change",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `ssh_user_apply_permission_change`

Purpose:

Applies a user shell, login, or sudo permission change after explicit confirmation.

Input arguments:

- `profileName`: SSH profile name.
- `user`: User account name.
- `shell`: Requested login shell.
- `login`: Requested login state: enabled, disabled, or unchanged.
- `sudo`: Requested sudo state.
- `confirmation`: Exact confirmation token returned by a check or simulate tool.

`tools/call` params sample:

```json
{
  "name": "ssh_user_apply_permission_change",
  "arguments": {
    "profileName": "vps01",
    "user": "deploy",
    "shell": "/usr/sbin/nologin",
    "login": "unchanged",
    "sudo": "unchanged",
    "confirmation": "<confirmation-token-from-check-result>"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_user_apply_permission_change",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `ssh_user_rollback_permission_change`

Purpose:

Restores the latest user shell, login, and managed sudo permission backup after explicit confirmation.

Input arguments:

- `profileName`: SSH profile name.
- `user`: User account name.
- `confirmation`: Exact confirmation token returned by a check or simulate tool.

`tools/call` params sample:

```json
{
  "name": "ssh_user_rollback_permission_change",
  "arguments": {
    "profileName": "vps01",
    "user": "deploy",
    "confirmation": "<confirmation-token-from-check-result>"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_user_rollback_permission_change",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `ssh_user_file_ownership_check`

Purpose:

Checks whether one user or group owns files under an approved root using a bounded non-following scan.

Input arguments:

- `profileName`: SSH profile name.
- `targetType`: Target category used by the operation.
- `name`: Target name.
- `scanRoot`: Root path for a bounded scan.
- `depth`: Maximum scan depth.
- `limit`: Maximum number of rows to return.

`tools/call` params sample:

```json
{
  "name": "ssh_user_file_ownership_check",
  "arguments": {
    "profileName": "vps01",
    "targetType": "service",
    "name": "nginx",
    "scanRoot": "/var/www",
    "depth": 3,
    "limit": 40
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_user_file_ownership_check",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_user_service_usage_check`

Purpose:

Checks systemd User, Group, and SupplementaryGroups references for one user or group.

Input arguments:

- `profileName`: SSH profile name.
- `targetType`: Target category used by the operation.
- `name`: Target name.
- `limit`: Maximum number of rows to return.

`tools/call` params sample:

```json
{
  "name": "ssh_user_service_usage_check",
  "arguments": {
    "profileName": "vps01",
    "targetType": "service",
    "name": "nginx",
    "limit": 40
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_user_service_usage_check",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_service_residual_config_check`

Purpose:

Checks common service unit, config, log, data, and runtime residual paths without reading file contents.

Input arguments:

- `profileName`: SSH profile name.
- `service`: systemd service name.
- `limit`: Maximum number of rows to return.

`tools/call` params sample:

```json
{
  "name": "ssh_service_residual_config_check",
  "arguments": {
    "profileName": "vps01",
    "service": "nginx.service",
    "limit": 40
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_service_residual_config_check",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_support_report_collect`

Purpose:

Collects a sanitized read-only support report without host names, IP addresses, usernames, or file contents.

Input arguments:

- `profileName`: SSH profile name.
- `limit`: Maximum number of rows to return.

`tools/call` params sample:

```json
{
  "name": "ssh_support_report_collect",
  "arguments": {
    "profileName": "vps01",
    "limit": 40
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_support_report_collect",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_firewall_status`

Purpose:

Checks firewalld and ufw availability and status without returning rule bodies.

Input arguments:

- `profileName`: SSH profile name.

`tools/call` params sample:

```json
{
  "name": "ssh_firewall_status",
  "arguments": {
    "profileName": "vps01"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_firewall_status",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_firewall_check_rule`

Purpose:

Checks one firewalld rule change and returns state plus confirmation token without applying it.

Input arguments:

- `profileName`: SSH profile name.
- `action`: Requested action.
- `target`: Operation target.
- `value`: Environment variable value.
- `zone`: Firewall zone.
- `permanent`: Whether the firewall rule should be permanent.

`tools/call` params sample:

```json
{
  "name": "ssh_firewall_check_rule",
  "arguments": {
    "profileName": "vps01",
    "action": "allow",
    "target": "https://127.0.0.1/",
    "value": "production",
    "zone": "public",
    "permanent": false
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_firewall_check_rule",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_firewall_apply_rule`

Purpose:

Applies one firewalld rule change after explicit confirmation.

Input arguments:

- `profileName`: SSH profile name.
- `action`: Requested action.
- `target`: Operation target.
- `value`: Environment variable value.
- `zone`: Firewall zone.
- `permanent`: Whether the firewall rule should be permanent.
- `confirmation`: Exact confirmation token returned by a check or simulate tool.

`tools/call` params sample:

```json
{
  "name": "ssh_firewall_apply_rule",
  "arguments": {
    "profileName": "vps01",
    "action": "allow",
    "target": "https://127.0.0.1/",
    "value": "production",
    "zone": "public",
    "permanent": false,
    "confirmation": "<confirmation-token-from-check-result>"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_firewall_apply_rule",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `ssh_backup_plan_check`

Purpose:

Checks backup scope, estimated file counts, and confirmation token without creating a backup.

Input arguments:

- `profileName`: SSH profile name.
- `scanRoot`: Root path for a bounded scan.
- `depth`: Maximum scan depth.
- `limit`: Maximum number of rows to return.

`tools/call` params sample:

```json
{
  "name": "ssh_backup_plan_check",
  "arguments": {
    "profileName": "vps01",
    "scanRoot": "/var/www",
    "depth": 3,
    "limit": 40
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_backup_plan_check",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_backup_run`

Purpose:

Creates a bounded provider-approved backup archive after explicit confirmation.

Input arguments:

- `profileName`: SSH profile name.
- `scanRoot`: Root path for a bounded scan.
- `depth`: Maximum scan depth.
- `limit`: Maximum number of rows to return.
- `confirmation`: Exact confirmation token returned by a check or simulate tool.

`tools/call` params sample:

```json
{
  "name": "ssh_backup_run",
  "arguments": {
    "profileName": "vps01",
    "scanRoot": "/var/www",
    "depth": 3,
    "limit": 40,
    "confirmation": "<confirmation-token-from-check-result>"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_backup_run",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `ssh_backup_verify`

Purpose:

Verifies whether an approved backup archive exists and can be listed without returning archive entries.

Input arguments:

- `profileName`: SSH profile name.
- `backupPath`: Backup path returned by a previous write operation.

`tools/call` params sample:

```json
{
  "name": "ssh_backup_verify",
  "arguments": {
    "profileName": "vps01",
    "backupPath": "/var/backups/kelpie/example.bak"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_backup_verify",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_audit_verify`

Purpose:

Verifies a Kelpie audit log hash chain without returning log bodies.

Input arguments:

- `profileName`: SSH profile name.
- `logPath`: Tool-specific argument of type `string` defined by the MCP schema.
- `limit`: Maximum number of rows to return.

`tools/call` params sample:

```json
{
  "name": "ssh_audit_verify",
  "arguments": {
    "profileName": "vps01",
    "logPath": "<value>",
    "limit": 40
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_audit_verify",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_audit_export`

Purpose:

Exports a sanitized Kelpie audit log summary without raw log bodies.

Input arguments:

- `profileName`: SSH profile name.
- `logPath`: Tool-specific argument of type `string` defined by the MCP schema.
- `limit`: Maximum number of rows to return.

`tools/call` params sample:

```json
{
  "name": "ssh_audit_export",
  "arguments": {
    "profileName": "vps01",
    "logPath": "<value>",
    "limit": 40
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_audit_export",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

### Environment

List, read, temporarily set, or persist remote environment variables under profile policy.

Tools in this group:

- [`get_environment_keys`](#get_environment_keys)
- [`peek_environment_value`](#peek_environment_value)
- [`set_environment_value`](#set_environment_value)
- [`list_persistent_environment_keys`](#list_persistent_environment_keys)
- [`persist_environment_value`](#persist_environment_value)
- [`remove_persistent_environment_value`](#remove_persistent_environment_value)

#### `get_environment_keys`

Purpose:

Lists remote environment variable keys for a configured SSH profile when profile policy allows it.

Input arguments:

- `profileName`: SSH profile name.

`tools/call` params sample:

```json
{
  "name": "get_environment_keys",
  "arguments": {
    "profileName": "vps01"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "get_environment_keys",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Do not record secrets or raw sensitive values in public documents or logs.

#### `peek_environment_value`

Purpose:

Reads one remote environment variable value only when profile policy allows the key.

Input arguments:

- `profileName`: SSH profile name.
- `key`: Environment variable key.

`tools/call` params sample:

```json
{
  "name": "peek_environment_value",
  "arguments": {
    "profileName": "vps01",
    "key": "APP_ENV"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "peek_environment_value",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Do not record secrets or raw sensitive values in public documents or logs.

#### `set_environment_value`

Purpose:

Runs one command with one remote environment variable set for that execution only. The value is not persisted.

Input arguments:

- `profileName`: SSH profile name.
- `key`: Environment variable key.
- `value`: Environment variable value.
- `command`: Managed command text or command name accepted by policy.

`tools/call` params sample:

```json
{
  "name": "set_environment_value",
  "arguments": {
    "profileName": "vps01",
    "key": "APP_ENV",
    "value": "production",
    "command": "uptime"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "set_environment_value",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Do not record secrets or raw sensitive values in public documents or logs.

#### `list_persistent_environment_keys`

Purpose:

Lists environment variable keys persisted in ~/.kelpie/.env when profile policy allows key listing.

Input arguments:

- `profileName`: SSH profile name.

`tools/call` params sample:

```json
{
  "name": "list_persistent_environment_keys",
  "arguments": {
    "profileName": "vps01"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "list_persistent_environment_keys",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Do not record secrets or raw sensitive values in public documents or logs.

#### `persist_environment_value`

Purpose:

Persists one remote environment variable value in ~/.kelpie/.env when profile policy allows setting the key.

Input arguments:

- `profileName`: SSH profile name.
- `key`: Environment variable key.
- `value`: Environment variable value.

`tools/call` params sample:

```json
{
  "name": "persist_environment_value",
  "arguments": {
    "profileName": "vps01",
    "key": "APP_ENV",
    "value": "production"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "persist_environment_value",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Do not record secrets or raw sensitive values in public documents or logs.

#### `remove_persistent_environment_value`

Purpose:

Removes one remote environment variable value from ~/.kelpie/.env when profile policy allows setting the key.

Input arguments:

- `profileName`: SSH profile name.
- `key`: Environment variable key.

`tools/call` params sample:

```json
{
  "name": "remove_persistent_environment_value",
  "arguments": {
    "profileName": "vps01",
    "key": "APP_ENV"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "remove_persistent_environment_value",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

### Generic execution

Run an allow-listed managed operation through policy checks.

Tools in this group:

- [`ssh_run_allowed_command`](#ssh_run_allowed_command)
- [`ssh_run_remote_operation`](#ssh_run_remote_operation)

#### `ssh_run_allowed_command`

Purpose:

Runs one allowed read-only diagnostic command against a configured SSH profile.

Input arguments:

- `commandName`: Allow-listed command name.
- `profileName`: SSH profile name.
- `arguments`: Key-value command arguments.

`tools/call` params sample:

```json
{
  "name": "ssh_run_allowed_command",
  "arguments": {
    "commandName": "get_system_info",
    "profileName": "vps01",
    "arguments": {}
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_run_allowed_command",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `ssh_run_remote_operation`

Purpose:

Runs one SSH remote operation from endpoint, credential, policy, operation, and options inputs.

Input arguments:

- `operation`: SshRemoteOperation object containing endpoint, credential, policy, operation, and options.

`tools/call` params sample:

```json
{
  "name": "ssh_run_remote_operation",
  "arguments": {
    "operation": {
      "operation": {
        "commandName": "get_system_info",
        "arguments": {}
      },
      "credential": {
        "userName": "deploy",
        "method": "privateKey"
      },
      "endpoint": {
        "port": 22,
        "host": "example.invalid"
      }
    }
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshRemoteOperationToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "get_system_info",
  "StandardError": "",
  "StandardOutput": "<remote command stdout>",
  "Error": "",
  "ExitCode": 0
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

### Terminal and session cleanup

Manage an interactive SSH terminal session and clear MCP password sessions.

Tools in this group:

- [`ssh_terminal_open`](#ssh_terminal_open)
- [`ssh_terminal_send`](#ssh_terminal_send)
- [`ssh_terminal_snapshot`](#ssh_terminal_snapshot)
- [`ssh_terminal_close`](#ssh_terminal_close)
- [`ssh_connection_close`](#ssh_connection_close)
- [`ssh_logout`](#ssh_logout)

#### `ssh_terminal_open`

Purpose:

Opens an interactive SSH terminal session and returns the initial rendered screen snapshot.

Input arguments:

- `profileName`: SSH profile name.
- `columns`: Terminal width in columns.
- `rows`: Terminal height in rows.
- `pixelWidth`: Terminal render width in pixels.
- `pixelHeight`: Terminal render height in pixels.

`tools/call` params sample:

```json
{
  "name": "ssh_terminal_open",
  "arguments": {
    "profileName": "vps01",
    "columns": 120,
    "rows": 40,
    "pixelWidth": 960,
    "pixelHeight": 640
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshTerminalSnapshotResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "ProfileName": "vps01",
  "Rows": 40,
  "Handle": "term-a1b2c3d4e5f6",
  "Text": "<terminal screen text>",
  "Error": "",
  "Connected": true,
  "Columns": 120,
  "Lines": [
    "<terminal screen line>"
  ]
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Persistent terminal sessions can carry remote state; close handles when they are no longer needed.

#### `ssh_terminal_send`

Purpose:

Sends raw input to an interactive SSH terminal session and returns the updated rendered screen snapshot.

Input arguments:

- `handle`: Terminal connection handle returned by ssh_terminal_open.
- `input`: Text to send to the terminal session.

`tools/call` params sample:

```json
{
  "name": "ssh_terminal_send",
  "arguments": {
    "handle": "term-a1b2c3d4e5f6",
    "input": "exit"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshTerminalSnapshotResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "ProfileName": "vps01",
  "Rows": 40,
  "Handle": "term-a1b2c3d4e5f6",
  "Text": "<terminal screen text>",
  "Error": "",
  "Connected": true,
  "Columns": 120,
  "Lines": [
    "<terminal screen line>"
  ]
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Persistent terminal sessions can carry remote state; close handles when they are no longer needed.

#### `ssh_terminal_snapshot`

Purpose:

Returns the current rendered screen snapshot for an interactive SSH terminal session.

Input arguments:

- `handle`: Terminal connection handle returned by ssh_terminal_open.

`tools/call` params sample:

```json
{
  "name": "ssh_terminal_snapshot",
  "arguments": {
    "handle": "term-a1b2c3d4e5f6"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshTerminalSnapshotResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "ProfileName": "vps01",
  "Rows": 40,
  "Handle": "term-a1b2c3d4e5f6",
  "Text": "<terminal screen text>",
  "Error": "",
  "Connected": true,
  "Columns": 120,
  "Lines": [
    "<terminal screen line>"
  ]
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Persistent terminal sessions can carry remote state; close handles when they are no longer needed.

#### `ssh_terminal_close`

Purpose:

Closes an interactive SSH terminal session.

Input arguments:

- `handle`: Terminal connection handle returned by ssh_terminal_open.

`tools/call` params sample:

```json
{
  "name": "ssh_terminal_close",
  "arguments": {
    "handle": "term-a1b2c3d4e5f6"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshTerminalCloseResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "Closed": true,
  "Handle": "term-a1b2c3d4e5f6",
  "ProfileName": "vps01",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Persistent terminal sessions can carry remote state; close handles when they are no longer needed.

#### `ssh_connection_close`

Purpose:

Closes a persistent SSH terminal connection opened by ssh_terminal_open.

Input arguments:

- `handle`: Terminal connection handle returned by ssh_terminal_open.

`tools/call` params sample:

```json
{
  "name": "ssh_connection_close",
  "arguments": {
    "handle": "term-a1b2c3d4e5f6"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshTerminalCloseResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "Closed": true,
  "Handle": "term-a1b2c3d4e5f6",
  "ProfileName": "vps01",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Persistent terminal sessions can carry remote state; close handles when they are no longer needed.

#### `ssh_logout`

Purpose:

Clears the in-memory SSH password session for a configured SSH profile.

Input arguments:

- `profileName`: SSH profile name.

`tools/call` params sample:

```json
{
  "name": "ssh_logout",
  "arguments": {
    "profileName": "vps01"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshLogoutResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "ProfileName": "vps01",
  "LoggedOut": true,
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Clears only the in-memory password session for the specified profile; it does not close existing terminal connections.

### Packages

Inspect packages and run confirmation-gated package operations.

Tools in this group:

- [`ssh_pkg_check_updates`](#ssh_pkg_check_updates)
- [`ssh_pkg_info`](#ssh_pkg_info)
- [`ssh_pkg_search`](#ssh_pkg_search)
- [`ssh_pkg_list_installed`](#ssh_pkg_list_installed)
- [`ssh_pkg_simulate_install`](#ssh_pkg_simulate_install)
- [`ssh_pkg_install`](#ssh_pkg_install)
- [`ssh_pkg_install_confirmed`](#ssh_pkg_install_confirmed)
- [`ssh_pkg_simulate_remove`](#ssh_pkg_simulate_remove)
- [`ssh_pkg_remove`](#ssh_pkg_remove)

#### `ssh_pkg_check_updates`

Purpose:

Runs the allowed pkg_check_updates command against a configured SSH profile.

Input arguments:

- `profileName`: SSH profile name.

`tools/call` params sample:

```json
{
  "name": "ssh_pkg_check_updates",
  "arguments": {
    "profileName": "vps01"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_pkg_check_updates",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_pkg_info`

Purpose:

Runs the allowed pkg_info command against a configured SSH profile.

Input arguments:

- `package`: Package name.
- `profileName`: SSH profile name.

`tools/call` params sample:

```json
{
  "name": "ssh_pkg_info",
  "arguments": {
    "package": "nginx",
    "profileName": "vps01"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_pkg_info",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_pkg_search`

Purpose:

Runs a limited package search against a configured SSH profile.

Input arguments:

- `query`: Text search query.
- `profileName`: SSH profile name.
- `limit`: Maximum number of rows to return.

`tools/call` params sample:

```json
{
  "name": "ssh_pkg_search",
  "arguments": {
    "query": "server_name",
    "profileName": "vps01",
    "limit": 40
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_pkg_search",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_pkg_list_installed`

Purpose:

Runs a limited installed-package listing against a configured SSH profile.

Input arguments:

- `filter`: Tool-specific argument of type `string` defined by the MCP schema.
- `profileName`: SSH profile name.
- `limit`: Maximum number of rows to return.

`tools/call` params sample:

```json
{
  "name": "ssh_pkg_list_installed",
  "arguments": {
    "filter": "<value>",
    "profileName": "vps01",
    "limit": 40
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_pkg_list_installed",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `ssh_pkg_simulate_install`

Purpose:

Runs the allowed pkg_simulate_install dry-run command against a configured SSH profile.

Input arguments:

- `package`: Package name.
- `profileName`: SSH profile name.

`tools/call` params sample:

```json
{
  "name": "ssh_pkg_simulate_install",
  "arguments": {
    "package": "nginx",
    "profileName": "vps01"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_pkg_simulate_install",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `ssh_pkg_install`

Purpose:

Returns a confirmation request for the pkg_install command without executing it.

Input arguments:

- `package`: Package name.
- `profileName`: SSH profile name.

`tools/call` params sample:

```json
{
  "name": "ssh_pkg_install",
  "arguments": {
    "package": "nginx",
    "profileName": "vps01"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshConfirmationResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_pkg_install",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `ssh_pkg_install_confirmed`

Purpose:

Runs the allowed pkg_install command after explicit confirmation. The confirmation argument must be pkg_install:<package>.

Input arguments:

- `package`: Package name.
- `profileName`: SSH profile name.
- `confirmation`: Exact confirmation token returned by a check or simulate tool.

`tools/call` params sample:

```json
{
  "name": "ssh_pkg_install_confirmed",
  "arguments": {
    "package": "nginx",
    "profileName": "vps01",
    "confirmation": "<confirmation-token-from-check-result>"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_pkg_install_confirmed",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `ssh_pkg_simulate_remove`

Purpose:

Runs the allowed pkg_simulate_remove dry-run command against a configured SSH profile.

Input arguments:

- `package`: Package name.
- `profileName`: SSH profile name.

`tools/call` params sample:

```json
{
  "name": "ssh_pkg_simulate_remove",
  "arguments": {
    "package": "nginx",
    "profileName": "vps01"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_pkg_simulate_remove",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `ssh_pkg_remove`

Purpose:

Returns a confirmation request for the pkg_remove command without executing it.

Input arguments:

- `package`: Package name.
- `profileName`: SSH profile name.

`tools/call` params sample:

```json
{
  "name": "ssh_pkg_remove",
  "arguments": {
    "package": "nginx",
    "profileName": "vps01"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshConfirmationResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_pkg_remove",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

### Services

Inspect and safely manage systemd services.

Tools in this group:

- [`ssh_service_status`](#ssh_service_status)
- [`ssh_service_is_active`](#ssh_service_is_active)
- [`ssh_service_is_enabled`](#ssh_service_is_enabled)
- [`ssh_list_services`](#ssh_list_services)
- [`ssh_service_enable_now`](#ssh_service_enable_now)
- [`ssh_service_reload`](#ssh_service_reload)
- [`ssh_service_restart`](#ssh_service_restart)
- [`ssh_service_stop`](#ssh_service_stop)
- [`ssh_service_disable`](#ssh_service_disable)

#### `ssh_service_status`

Purpose:

Runs systemctl status for one service without changing service state.

Input arguments:

- `service`: systemd service name.
- `profileName`: SSH profile name.

`tools/call` params sample:

```json
{
  "name": "ssh_service_status",
  "arguments": {
    "service": "nginx.service",
    "profileName": "vps01"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_service_status",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_service_is_active`

Purpose:

Runs systemctl is-active for one service without changing service state.

Input arguments:

- `service`: systemd service name.
- `profileName`: SSH profile name.

`tools/call` params sample:

```json
{
  "name": "ssh_service_is_active",
  "arguments": {
    "service": "nginx.service",
    "profileName": "vps01"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_service_is_active",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_service_is_enabled`

Purpose:

Runs systemctl is-enabled for one service without changing service state.

Input arguments:

- `service`: systemd service name.
- `profileName`: SSH profile name.

`tools/call` params sample:

```json
{
  "name": "ssh_service_is_enabled",
  "arguments": {
    "service": "nginx.service",
    "profileName": "vps01"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_service_is_enabled",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `ssh_list_services`

Purpose:

Lists systemd service units with a validated state filter and line limit.

Input arguments:

- `profileName`: SSH profile name.
- `state`: Tool-specific argument of type `string` defined by the MCP schema.
- `limit`: Maximum number of rows to return.

`tools/call` params sample:

```json
{
  "name": "ssh_list_services",
  "arguments": {
    "profileName": "vps01",
    "state": "<value>",
    "limit": 40
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_list_services",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `ssh_service_enable_now`

Purpose:

Runs systemctl enable --now for one service after explicit confirmation.

Input arguments:

- `service`: systemd service name.
- `profileName`: SSH profile name.
- `confirmation`: Exact confirmation token returned by a check or simulate tool.

`tools/call` params sample:

```json
{
  "name": "ssh_service_enable_now",
  "arguments": {
    "service": "nginx.service",
    "profileName": "vps01",
    "confirmation": "<confirmation-token-from-check-result>"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_service_enable_now",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `ssh_service_reload`

Purpose:

Runs systemctl reload for one service after explicit confirmation.

Input arguments:

- `service`: systemd service name.
- `profileName`: SSH profile name.
- `confirmation`: Exact confirmation token returned by a check or simulate tool.

`tools/call` params sample:

```json
{
  "name": "ssh_service_reload",
  "arguments": {
    "service": "nginx.service",
    "profileName": "vps01",
    "confirmation": "<confirmation-token-from-check-result>"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_service_reload",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `ssh_service_restart`

Purpose:

Runs systemctl restart for one service after explicit confirmation.

Input arguments:

- `service`: systemd service name.
- `profileName`: SSH profile name.
- `confirmation`: Exact confirmation token returned by a check or simulate tool.

`tools/call` params sample:

```json
{
  "name": "ssh_service_restart",
  "arguments": {
    "service": "nginx.service",
    "profileName": "vps01",
    "confirmation": "<confirmation-token-from-check-result>"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_service_restart",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `ssh_service_stop`

Purpose:

Runs systemctl stop for one service after explicit confirmation.

Input arguments:

- `service`: systemd service name.
- `profileName`: SSH profile name.
- `confirmation`: Exact confirmation token returned by a check or simulate tool.

`tools/call` params sample:

```json
{
  "name": "ssh_service_stop",
  "arguments": {
    "service": "nginx.service",
    "profileName": "vps01",
    "confirmation": "<confirmation-token-from-check-result>"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_service_stop",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `ssh_service_disable`

Purpose:

Runs systemctl disable for one service after explicit confirmation.

Input arguments:

- `service`: systemd service name.
- `profileName`: SSH profile name.
- `confirmation`: Exact confirmation token returned by a check or simulate tool.

`tools/call` params sample:

```json
{
  "name": "ssh_service_disable",
  "arguments": {
    "service": "nginx.service",
    "profileName": "vps01",
    "confirmation": "<confirmation-token-from-check-result>"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `SshToolResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "CommandName": "ssh_service_disable",
  "Host": "example.invalid",
  "ExitCode": 0,
  "StandardOutput": "<remote command stdout>",
  "ProfileName": "vps01",
  "Port": 22,
  "CommandText": "<allow-listed command text>",
  "TimedOut": false,
  "StandardError": "",
  "UserName": "deploy",
  "Error": ""
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

### Service config/logs

Operate on provider-approved service configuration files and logs.

Tools in this group:

- [`service_config_paths`](#service_config_paths)
- [`service_config_file_check_read`](#service_config_file_check_read)
- [`service_config_file_read`](#service_config_file_read)
- [`service_config_file_check_write`](#service_config_file_check_write)
- [`service_config_file_write`](#service_config_file_write)
- [`service_config_file_rollback`](#service_config_file_rollback)
- [`service_config_file_commit`](#service_config_file_commit)
- [`service_config_test`](#service_config_test)
- [`service_logfile_read`](#service_logfile_read)

#### `service_config_paths`

Purpose:

Returns configuration file paths for a supported service on a configured SSH profile.

Input arguments:

- `serviceKey`: Tool-specific argument of type `string` defined by the MCP schema.
- `profileName`: SSH profile name.

`tools/call` params sample:

```json
{
  "name": "service_config_paths",
  "arguments": {
    "serviceKey": "<value>",
    "profileName": "vps01"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `ServiceConfigPathsResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "ServiceKey": "default",
  "DisplayName": "<display name>",
  "MainConfig": "/path/example",
  "ConfigFiles": [],
  "IncludePatterns": [],
  "Warnings": []
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `service_config_file_check_read`

Purpose:

Checks whether one provider-approved configuration file can be read without returning its content.

Input arguments:

- `serviceKey`: Tool-specific argument of type `string` defined by the MCP schema.
- `profileName`: SSH profile name.
- `path`: Target path validated by the tool policy or provider.

`tools/call` params sample:

```json
{
  "name": "service_config_file_check_read",
  "arguments": {
    "serviceKey": "<value>",
    "profileName": "vps01",
    "path": "/var/www/example/index.html"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `ServiceConfigFileAccessCheckResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "ServiceKey": "default",
  "DisplayName": "<display name>",
  "Path": "/path/example",
  "CanRead": true,
  "CanWrite": true,
  "RequiresConfirmation": true,
  "Confirmation": "<value>",
  "Method": "<value>",
  "TargetKey": "<value>",
  "Encoding": "<value>",
  "Warnings": []
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `service_config_file_read`

Purpose:

Reads one provider-approved configuration file for a supported service on a configured SSH profile.

Input arguments:

- `serviceKey`: Tool-specific argument of type `string` defined by the MCP schema.
- `profileName`: SSH profile name.
- `path`: Target path validated by the tool policy or provider.

`tools/call` params sample:

```json
{
  "name": "service_config_file_read",
  "arguments": {
    "serviceKey": "<value>",
    "profileName": "vps01",
    "path": "/var/www/example/index.html"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `ServiceConfigFileReadResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "ServiceKey": "default",
  "DisplayName": "<display name>",
  "Path": "/path/example",
  "Content": "<output text>",
  "Encoding": "<value>",
  "Truncated": true,
  "Warnings": []
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `service_config_file_check_write`

Purpose:

Checks whether one provider-limited configuration edit can be written without applying changes.

Input arguments:

- `serviceKey`: Tool-specific argument of type `string` defined by the MCP schema.
- `profileName`: SSH profile name.
- `path`: Target path validated by the tool policy or provider.
- `targetKey`: Configured target key.
- `method`: HTTP method or operation method.
- `targetValue`: Tool-specific argument of type `string?` defined by the MCP schema.

`tools/call` params sample:

```json
{
  "name": "service_config_file_check_write",
  "arguments": {
    "serviceKey": "<value>",
    "profileName": "vps01",
    "path": "/var/www/example/index.html",
    "targetKey": "main",
    "method": "GET",
    "targetValue": "<value>"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `ServiceConfigFileAccessCheckResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "ServiceKey": "default",
  "DisplayName": "<display name>",
  "Path": "/path/example",
  "CanRead": true,
  "CanWrite": true,
  "RequiresConfirmation": true,
  "Confirmation": "<value>",
  "Method": "<value>",
  "TargetKey": "<value>",
  "Encoding": "<value>",
  "Warnings": []
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `service_config_file_write`

Purpose:

Applies one provider-limited configuration edit after explicit confirmation.

Input arguments:

- `serviceKey`: Tool-specific argument of type `string` defined by the MCP schema.
- `profileName`: SSH profile name.
- `path`: Target path validated by the tool policy or provider.
- `targetKey`: Configured target key.
- `method`: HTTP method or operation method.
- `confirmation`: Exact confirmation token returned by a check or simulate tool.
- `targetValue`: Tool-specific argument of type `string?` defined by the MCP schema.

`tools/call` params sample:

```json
{
  "name": "service_config_file_write",
  "arguments": {
    "serviceKey": "<value>",
    "profileName": "vps01",
    "path": "/var/www/example/index.html",
    "targetKey": "main",
    "method": "GET",
    "confirmation": "<confirmation-token-from-check-result>",
    "targetValue": "<value>"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `ServiceConfigFileWriteResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "ServiceKey": "default",
  "DisplayName": "<display name>",
  "Path": "/path/example",
  "Encoding": "<value>",
  "BytesWritten": 0,
  "Warnings": []
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `service_config_file_rollback`

Purpose:

Restores one provider-approved configuration file from its Kelpie backup after explicit confirmation.

Input arguments:

- `serviceKey`: Tool-specific argument of type `string` defined by the MCP schema.
- `profileName`: SSH profile name.
- `path`: Target path validated by the tool policy or provider.
- `confirmation`: Exact confirmation token returned by a check or simulate tool.

`tools/call` params sample:

```json
{
  "name": "service_config_file_rollback",
  "arguments": {
    "serviceKey": "<value>",
    "profileName": "vps01",
    "path": "/var/www/example/index.html",
    "confirmation": "<confirmation-token-from-check-result>"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `ServiceConfigFileBackupActionResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "ServiceKey": "default",
  "DisplayName": "<display name>",
  "Path": "/path/example",
  "BackupPath": "/path/example",
  "Changed": true,
  "Warnings": []
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `service_config_file_commit`

Purpose:

Commits one provider-approved configuration file edit by removing its Kelpie backup after explicit confirmation.

Input arguments:

- `serviceKey`: Tool-specific argument of type `string` defined by the MCP schema.
- `profileName`: SSH profile name.
- `path`: Target path validated by the tool policy or provider.
- `confirmation`: Exact confirmation token returned by a check or simulate tool.

`tools/call` params sample:

```json
{
  "name": "service_config_file_commit",
  "arguments": {
    "serviceKey": "<value>",
    "profileName": "vps01",
    "path": "/var/www/example/index.html",
    "confirmation": "<confirmation-token-from-check-result>"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `ServiceConfigFileBackupActionResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "ServiceKey": "default",
  "DisplayName": "<display name>",
  "Path": "/path/example",
  "BackupPath": "/path/example",
  "Changed": true,
  "Warnings": []
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `service_config_test`

Purpose:

Tests provider-managed configuration files for a supported service after explicit confirmation.

Input arguments:

- `serviceKey`: Tool-specific argument of type `string` defined by the MCP schema.
- `profileName`: SSH profile name.
- `confirmation`: Exact confirmation token returned by a check or simulate tool.

`tools/call` params sample:

```json
{
  "name": "service_config_test",
  "arguments": {
    "serviceKey": "<value>",
    "profileName": "vps01",
    "confirmation": "<confirmation-token-from-check-result>"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `ServiceConfigFileTestResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "ServiceKey": "default",
  "DisplayName": "<display name>",
  "TestCommand": "<command text>",
  "ExitCode": 0,
  "StandardOutput": "<output text>",
  "StandardError": "",
  "Stdout": null,
  "Stderr": null,
  "Warnings": []
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `service_logfile_read`

Purpose:

Reads one provider-approved log file for a supported service on a configured SSH profile.

Input arguments:

- `serviceKey`: Tool-specific argument of type `string` defined by the MCP schema.
- `profileName`: SSH profile name.
- `logKey`: Configured log key.
- `sinceMinutes`: Tool-specific argument of type `int?` defined by the MCP schema.
- `lines`: Maximum number of log or terminal lines to return.

`tools/call` params sample:

```json
{
  "name": "service_logfile_read",
  "arguments": {
    "serviceKey": "<value>",
    "profileName": "vps01",
    "logKey": "access",
    "sinceMinutes": "<value>",
    "lines": 120
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `ServiceLogfileReadResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "ServiceKey": "default",
  "DisplayName": "<display name>",
  "LogKey": "default",
  "Path": "/path/example",
  "Content": "<output text>",
  "Encoding": "<value>",
  "Truncated": true,
  "Warnings": []
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

### Web files

Operate on provider-approved web roots.

Tools in this group:

- [`web_file_list`](#web_file_list)
- [`web_file_search_name`](#web_file_search_name)
- [`web_file_search_text`](#web_file_search_text)
- [`web_file_stat`](#web_file_stat)
- [`web_file_check_write`](#web_file_check_write)
- [`web_file_check_permissions`](#web_file_check_permissions)
- [`web_file_read`](#web_file_read)
- [`web_file_head`](#web_file_head)
- [`web_file_tail`](#web_file_tail)
- [`web_file_write`](#web_file_write)
- [`web_change_owner`](#web_change_owner)
- [`web_change_owner_recursive`](#web_change_owner_recursive)
- [`web_change_mode`](#web_change_mode)
- [`web_change_mode_recursive`](#web_change_mode_recursive)

#### `web_file_list`

Purpose:

Lists provider-approved web files and directories on a configured SSH profile.

Input arguments:

- `profileName`: SSH profile name.
- `siteKey`: Configured web site key.
- `path`: Target path validated by the tool policy or provider.
- `maxDepth`: Maximum traversal depth.
- `limit`: Maximum number of rows to return.

`tools/call` params sample:

```json
{
  "name": "web_file_list",
  "arguments": {
    "profileName": "vps01",
    "siteKey": "default",
    "path": "/var/www/example/index.html",
    "maxDepth": 3,
    "limit": 40
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `WebPublicFileListResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "SiteKey": "default",
  "DisplayName": "<display name>",
  "Path": "/path/example",
  "ResolvedPath": "/path/example",
  "Exists": true,
  "Entries": [],
  "Truncated": true,
  "Warnings": []
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `web_file_search_name`

Purpose:

Searches provider-approved web file and directory names with a restricted glob pattern.

Input arguments:

- `profileName`: SSH profile name.
- `siteKey`: Configured web site key.
- `pattern`: Search pattern or glob accepted by the tool.
- `path`: Target path validated by the tool policy or provider.
- `maxDepth`: Maximum traversal depth.
- `limit`: Maximum number of rows to return.

`tools/call` params sample:

```json
{
  "name": "web_file_search_name",
  "arguments": {
    "profileName": "vps01",
    "siteKey": "default",
    "pattern": "*.conf",
    "path": "/var/www/example/index.html",
    "maxDepth": 3,
    "limit": 40
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `WebPublicFileListResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "SiteKey": "default",
  "DisplayName": "<display name>",
  "Path": "/path/example",
  "ResolvedPath": "/path/example",
  "Exists": true,
  "Entries": [],
  "Truncated": true,
  "Warnings": []
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `web_file_search_text`

Purpose:

Searches readable provider-approved web text files with bounded file size and result limits.

Input arguments:

- `profileName`: SSH profile name.
- `siteKey`: Configured web site key.
- `query`: Text search query.
- `path`: Target path validated by the tool policy or provider.
- `maxDepth`: Maximum traversal depth.
- `limit`: Maximum number of rows to return.
- `maxFileBytes`: Tool-specific argument of type `int` defined by the MCP schema.

`tools/call` params sample:

```json
{
  "name": "web_file_search_text",
  "arguments": {
    "profileName": "vps01",
    "siteKey": "default",
    "query": "server_name",
    "path": "/var/www/example/index.html",
    "maxDepth": 3,
    "limit": 40,
    "maxFileBytes": "<value>"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `WebPublicTextSearchResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "SiteKey": "default",
  "DisplayName": "<display name>",
  "Path": "/path/example",
  "ResolvedPath": "/path/example",
  "Query": "<value>",
  "Exists": true,
  "Matches": [],
  "Truncated": true,
  "Warnings": []
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `web_file_stat`

Purpose:

Returns metadata for one provider-approved web public path on a configured SSH profile.

Input arguments:

- `profileName`: SSH profile name.
- `siteKey`: Configured web site key.
- `path`: Target path validated by the tool policy or provider.

`tools/call` params sample:

```json
{
  "name": "web_file_stat",
  "arguments": {
    "profileName": "vps01",
    "siteKey": "default",
    "path": "/var/www/example/index.html"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `WebPublicFileStatResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "SiteKey": "default",
  "DisplayName": "<display name>",
  "Path": "/path/example",
  "ResolvedPath": "/path/example",
  "Exists": true,
  "Type": "<value>",
  "Size": 0,
  "Mode": "<value>",
  "Owner": "<value>",
  "Group": "<value>",
  "LastModified": "<value>",
  "IsSymlink": true,
  "Warnings": []
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `web_file_check_write`

Purpose:

Checks whether one provider-approved web file can be written without applying changes.

Input arguments:

- `profileName`: SSH profile name.
- `siteKey`: Configured web site key.
- `path`: Target path validated by the tool policy or provider.
- `contentType`: Content type metadata for written content.

`tools/call` params sample:

```json
{
  "name": "web_file_check_write",
  "arguments": {
    "profileName": "vps01",
    "siteKey": "default",
    "path": "/var/www/example/index.html",
    "contentType": "text/html"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `WebPublicFileWriteCheckResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "SiteKey": "default",
  "DisplayName": "<display name>",
  "Path": "/path/example",
  "ResolvedPath": "/path/example",
  "Exists": true,
  "CanWrite": true,
  "RequiresConfirmation": true,
  "Confirmation": "<value>",
  "ContentType": "<output text>",
  "Reason": "<value>",
  "Warnings": []
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `web_file_check_permissions`

Purpose:

Checks whether one provider-approved web public path is eligible for owner/group or mode changes without applying changes.

Input arguments:

- `profileName`: SSH profile name.
- `siteKey`: Configured web site key.
- `path`: Target path validated by the tool policy or provider.
- `owner`: Owner account name.
- `group`: Group name.
- `mode`: Three-digit octal mode for permission changes.
- `recursive`: Tool-specific argument of type `bool` defined by the MCP schema.

`tools/call` params sample:

```json
{
  "name": "web_file_check_permissions",
  "arguments": {
    "profileName": "vps01",
    "siteKey": "default",
    "path": "/var/www/example/index.html",
    "owner": "www-data",
    "group": "www-data",
    "mode": "755",
    "recursive": "<value>"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `WebPublicPermissionCheckResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "SiteKey": "default",
  "DisplayName": "<display name>",
  "Path": "/path/example",
  "ResolvedPath": "/path/example",
  "Exists": true,
  "Type": "<value>",
  "CurrentOwner": "<value>",
  "CurrentGroup": "<value>",
  "CurrentMode": "<value>",
  "CanChangeOwner": true,
  "CanChangeMode": true,
  "OwnerConfirmation": "<value>",
  "ModeConfirmation": "<value>",
  "Reason": "<value>",
  "Warnings": []
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `web_file_read`

Purpose:

Reads one provider-approved web file on a configured SSH profile.

Input arguments:

- `profileName`: SSH profile name.
- `siteKey`: Configured web site key.
- `path`: Target path validated by the tool policy or provider.

`tools/call` params sample:

```json
{
  "name": "web_file_read",
  "arguments": {
    "profileName": "vps01",
    "siteKey": "default",
    "path": "/var/www/example/index.html"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `WebPublicFileReadResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "SiteKey": "default",
  "DisplayName": "<display name>",
  "Path": "/path/example",
  "ResolvedPath": "/path/example",
  "Exists": true,
  "ContentBase64": "<output text>",
  "Encoding": "<value>",
  "ContentType": "<output text>",
  "Size": 0,
  "LastModified": "<value>",
  "Warnings": []
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `web_file_head`

Purpose:

Reads the beginning of one provider-approved web file with bounded bytes and lines.

Input arguments:

- `profileName`: SSH profile name.
- `siteKey`: Configured web site key.
- `path`: Target path validated by the tool policy or provider.
- `maxBytes`: Maximum number of bytes to read.
- `maxLines`: Maximum number of lines to read.

`tools/call` params sample:

```json
{
  "name": "web_file_head",
  "arguments": {
    "profileName": "vps01",
    "siteKey": "default",
    "path": "/var/www/example/index.html",
    "maxBytes": 4096,
    "maxLines": 80
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `WebPublicFileReadResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "SiteKey": "default",
  "DisplayName": "<display name>",
  "Path": "/path/example",
  "ResolvedPath": "/path/example",
  "Exists": true,
  "ContentBase64": "<output text>",
  "Encoding": "<value>",
  "ContentType": "<output text>",
  "Size": 0,
  "LastModified": "<value>",
  "Warnings": []
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `web_file_tail`

Purpose:

Reads the end of one provider-approved web file with bounded bytes and lines.

Input arguments:

- `profileName`: SSH profile name.
- `siteKey`: Configured web site key.
- `path`: Target path validated by the tool policy or provider.
- `maxBytes`: Maximum number of bytes to read.
- `maxLines`: Maximum number of lines to read.

`tools/call` params sample:

```json
{
  "name": "web_file_tail",
  "arguments": {
    "profileName": "vps01",
    "siteKey": "default",
    "path": "/var/www/example/index.html",
    "maxBytes": 4096,
    "maxLines": 80
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `WebPublicFileReadResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "SiteKey": "default",
  "DisplayName": "<display name>",
  "Path": "/path/example",
  "ResolvedPath": "/path/example",
  "Exists": true,
  "ContentBase64": "<output text>",
  "Encoding": "<value>",
  "ContentType": "<output text>",
  "Size": 0,
  "LastModified": "<value>",
  "Warnings": []
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- Read-oriented tool. Do not include real host names, user names, secrets, or customer data in committed examples.

#### `web_file_write`

Purpose:

Writes one provider-approved web file after explicit confirmation, optionally applying owner[:group] and/or mode atomically through the sudo helper.

Input arguments:

- `profileName`: SSH profile name.
- `siteKey`: Configured web site key.
- `path`: Target path validated by the tool policy or provider.
- `contentBase64`: Base64-encoded replacement content.
- `confirmation`: Exact confirmation token returned by a check or simulate tool.
- `encoding`: Text encoding label.
- `contentType`: Content type metadata for written content.
- `owner`: Owner account name.
- `mode`: Three-digit octal mode for permission changes.

`tools/call` params sample:

```json
{
  "name": "web_file_write",
  "arguments": {
    "profileName": "vps01",
    "siteKey": "default",
    "path": "/var/www/example/index.html",
    "contentBase64": "PGgxPkhlbGxvIEtlbHBpZTwvaDE+Cg==",
    "confirmation": "<confirmation-token-from-check-result>",
    "encoding": "utf-8",
    "contentType": "text/html",
    "owner": "www-data",
    "mode": "755"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `WebPublicFileWriteResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "SiteKey": "default",
  "DisplayName": "<display name>",
  "Path": "/path/example",
  "ResolvedPath": "/path/example",
  "Written": true,
  "Created": true,
  "Overwritten": true,
  "ContentType": "<output text>",
  "Size": 0,
  "Warnings": []
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `web_change_owner`

Purpose:

Runs sudo chown for one provider-approved web public path after explicit confirmation.

Input arguments:

- `profileName`: SSH profile name.
- `siteKey`: Configured web site key.
- `path`: Target path validated by the tool policy or provider.
- `owner`: Owner account name.
- `group`: Group name.
- `confirmation`: Exact confirmation token returned by a check or simulate tool.

`tools/call` params sample:

```json
{
  "name": "web_change_owner",
  "arguments": {
    "profileName": "vps01",
    "siteKey": "default",
    "path": "/var/www/example/index.html",
    "owner": "www-data",
    "group": "www-data",
    "confirmation": "<confirmation-token-from-check-result>"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `WebPublicPermissionChangeResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "SiteKey": "default",
  "DisplayName": "<display name>",
  "Path": "/path/example",
  "ResolvedPath": "/path/example",
  "Changed": true,
  "Owner": "<value>",
  "Group": "<value>",
  "Mode": "<value>",
  "Warnings": []
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `web_change_owner_recursive`

Purpose:

Runs sudo chown recursively for one provider-approved web public directory tree after explicit confirmation. Symbolic links are skipped.

Input arguments:

- `profileName`: SSH profile name.
- `siteKey`: Configured web site key.
- `path`: Target path validated by the tool policy or provider.
- `owner`: Owner account name.
- `group`: Group name.
- `confirmation`: Exact confirmation token returned by a check or simulate tool.

`tools/call` params sample:

```json
{
  "name": "web_change_owner_recursive",
  "arguments": {
    "profileName": "vps01",
    "siteKey": "default",
    "path": "/var/www/example/index.html",
    "owner": "www-data",
    "group": "www-data",
    "confirmation": "<confirmation-token-from-check-result>"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `WebPublicPermissionChangeResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "SiteKey": "default",
  "DisplayName": "<display name>",
  "Path": "/path/example",
  "ResolvedPath": "/path/example",
  "Changed": true,
  "Owner": "<value>",
  "Group": "<value>",
  "Mode": "<value>",
  "Warnings": []
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `web_change_mode`

Purpose:

Runs sudo chmod for one provider-approved web public path after explicit confirmation.

Input arguments:

- `profileName`: SSH profile name.
- `siteKey`: Configured web site key.
- `path`: Target path validated by the tool policy or provider.
- `mode`: Three-digit octal mode for permission changes.
- `confirmation`: Exact confirmation token returned by a check or simulate tool.

`tools/call` params sample:

```json
{
  "name": "web_change_mode",
  "arguments": {
    "profileName": "vps01",
    "siteKey": "default",
    "path": "/var/www/example/index.html",
    "mode": "755",
    "confirmation": "<confirmation-token-from-check-result>"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `WebPublicPermissionChangeResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "SiteKey": "default",
  "DisplayName": "<display name>",
  "Path": "/path/example",
  "ResolvedPath": "/path/example",
  "Changed": true,
  "Owner": "<value>",
  "Group": "<value>",
  "Mode": "<value>",
  "Warnings": []
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

#### `web_change_mode_recursive`

Purpose:

Runs sudo chmod recursively for one provider-approved web public directory tree after explicit confirmation. Symbolic links are skipped.

Input arguments:

- `profileName`: SSH profile name.
- `siteKey`: Configured web site key.
- `path`: Target path validated by the tool policy or provider.
- `mode`: Three-digit octal mode for permission changes.
- `confirmation`: Exact confirmation token returned by a check or simulate tool.

`tools/call` params sample:

```json
{
  "name": "web_change_mode_recursive",
  "arguments": {
    "profileName": "vps01",
    "siteKey": "default",
    "path": "/var/www/example/index.html",
    "mode": "755",
    "confirmation": "<confirmation-token-from-check-result>"
  }
}
```

Processing:

KelpieMCPServer validates the MCP schema arguments, resolves any saved profile or supplied remote operation, applies the relevant policy/provider checks, runs the bounded operation, and returns the tool result to the MCP client.

Return value:

- Return type: `WebPublicPermissionChangeResult`.
- The returned object is serialized as MCP tool content, usually as `structuredContent` when the client supports structured tool results.
- Error fields are empty on success and contain validation, policy, connection, or execution errors when the tool cannot complete normally.

Return value sample:

```json
{
  "SiteKey": "default",
  "DisplayName": "<display name>",
  "Path": "/path/example",
  "ResolvedPath": "/path/example",
  "Changed": true,
  "Owner": "<value>",
  "Group": "<value>",
  "Mode": "<value>",
  "Warnings": []
}
```

Execution result sample:

The MCP execution result body is the return value sample above, wrapped by the client as the result of `tools/call`.

Safety notes:

- This tool can change remote or local state. Use the matching check or simulate tool first when available, and pass only the exact confirmation token returned by Kelpie.

## Safety Notes

- MCP callable tools are separate from terminal commands such as `kelpie` and `kelpiemcp`.
- MCP execution never displays passwords or private keys.
- Direct root SSH login is rejected.
- Managed commands must exist in the allow-listed command catalog.
- Raw operations must pass raw shell policy checks.
- Confirmation-gated tools do not change the target unless the confirmation string matches exactly.
- Service configuration and web file operations are limited to provider-approved paths.
- Real host names, real user names, secrets, raw log bodies containing secrets, and unpublished settings must not be recorded in committed documents.


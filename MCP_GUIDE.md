# MCP_GUIDE.md

This guide explains how to use KelpieSSH as an MCP server for Codex or another MCP client.

Japanese documentation is available in [docs/ja/MCP_GUIDE.ja.md](docs/ja/MCP_GUIDE.ja.md).

## What the MCP Server Does

The MCP server is the local bridge between an AI client and KelpieSSH. The AI client connects to the local KelpieSSH MCP server over Streamable HTTP, and the server runs allowed KelpieSSH operations against configured SSH profiles.

The MCP server is only needed when an AI client uses KelpieSSH tools. Normal terminal commands such as `kelpie open vps01`, `kelpie login`, `kelpie status vps01`, `kelpie diag vps01`, and `kelpie logs ...` do not require the MCP server.

## Diagnostic Boundaries

KelpieSSH separates validation and diagnostics by execution target. A success in one boundary does not prove that another boundary is healthy.

| Boundary | Commands or tools | What it verifies | Network behavior |
| :--- | :--- | :--- | :--- |
| Local static validation | `kelpie config check`, `kelpie profile check <profile>` | Local files, JSON, schema, credential references, provider selection, and policy shape | Does not start MCP or open SSH |
| MCP server health | `kelpiemcp status`, `kelpie status <profile>`, `/health`, `kelpie_ping` | Local MCP process, control pipe, HTTP endpoint, or MCP tool dispatch | Uses local process, pipe, or loopback only; does not diagnose the SSH target |
| MCP host diagnostics | `get_system_info`, `get_disk_usage`, `get_memory_usage`, `get_listening_ports` | The local machine running `KelpieMCPServer` | Does not open SSH |
| SSH target diagnostics | `kelpie diag`, `kelpie inventory`, `kelpie services`, `kelpie logs`, and `ssh_*` diagnostic tools | The configured remote target through profile policy and allow-listed SSH commands | Opens SSH to the selected target |

`get_system_info` describes the local MCP host; `ssh_get_system_info` describes the selected SSH target. CLI remote diagnostics execute directly in the `kelpie` process and do not require MCP. The corresponding MCP `ssh_*` tools require a running MCP server because the AI client calls them through that server.

Static validation does not accept a changed trust baseline, start the server, test credentials against a target, or prove SSH reachability. Likewise, an SSH target failure does not by itself mean the local MCP server is unhealthy.

## Server Instructions and Prompt

During MCP initialization, KelpieSSH announces server instructions that describe its purpose and safety boundaries. Compatible AI clients receive guidance to discover the target and its capabilities first, prefer dedicated read-only tools, protect credentials, preview changes, and respect profile policy, confirmation, audit, and rollback controls.

KelpieSSH also exposes the `kelpie_get_started` MCP prompt. Select it in a prompt-capable MCP client and optionally provide a diagnosis or maintenance goal. The returned workflow leads the AI through `get_target_inventory`, `ssh_get_capabilities`, focused diagnostics, change checks, approval, and post-change verification. A goal must not contain credentials or secret values.

Server instructions and prompts guide the AI client but do not grant permissions. The selected profile's `Mode`, `AllowedRoots`, `SpecialPaths`, trust state, and each tool's validation remain authoritative.

## MCP Files and Layout

An installed or zip-extracted KelpieSSH layout includes the MCP frontend command and the MCP server body:

```text
F:\Kelpie
├─ bin
│  ├─ kelpie.exe
│  ├─ kelpiemcp.exe
│  └─ mcp
│     └─ KelpieMCPServer.exe
└─ config
   └─ kelpiemcp.json
```

`kelpiemcp.exe` is the command used to start, stop, and inspect the local server. `KelpieMCPServer.exe` is the server process that exposes the MCP endpoint.

`kelpiemcp` and `KelpieMCPServer` read `config/kelpiemcp.json`.

When building from source, publish the MCP server body into the MCP directory:

```powershell
dotnet publish src\KelpieMCPServer\KelpieMCPServer.csproj -c Release -o F:\Kelpie\bin\mcp
```

## Configuration

For general Kelpie configuration files and field details, see [CONFIG.md](CONFIG.md).

Persistent server options are configured in:

```text
<KelpieHome>\config\kelpiemcp.json
```

The public port is selected when `KelpieMCPServer` starts:

```powershell
KelpieMCPServer --port 45432
KelpieMCPServer --runtime-base "<KelpieHome>\bin" --port 45432
```

`--port` accepts values from `1` through `65535`. Its default is `45432`. The `Server.Port` value in `kelpiemcp.json` is not used and is removed when `kelpie init` next updates the configuration.

Profiles are loaded into the MCP server when it starts. After editing files under `<KelpieHome>\profiles`, the user runs `kelpiemcp profile reload <profile>` to update both the trust store and the in-memory profile catalog. The `profile_reload` MCP tool does not update trusted profile hashes and is not the acceptance path for intentional profile file edits. Changes to `kelpiemcp.json` require a server restart with `kelpiemcp start --reload-config`.

## Starting the Server

Start the local MCP server before connecting from Codex, Claude, or another MCP client:

```powershell
kelpiemcp start
```

Check that it is running:

```powershell
kelpiemcp status
```

Stop the MCP server when you no longer need MCP access:

```powershell
kelpiemcp stop
```

## Windows Service Registration

On Windows, you can register `KelpieMCPServer` as a Windows Service when you want the MCP server body to be managed by Windows Service Control Manager.

Open a terminal running as administrator and register the service:

```powershell
kelpiemcp service register
```

The service is registered as `KelpieMCPServer` with automatic startup and a service description. Start it immediately with:

```powershell
Start-Service KelpieMCPServer
```

Check the service registration state:

```powershell
kelpiemcp service status
```

Stop the running service before unregistering it:

```powershell
Stop-Service KelpieMCPServer
```

Unregister the service from a terminal running as administrator:

```powershell
kelpiemcp service unregister
```

The service registration stores `--runtime-base` (the command directory, normally `<KelpieHome>\bin`), the port, and any explicit directory overrides in the service binary command line. Kelpie home is the parent of that command directory; `--bin-dir` takes precedence. The service uses the same files as normal startup only when those paths agree. The service working directory and interactive user's home do not select the data directory. Register again with the intended overrides to update an existing registration.

The default service identity is LocalSystem. Ensure that identity can read configuration, profiles, SSH keys, and the trust-store pair, and can write data and logs. Protect the containing directories from unauthorized changes. TrustStore version 4 uses AES-256-GCM and stores its key in `dat\mcp_trusted_store.dat.key`. New Windows key ACLs disable inheritance and grant FullControl to LocalSystem, Administrators, and the creating identity. Normal reads validate the existing ACL without changing it: inheritance and access grants to broad principals such as Everyone, Authenticated Users, or Builtin Users are rejected. Administrators may explicitly grant a runtime identity Read access; normal reads do not require permission to change the ACL. Legacy version 2 migration still protects the key ACL and requires permission to change it. A separate service account is not configured automatically by `service register`.

On Unix, new trust-store keys are created with owner read/write permissions (`0600`, subject to the process umask). Loading or saving an existing store restricts its key to `0600` before reading it. If the process cannot inspect or restrict key permissions, the operation fails without replacing the store or generating a replacement key.

### Trust-Store Backup and Recovery

1. Stop the server and prevent offline CLI trust changes while taking or restoring a backup.
2. Back up `mcp_trusted_store.dat` and `mcp_trusted_store.dat.key` together, alongside the corresponding configuration and profiles. Protect the backup directory with the same restricted access; the key is not protected by DPAPI.
3. Restore the matching pair into the registered data directory while the server remains stopped. Restore the matching configuration and profiles, then apply the intended directory and key ACLs. Do not combine a store and key from different backups.
4. Start the server and verify health, the profile inventory, and a read-only operation. A missing, unreadable, invalid, or mismatched key must be investigated; do not delete the store or use a reload operation to recreate trust as a recovery shortcut.

Restoring only the store or only the key is insufficient. Legacy version 3 migration still requires the original DPAPI user once; this does not apply to ordinary version 4 access.

For a source checkout, `TrustStoreOperationalTests` checks missing keys, invalid or unrelated keys, actual Windows ACL denial, and restoration of a matching backup pair. Build the standalone probe with `dotnet build tests/KelpieSSH.ServiceIdentityProbe/KelpieSSH.ServiceIdentityProbe.csproj`, then run `scripts\Test-KelpieTrustStoreServiceIdentity.ps1` from an administrator PowerShell terminal. The probe compiles the same test source and runs the five cases directly as LocalSystem. It creates and removes a uniquely named temporary scheduled task and keeps results under `.artifacts/service-ops-results`; it does not register, stop, or restart the installed MCP service. This verifies trust-store operations under the service identity, not the entire Service Control Manager lifecycle.

The temporary task starts the .NET 8 probe executable directly. It does not start a PowerShell worker, resolve an SDK, or launch VSTest and a separate test host. `result.json` contains the actual identity, exit code, counters, and individual case outcomes. `worker-state.json` records the current case, process ID, and timestamps; `test.log` records completed cases and failures. The launcher requires the five expected case names and successful outcomes before reporting success. It preserves `task-diagnostics.json` before removing the task, including on timeout, and reports the last case or process-startup stage. The default timeout is 600 seconds; `-TimeoutSeconds` accepts 10 through 900 seconds.

`tests\scripts\Test-KelpieTrustStoreLauncher.ps1` simulates task success, early exit, queued/running timeout, missing case results, and an incorrect identity. `tests\scripts\Test-KelpieTrustStoreWorker.ps1` runs the same standalone probe under the current identity with an explicit regression option, records that actual identity, and checks rejection without the option. The scheduled SYSTEM task never enables that option. Regression results are not proof of LocalSystem access.

Use either normal process startup or Windows Service startup for one MCP server instance; do not run both at the same time.

## AI Client Connection Settings

The default Streamable HTTP MCP endpoint is:

```text
http://127.0.0.1:45432/mcp
```

If a different runtime port is supplied with `--port`, update the AI client MCP configuration to match.

### Codex

Add the Streamable HTTP MCP server URL to the Codex MCP configuration:

```toml
[mcp_servers.kelpie]
url = "http://127.0.0.1:45432/mcp"
```

Restart or reload Codex after changing the MCP configuration.

### Claude

For Claude Code, add KelpieSSH as a Streamable HTTP MCP server:

```powershell
claude mcp add --transport http kelpie http://127.0.0.1:45432/mcp
```

Check that the server is registered:

```powershell
claude mcp list
```

For Claude clients that use a JSON MCP server configuration, register the same Streamable HTTP endpoint:

```json
{
  "mcpServers": {
    "kelpie": {
      "type": "http",
      "url": "http://127.0.0.1:45432/mcp"
    }
  }
}
```

## Password Sessions

For password-based SSH profiles, store or clear the password in the running server session with:

```powershell
kelpiemcp password vps01
kelpiemcp forget vps01
```

The password is sent to the running `KelpieMCPServer` over the local control pipe and kept only in memory for that server process.

From an MCP client, use `ssh_logout` to clear the password session for a profile. To close an interactive SSH terminal connection opened through MCP, use `ssh_connection_close` with the terminal handle.

## Startup Errors

Startup failures are reported with sanitized guidance instead of raw exception text:

- Invalid `kelpiemcp.json`: run `kelpie config check`, then correct the configuration.
- Untrusted configuration change: restart with `--reload-config` after reviewing the change.
- Endpoint already in use: stop the existing server or select another `--port`.
- Access denied: verify Kelpie home permissions and control-pipe ownership.
- Other failure: inspect the Kelpie log for the detailed internal exception.

## MCP command-line tools

The MCP command-line tool list is documented in [MCP_COMMANDS.md](MCP_COMMANDS.md).


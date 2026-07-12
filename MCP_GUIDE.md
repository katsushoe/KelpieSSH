# MCP_GUIDE.md

This guide explains how to use KelpieSSH as an MCP server for Codex or another MCP client.

Japanese documentation is available in [docs/ja/MCP_GUIDE.ja.md](docs/ja/MCP_GUIDE.ja.md).

## What the MCP Server Does

The MCP server is the local bridge between an AI client and KelpieSSH. The AI client connects to the local KelpieSSH MCP server over Streamable HTTP, and the server runs allowed KelpieSSH operations against configured SSH profiles.

The MCP server is only needed when an AI client uses KelpieSSH tools. Normal terminal commands such as `kelpie open vps01`, `kelpie login`, `kelpie status vps01`, `kelpie diag vps01`, and `kelpie logs ...` do not require the MCP server.

## MCP Files and Layout

An installed or zip-extracted KelpieSSH layout includes the MCP frontend command and the MCP server body:

```text
D:\Kelpie
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
dotnet publish src\KelpieMCPServer\KelpieMCPServer.csproj -c Release -o D:\Kelpie\bin\mcp
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
KelpieMCPServer --runtime-base "<runtime-home>" --port 45432
```

`--port` accepts values from `1` through `65535`. Its default is `45432`. The `Server.Port` value in `kelpiemcp.json` is not used, even when it remains in an existing configuration file.

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

The service uses the same `config\kelpiemcp.json`, profiles, data, and logs under Kelpie home as the normal `kelpiemcp start` process. Use either normal process startup or Windows Service startup for one MCP server instance; do not run both at the same time.

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

## MCP command-line tools

The MCP command-line tool list is documented in [MCP_COMMANDS.md](MCP_COMMANDS.md).

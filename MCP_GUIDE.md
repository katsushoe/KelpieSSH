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

The port and server options are configured in:

```text
<KelpieHome>\config\kelpiemcp.json
```

The default server port is `45432`.

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

## AI Client Connection Settings

The default Streamable HTTP MCP endpoint is:

```text
http://127.0.0.1:45432/mcp
```

If the port is changed in `kelpiemcp.json`, update the AI client MCP configuration to match.

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

## MCP command-line tools

The MCP command-line tool list is documented in [MCP_COMMANDS.md](MCP_COMMANDS.md).


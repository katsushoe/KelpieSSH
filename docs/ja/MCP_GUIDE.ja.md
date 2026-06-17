# MCP_GUIDE.ja.md

このガイドでは、Codex などの MCP client から KelpieSSH を MCP サーバーとして使う方法を説明します。

English documentation is available in [../../MCP_GUIDE.md](../../MCP_GUIDE.md).

呼び出し可能な MCP tools と schema の一覧は [MCP_COMMANDS.ja.md](MCP_COMMANDS.ja.md) を参照してください。

## MCP サーバーの役割

MCP サーバーは、AI client と KelpieSSH をつなぐローカルの橋渡しです。AI client は Streamable HTTP でローカルの KelpieSSH MCP サーバーに接続し、サーバーは設定済み SSH profile に対して許可された KelpieSSH 操作を実行します。

MCP サーバーが必要なのは、AI client から KelpieSSH tools を使う場合だけです。`kelpie open vps01`、`kelpie login`、`kelpie status vps01`、`kelpie diag vps01`、`kelpie logs ...` のような通常のターミナルコマンドには MCP サーバーは不要です。

## MCP ファイルと配置

インストールまたは zip 展開された KelpieSSH には、MCP frontend command と MCP server body が含まれます。

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

`kelpiemcp.exe` は、ローカルサーバーの起動、停止、状態確認に使う command です。`KelpieMCPServer.exe` は MCP endpoint を公開する server process です。

`kelpiemcp` と `KelpieMCPServer` は `config/kelpiemcp.json` を読み込みます。

ソースからビルドする場合は、MCP server body を MCP directory に publish します。

```powershell
dotnet publish src\KelpieMCPServer\KelpieMCPServer.csproj -c Release -o D:\Kelpie\bin\mcp
```

## 設定

既定の MCP endpoint は次のとおりです。

```text
http://127.0.0.1:45432/mcp
```

port と server options は次のファイルで設定します。

```text
<KelpieHome>\config\kelpiemcp.json
```

port を変更した場合は、AI client 側の MCP 設定も合わせて更新してください。

## サーバー起動

Codex などの MCP client から接続する前に、ローカル MCP サーバーを起動します。

```powershell
kelpiemcp start
```

起動していることを確認します。

```powershell
kelpiemcp status
```

Codex MCP 設定へ Streamable HTTP MCP server URL を追加します。

```toml
[mcp_servers.kelpie]
url = "http://127.0.0.1:45432/mcp"
```

MCP access が不要になったら、MCP サーバーを停止します。

```powershell
kelpiemcp stop
```

## パスワードセッション

パスワード認証の SSH profile を使う場合は、実行中のサーバーセッションにパスワードを保存または削除します。

```powershell
kelpiemcp password vps01
kelpiemcp forget vps01
```

パスワードはローカル control pipe を通して実行中の `KelpieMCPServer` へ送られ、そのサーバープロセスのメモリ内にのみ保持されます。

## MCP tools

現在の tools は次のとおりです。

- `kelpie_ping`
- `get_system_info`
- `get_disk_usage`
- `get_memory_usage`
- `get_listening_ports`
- `ssh_run_allowed_command`
- `get_target_inventory`
- `ssh_get_system_info`
- `ssh_get_disk_usage`
- `ssh_get_memory_usage`
- `ssh_get_listening_ports`
- `ssh_get_failed_services`
- `ssh_tail_log`

SSH tool results は raw `StandardOutput` / `StandardError` strings を保持し、行配列も公開します。

- `Stdout` / `Stderr`: ANSI escape sequences を保持したまま行分割した出力。
- `StdoutPlain` / `StderrPlain`: ANSI escape sequences を除去した後に行分割した出力。

# MCP_GUIDE.ja.md

このガイドでは、Codex などの MCP client から KelpieSSH を MCP サーバーとして使う方法を説明します。

English documentation is available in [../../MCP_GUIDE.md](../../MCP_GUIDE.md).

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

Kelpie の設定ファイル全般と各項目の詳細は [CONFIG.ja.md](CONFIG.ja.md) を参照してください。

port と server options は次のファイルで設定します。

```text
<KelpieHome>\config\kelpiemcp.json
```

既定の server port は `45432` です。

## サーバー起動

Codex、Claude、またはほかの MCP client から接続する前に、ローカル MCP サーバーを起動します。

```powershell
kelpiemcp start
```

起動していることを確認します。

```powershell
kelpiemcp status
```

MCP access が不要になったら、MCP サーバーを停止します。

```powershell
kelpiemcp stop
```

## AI client 接続設定

既定の Streamable HTTP MCP endpoint は次のとおりです。

```text
http://127.0.0.1:45432/mcp
```

`kelpiemcp.json` で port を変更した場合は、AI client 側の MCP 設定も合わせて更新してください。

### Codex

Codex MCP 設定へ Streamable HTTP MCP server URL を追加します。

```toml
[mcp_servers.kelpie]
url = "http://127.0.0.1:45432/mcp"
```

MCP 設定を変更した後は、Codex を再起動または reload してください。

### Claude

Claude Code では、KelpieSSH を Streamable HTTP MCP server として追加します。

```powershell
claude mcp add --transport http kelpie http://127.0.0.1:45432/mcp
```

server が登録されたことを確認します。

```powershell
claude mcp list
```

JSON MCP server configuration を使う Claude client では、同じ Streamable HTTP endpoint を登録します。

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

## パスワードセッション

パスワード認証の SSH profile を使う場合は、実行中のサーバーセッションにパスワードを保存または削除します。

```powershell
kelpiemcp password vps01
kelpiemcp forget vps01
```

パスワードはローカル control pipe を通して実行中の `KelpieMCPServer` へ送られ、そのサーバープロセスのメモリ内にのみ保持されます。

## MCP コマンドラインツール

MCP コマンドラインツールの一覧は [MCP_COMMANDS.ja.md](MCP_COMMANDS.ja.md) にありますので、そちらを参照してください。


# MCP_GUIDE.ja.md

このガイドでは、Codex などの MCP client から KelpieSSH を MCP サーバーとして使う方法を説明します。

English documentation is available in [../../MCP_GUIDE.md](../../MCP_GUIDE.md).

## MCP サーバーの役割

MCP サーバーは、AI client と KelpieSSH をつなぐローカルの橋渡しです。AI client は Streamable HTTP でローカルの KelpieSSH MCP サーバーに接続し、サーバーは設定済み SSH profile に対して許可された KelpieSSH 操作を実行します。

MCP サーバーが必要なのは、AI client から KelpieSSH tools を使う場合だけです。`kelpie open vps01`、`kelpie login`、`kelpie status vps01`、`kelpie diag vps01`、`kelpie logs ...` のような通常のターミナルコマンドには MCP サーバーは不要です。

## 診断の責務境界

KelpieSSHは、検証・診断対象を次の4境界に分離します。ある境界の成功は、別の境界が正常であることを保証しません。

| 境界 | コマンドまたはtool | 確認対象 | ネットワーク動作 |
| :--- | :--- | :--- | :--- |
| ローカル静的検査 | `kelpie config check`、`kelpie profile check <profile>` | ローカルfile、JSON、schema、認証参照、provider選択、policy形式 | MCPを起動せず、SSH接続もしない |
| MCPサーバー疎通 | `kelpiemcp status`、`kelpie status <profile>`、`/health`、`kelpie_ping` | ローカルMCP process、control pipe、HTTP endpoint、MCP tool dispatch | ローカルprocess、pipe、loopbackだけを使い、SSH対象は診断しない |
| MCP実行ホスト診断 | `get_system_info`、`get_disk_usage`、`get_memory_usage`、`get_listening_ports` | `KelpieMCPServer`を実行しているローカルmachine | SSH接続しない |
| SSH対象診断 | `kelpie diag`、`kelpie inventory`、`kelpie services`、`kelpie logs`、診断用`ssh_*` tools | profile policyと許可済みSSH commandを通したremote対象 | 選択した対象へSSH接続する |

`get_system_info`はローカルMCP host、`ssh_get_system_info`は選択したSSH対象を返します。CLIのremote診断は`kelpie` processから直接実行するためMCPは不要です。MCPの`ssh_*` toolsはAI clientがMCPサーバー経由で呼び出すため、MCPサーバーが必要です。

静的検査は、変更されたtrust baselineの受け入れ、サーバー起動、対象に対する認証確認、SSH到達確認を行いません。また、SSH対象の失敗だけでローカルMCPサーバー異常とは判断しません。

## MCP ファイルと配置

インストールまたは zip 展開された KelpieSSH には、MCP frontend command と MCP server body が含まれます。

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

`kelpiemcp.exe` は、ローカルサーバーの起動、停止、状態確認に使う command です。`KelpieMCPServer.exe` は MCP endpoint を公開する server process です。

`kelpiemcp` と `KelpieMCPServer` は `config/kelpiemcp.json` を読み込みます。

ソースからビルドする場合は、MCP server body を MCP directory に publish します。

```powershell
dotnet publish src\KelpieMCPServer\KelpieMCPServer.csproj -c Release -o F:\Kelpie\bin\mcp
```

## 設定

Kelpie の設定ファイル全般と各項目の詳細は [CONFIG.ja.md](CONFIG.ja.md) を参照してください。

永続的な server options は次のファイルで設定します。

```text
<KelpieHome>\config\kelpiemcp.json
```

公開ポートは `KelpieMCPServer` の起動時に指定します。

```powershell
KelpieMCPServer --port 45432
KelpieMCPServer --runtime-base "<runtime-home>" --port 45432
```

`--port` の設定範囲は `1`～`65535`、既定値は `45432` です。既存の `kelpiemcp.json` に `Server.Port` が残っていても使用せず、次回 `kelpie init` で設定を更新するときに削除します。

Profiles は MCP サーバー起動時にメモリへ読み込まれます。`<KelpieHome>\profiles` 配下のファイルを編集した後は、利用者が `kelpiemcp profile reload <profile>` を実行して trust store と in-memory profile catalog を更新します。`profile_reload` MCP tool は trust store の profile hash を更新しないため、正規の profile 編集を受け入れる操作には使いません。`kelpiemcp.json` の変更後は `kelpiemcp start --reload-config` による server restart が必要です。

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

## Windows Service 登録方法

Windows では、`KelpieMCPServer` を Windows Service として登録できます。MCP server body を Windows Service Control Manager で管理したい場合に使います。

管理者権限のターミナルでサービスを登録します。

```powershell
kelpiemcp service register
```

サービス名は `KelpieMCPServer` です。登録時の startup type は自動起動で、サービス説明文も設定されます。すぐに起動するには次のコマンドを使います。

```powershell
Start-Service KelpieMCPServer
```

サービス登録状態を確認します。

```powershell
kelpiemcp service status
```

登録解除前に、実行中のサービスを停止します。

```powershell
Stop-Service KelpieMCPServer
```

管理者権限のターミナルでサービス登録を解除します。

```powershell
kelpiemcp service unregister
```

Windows Service は通常の `kelpiemcp start` プロセスと同じ Kelpie home 配下の `config\kelpiemcp.json`、profiles、data、logs を使います。通常プロセス起動と Windows Service 起動はどちらか一方を使い、同時に起動しないでください。

## AI client 接続設定

既定の Streamable HTTP MCP endpoint は次のとおりです。

```text
http://127.0.0.1:45432/mcp
```

`--port` で既定値以外を指定した場合は、AI client 側の MCP 設定も合わせて更新してください。

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

MCP client からは、`ssh_logout` で profile の password session を削除できます。MCP で開いた対話 SSH terminal connection を閉じる場合は、terminal handle を指定して `ssh_connection_close` を呼び出します。

## 起動エラー

起動失敗時はraw例外本文ではなく、安全な対処案を表示します。

- `kelpiemcp.json`不正: `kelpie config check`を実行し、設定を修正します。
- 未信頼の設定変更: 変更内容を確認後、`--reload-config`で再起動します。
- endpoint使用中: 既存サーバーを停止するか、別の`--port`を指定します。
- access denied: Kelpie homeの権限とcontrol pipeの所有状態を確認します。
- その他: 詳細な内部例外はKelpie logで確認します。

## MCP コマンドラインツール

MCP コマンドラインツールの一覧は [MCP_COMMANDS.ja.md](MCP_COMMANDS.ja.md) にありますので、そちらを参照してください。


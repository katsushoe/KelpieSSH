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

## サーバー指示とプロンプト

KelpieSSH は MCP 初期化時に、製品の目的と安全境界を示す Server Instructions を通知します。対応する AI client は、対象と capability の事前確認、専用の読み取り専用 tool の優先、認証情報の保護、変更前の preview、profile policy・確認・監査・rollback の順守に関する案内を受け取ります。

また、KelpieSSH は MCP prompt `kelpie_get_started` を公開します。Prompt 対応 MCP client で選択し、必要に応じて診断または保守の目的を指定すると、`get_target_inventory`、`ssh_get_capabilities`、必要最小限の診断、変更前検査、承認、変更後検証の順に AI を案内します。目的には認証情報や秘密値を含めないでください。

サーバー指示とpromptはAI clientへの案内であり、権限を追加しません。選択したprofileの`Mode`、`AllowedRoots`、`SpecialPaths`、trust状態、各toolの検証結果が常に優先されます。

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
KelpieMCPServer --runtime-base "<KelpieHome>\bin" --port 45432
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

## Windowsサービスの登録方法

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

サービス登録は、`--runtime-base`（コマンドの配置ディレクトリ。通常は`<KelpieHome>\bin`）、ポート、個別のディレクトリ指定をサービスの実行引数へ保存します。Kelpie home はそのコマンドディレクトリの親で、`--bin-dir`を指定した場合はそちらを優先します。通常起動と同じファイルを使うのは、指定したパスが一致する場合です。サービスの作業ディレクトリや対話ユーザーのホームから保存先を選択しません。既存登録のパスを更新する場合は、必要なディレクトリ指定を付けて再登録します。

既定のサービス実行アカウントはLocalSystemです。設定、プロファイル、SSH鍵、信頼ストアの対を読めることと、データ・ログを書けることを確認し、格納ディレクトリを権限のない変更から保護してください。TrustStore version 4はAES-256-GCMを使い、鍵を`dat\mcp_trusted_store.dat.key`へ保存します。Windowsの新規鍵ACLは継承を無効化し、LocalSystem、Administrators、作成アカウントにFullControlを付与します。通常読込では既存ACLを変更せず検証し、継承された権限やEveryone、Authenticated Users、Builtin Usersなど広い対象への許可を拒否します。管理者は実行アカウントへ明示的にRead権限を追加できます。通常読込にACL変更権限は不要です。旧version 2からの移行では鍵ACLを保護するため、ACL変更権限が必要です。`service register`は独自アカウントを自動設定しません。

### 信頼ストアのバックアップと復旧

Unixでは、新規の信頼ストア鍵を所有者だけが読み書きできる権限（`0600`。プロセスのumaskによる制限を受けます）で作成します。既存ストアの読込・保存では、鍵を読む前に権限を`0600`へ制限します。権限の確認・変更ができない場合は、ストアの置換や代替鍵の生成を行わず処理を拒否します。

1. サーバーを停止し、バックアップ・復旧中は停止中CLIによる信頼情報の変更も防ぎます。
2. `mcp_trusted_store.dat`と`mcp_trusted_store.dat.key`を対で、対応する設定・プロファイルと一緒に退避します。退避先も同等の限定したアクセス権で保護します。鍵はDPAPIで保護されていません。
3. 停止中に、対応する対を登録済みのデータディレクトリへ戻します。設定・プロファイルも対応する状態へ戻し、ディレクトリと鍵に必要なACLを設定します。異なる退避時点のストアと鍵を組み合わせてはいけません。
4. 起動後に正常性、プロファイル一覧、読取専用操作を確認します。鍵の欠落・読取不能・形式不正・不一致は原因を調査し、ストア削除や再読込操作による信頼状態の再作成を復旧の代用にしないでください。

ストアだけ、または鍵だけの復元では不十分です。旧version 3の移行時は元のDPAPIユーザーによる一度の復号が必要ですが、通常のversion 4読書きには該当しません。

ソースからの検証では、`TrustStoreOperationalTests`が鍵欠落、不正長・別鍵、Windows ACLによる拒否、バックアップ対の復旧を確認します。`dotnet build tests/KelpieSSH.ServiceIdentityProbe/KelpieSSH.ServiceIdentityProbe.csproj`で専用プログラムをビルドし、管理者PowerShellから`scripts\Test-KelpieTrustStoreServiceIdentity.ps1`を実行してください。既存試験と同じソースをコンパイルし、LocalSystemで5件を直接実行します。一意名の一時タスクを作成・削除し、結果を`.artifacts/service-ops-results`へ保存します。既存MCPサービスの登録・停止・再起動は行いません。サービス実行アカウントでの信頼ストア操作の検証であり、Service Control Managerの起動全体を検証するものではありません。

一時タスクは.NET 8の検証実行ファイルを直接起動します。PowerShellの子スクリプト、SDKの解決、VSTestと別のテストホストの起動を省きます。`result.json`に実行アカウント判定、終了コード、件数、各試験の結果を保存します。`worker-state.json`には実行中の試験、プロセスID、時刻、`test.log`には完了した試験と失敗を記録します。起動側は期待する5件の名称と合格を確認してから成功を返します。タイムアウトを含め、タスク削除前に`task-diagnostics.json`を保存し、停止箇所が試験かプロセス起動かも表示します。タイムアウトの初期値は600秒で、`-TimeoutSeconds`には10～900秒を指定できます。

`tests\scripts\Test-KelpieTrustStoreLauncher.ps1`は成功、早期終了、待機中・実行中のタイムアウト、試験結果不足、異なる実行アカウントを模擬検証します。`tests\scripts\Test-KelpieTrustStoreWorker.ps1`は明示的な回帰用オプションで現在のアカウントから同じ検証実行ファイルを起動し、実際のアカウント判定と、オプションなしでの拒否を確認します。SYSTEMの一時タスクではこのオプションを使いません。通常アカウントの回帰結果をLocalSystemでのアクセス成功とは扱いません。

通常プロセス起動とWindows Service起動はどちらか一方を使い、同時に起動しないでください。

## AIクライアントの接続設定

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


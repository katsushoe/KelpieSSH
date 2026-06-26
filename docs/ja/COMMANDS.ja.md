# KelpieSSH Commands

最終更新: 2026-06-26

このファイルは、利用者が通常のターミナルから直接実行する `kelpie` / `kelpiemcp` CLI コマンドの正本です。
コマンドラインオプションの詳細は [CLI_OPTIONS.md](../../CLI_OPTIONS.md) を参照してください。
MCP callable tool の仕様と実行例は `MCP_COMMANDS.ja.md` を正本とします。

## Command Groups

| Group | Command | 内容 |
| :--- | :--- | :--- |
| MCP server control | `kelpiemcp start [--reload-config]`, `kelpiemcp stop`, `kelpiemcp status` | `KelpieMCPServer` の起動、停止、状態確認を行う。 |
| MCP profile trust | `kelpiemcp profile add <profile>`, `kelpiemcp profile reload <profile>`, `kelpiemcp profile revoke <profile>`, `kelpiemcp profile-capabilities [profile]` | SSH profile の信頼 baseline 追加、更新、取り消し、確認を行う。 |
| MCP Windows Service | `kelpiemcp service register`, `kelpiemcp service unregister`, `kelpiemcp service status` | Windows Service 登録、登録解除、登録状態確認を行う。 |
| MCP password session | `kelpiemcp password`, `kelpiemcp forget` | 起動中の MCP server に SSH パスワードを一時保存、削除する。 |
| Compatibility | `kelpiemcp login`, `kelpiemcp logout` | 旧名互換。新規利用では `password` / `forget` を使う。 |
| Initialization | `kelpie init [--silent] [profile]`, `kelpie config --check` | `KelpieHome` 配下の初期ディレクトリとサンプル設定を作成・検証する。 |
| Profile/session | `kelpie profile create`, `kelpie profile edit`, `kelpie profile delete`, `kelpie profile clean`, `kelpie profile commit`, `kelpie profile rollback`, `kelpie open`, `kelpie login`, `kelpie logout`, `kelpie profiles`, `kelpie sessions`, `kelpie kill` | SSH プロファイルひな形作成・編集・削除、プロファイル選択、ログイン、セッション表示、セッション終了を行う。 |
| Mode/UI | `kelpie gui`, `kelpie cli`, `kelpie login --console`, `kelpie login --desktop` | CLI/GUI モードや一時的な起動方式を切り替える。 |
| Diagnostics | `kelpie profile check`, `kelpie profile show`, `kelpie status`, `kelpie diag`, `kelpie logs` | プロファイル検証、プロファイル情報、MCP server 状態、SSH 診断、サービスログを表示する。 |
| Environment | `kelpie env keys`, `kelpie env peek`, `kelpie env set`, `kelpie env list`, `kelpie env persist`, `kelpie env remove` | profile policy に従って remote 環境変数の key 表示、値参照、一時設定、永続化を行う。 |
| Help/version | `kelpie version`, `kelpie help`, `kelpiemcp version`, `kelpiemcp help` | バージョンとヘルプを表示する。 |
| Candidates | `kelpie services`, `kelpie pkg ...` | 今後追加候補。 |

## Common Rules

`KelpieHome` は `kelpie` / `kelpiemcp` の配置ディレクトリの1つ上に固定します。たとえば `D:\Kelpie\bin\kelpie.exe` から実行した場合、`KelpieHome` は `D:\Kelpie` です。

runtime directory override、dry-run、Silent モード、profile transaction option の詳細は [CLI_OPTIONS.md](../../CLI_OPTIONS.md) を参照してください。

設定ファイルはコマンド単位で分けます。

- `kelpiemcp` と `KelpieMCPServer`: `KelpieHome/config/kelpiemcp.json`
- `kelpie`: `KelpieHome/config/kelpie.json`
- SSHプロファイル: `KelpieHome/profiles/<profile>.json`

SSHプロファイルの認証設定は、同じ `profiles/<profile>.json` の直下 `Auth` または `Authentication` に書きます。正式名は `Authentication`、短縮名は `Auth` です。平文パスワードはプロファイルに書きません。

`Platform.OsFamily` は、エンドユーザーが知っているOS名を書いてよいです。Kelpie内部では、コマンド処理プロバイダを選ぶために command OS family へ正規化します。

| `OsFamily` 設定値 | command OS family | 主な対象OS |
| :--- | :--- | :--- |
| `debian` | `debian` | Debian |
| `ubuntu` | `debian` | Ubuntu |
| `rhel` | `rhel` | Red Hat Enterprise Linux 系 |
| `alma`, `almalinux` | `rhel` | AlmaLinux |
| `rocky`, `rockylinux` | `rhel` | Rocky Linux |
| `centos` | `rhel` | CentOS / CentOS Stream |
| `oraclelinux`, `ol` | `rhel` | Oracle Linux |

コマンド処理プロバイダは、`OsFamily` の正規化結果と `PackageManager` の組み合わせで選択されます。

| Provider | OS family | Package manager | 未指定時の既定値 | 主な許可コマンド |
| :--- | :--- | :--- | :--- | :--- |
| `CommonDiagnosticCommandProvider` | `*` | 任意 | なし | `get_system_info`, `get_disk_usage`, `get_memory_usage`, `get_listening_ports`, `get_failed_services`, `tail_log` |
| `DebianAptCommandProvider` | `debian` | `apt` | `apt` | `pkg_check_updates`, `pkg_simulate_install`, `pkg_install`, `pkg_simulate_remove`, `pkg_remove` |
| `DebianNginxCommandProvider` | `debian` | any | systemd | `service_enable_now`, `service_reload`, `service_restart`, `service_stop`, `service_disable`, `http_get_local` |
| `RhelDnfCommandProvider` | `rhel` | `dnf` | `dnf` | `pkg_check_updates`, `pkg_simulate_install`, `pkg_install`, `pkg_simulate_remove`, `pkg_remove` |

## Commands

この章では、各コマンドに目的、構文、引数詳細、引数サンプル、処理内容、実行結果サンプル、安全上の注意を記載します。

### `kelpiemcp start`

目的:

`KelpieMCPServer` 本体プロセスの起動を要求します。コマンド自体はすぐ終了します。

構文:

```powershell
kelpiemcp start [--reload-config]
```

引数詳細:

| 引数 | 必須 | 説明 |
| :--- | :---: | :--- |
| `--reload-config` | no | 管理者が編集済み `config/kelpiemcp.json` を明示的に信頼更新対象として指定する。現在の設定内容を今回の起動で採用し、次回起動時の trust store 基準 hash として更新する。 |

引数サンプル:

MCPサーバー設定を正規に編集した後の例:

```powershell
kelpiemcp start --reload-config
```

処理内容:

起動中でなければ `KelpieMCPServer` の起動を要求します。Windows で `KelpieMCPServer` が Windows Service として登録済みの場合は Windows Service を開始します。この場合は管理者権限のターミナルから実行してください。未登録の場合は通常のローカルプロセスとして起動します。すでに起動中の場合は二重起動せず、起動中であることを返します。

MCPサーバー起動時は、`kelpiemcp.json` と SSH profile ファイルの hash を protected trust store と照合します。通常起動で `kelpiemcp.json` の hash が一致しない場合、MCPサーバーは起動失敗します。通常起動で hash が一致しない profile は load エラーになり、他の profile はロード継続します。正規に `kelpiemcp.json` を編集した場合は `--reload-config` を指定して起動します。正規に profile を編集した場合は `kelpiemcp profile reload <profile>` で信頼 baseline を更新します。trust store の復号または認証に失敗した場合、MCPサーバーは起動失敗します。起動ユーザーは `kelpiemcp.json` と全 profile に不正がないことを確認し、trust store を退避または削除して再起動します。削除した場合、次回起動時に現在の `kelpiemcp.json` と全 profile が新規 baseline として登録されます。

戻り値:

- exit code `0`: 起動要求を受け付けた。
- exit code non-zero: 起動要求、Windows Service 操作、または MCPサーバー起動時検証に失敗した。
- standard output: 起動要求、Windows Service 起動要求、または起動済み状態を表示する。
- standard error: 起動失敗、Service制御失敗、trust store 検証失敗などを表示する。

戻り値サンプル:

```json
{
  "exitCode": 0,
  "stdout": "KelpieMCPServer start requested.",
  "stderr": ""
}
```

実行結果サンプル:

```text
KelpieMCPServer start requested.
```

Windows Service 登録済みの場合:

```text
Windows Service start requested: KelpieMCPServer
```

すでに起動中の場合:

```text
KelpieMCPServer is already running.
```

安全メモ:

- `--reload-config` は、編集した MCPサーバー設定が意図した内容であり、不正変更がないことを確認してから使う。
- `kelpiemcp profile reload <profile>` は、編集した profile が意図した内容であり、不正変更がないことを確認してから使う。
- trust store を削除すると、次回起動時に現在の `kelpiemcp.json` と全 profile が信頼済み baseline として再登録される。
- 共有PC、第三者が操作可能な端末、VPS上での運用では、`kelpiemcp`、`kelpiemcp.json`、profile JSON、`mcp_trusted_store.dat` のOS権限を管理者または運用管理者グループに制限すると、より強固に守れる。

### `kelpiemcp stop`

目的:

NamedPipe 経由で起動中の `KelpieMCPServer` に停止を要求します。

構文:

```powershell
kelpiemcp stop
```

引数詳細:

- なし。

引数サンプル:

- なし。

処理内容:

NamedPipe 経由で起動中の `KelpieMCPServer` に停止要求を送信します。起動していない場合は停止操作を行いません。

実行結果サンプル:

```text
KelpieMCPServer stop requested.
```

起動していない場合:

```text
KelpieMCPServer is not running.
```

### `kelpiemcp status`

目的:

NamedPipe 経由で `KelpieMCPServer` の起動状態を確認します。Windows Service として登録されているかも表示します。

構文:

```powershell
kelpiemcp status
```

引数詳細:

- なし。

引数サンプル:

- なし。

処理内容:

NamedPipe 経由で `KelpieMCPServer` の起動状態を確認し、起動中の場合は MCP URL、Health URL、Control pipe を表示します。起動中、停止中のどちらでも Windows Service として登録されているかを表示します。

実行結果サンプル:

```text
KelpieMCPServer: running
MCP URL: http://127.0.0.1:45432/mcp
Health URL: http://127.0.0.1:45432/health
Control pipe: KelpieMCPServer.Control
Registered as Windows service: yes
```

停止中の場合:

```text
KelpieMCPServer: stopped
Registered as Windows service: yes
```

### `kelpiemcp profile add <profile>`

目的:

新しい SSH profile JSON を MCP trust store に信頼済みとして追加します。

構文:

```powershell
kelpiemcp profile add vps02
```

引数詳細:

- `profile`: `KelpieHome/profiles/<profile>.json` の `<profile>` 部分。対象ファイルは存在し、Kelpie SSH profile として読み込める必要があります。

処理内容:

`KelpieMCPServer` 起動中は、NamedPipe 経由で起動中サーバーへ要求し、profile hash を追加して in-memory catalog も再読み込みします。停止中は、`kelpiemcp` が profile を検証し、`dat/mcp_trusted_store.dat` の profile hash だけを更新します。`ProfileOperations:Add:CLI` が `Deny` の場合、この操作は拒否されます。

戻り値:

- exit code `0`: profile を信頼済みとして追加した。
- exit code non-zero: profile 名不足、profile file 不在、JSON不正、trust store 無効、`ProfileOperations:Add:CLI` が `Deny`、すでに信頼済み。
- standard output: `SshProfileTrustOperationResult` JSON。

戻り値サンプル:

```json
{
  "Success": true,
  "ProfileName": "vps02",
  "Status": "add",
  "Message": ""
}
```

実行結果サンプル:

```text
{"Success":true,"ProfileName":"vps02","Status":"add","Message":""}
```

安全メモ:

- 新規 profile の内容が意図したものか確認してから実行してください。

### `kelpiemcp profile reload <profile>`

目的:

正規に編集した SSH profile JSON を新しい信頼 baseline として受け入れます。

構文:

```powershell
kelpiemcp profile reload vps01
```

引数詳細:

- `profile`: 既に信頼済みの SSH profile 名。現在の JSON が正常に読み込める必要があります。

処理内容:

`KelpieMCPServer` 起動中は、起動中サーバーが profile を検証し、trusted hash を更新して in-memory catalog も再読み込みします。停止中は、`kelpiemcp` が profile を検証し、`dat/mcp_trusted_store.dat` を更新します。`ProfileOperations:Reload:CLI` が `Deny` の場合、この操作は拒否されます。

戻り値:

- exit code `0`: 編集済み profile を信頼済み baseline として受け入れた。
- exit code non-zero: profile 不在、未信頼、JSON不正、`ProfileOperations:Reload:CLI` が `Deny`、trust store 更新失敗。
- standard output: `SshProfileTrustOperationResult` JSON。

戻り値サンプル:

```json
{
  "Success": true,
  "ProfileName": "vps01",
  "Status": "reload",
  "Message": ""
}
```

実行結果サンプル:

```text
{"Success":true,"ProfileName":"vps01","Status":"reload","Message":""}
```

安全メモ:

- このコマンドは現在の profile file 内容を信頼済みにします。実行前に変更内容を確認してください。

### `kelpiemcp profile revoke <profile>`

目的:

指定 profile の信頼済み hash を MCP trust store から削除します。

構文:

```powershell
kelpiemcp profile revoke vps01
```

引数詳細:

- `profile`: 信頼を取り消す SSH profile 名。

処理内容:

`KelpieMCPServer` 起動中は、起動中サーバーが trusted hash を削除し、in-memory catalog を再読み込みします。停止中は、`kelpiemcp` が `dat/mcp_trusted_store.dat` から対象 profile entry を削除します。`ProfileOperations:Revoke:CLI` が `Deny` の場合、この操作は拒否されます。

戻り値:

- exit code `0`: 信頼済み entry を削除した。
- exit code non-zero: profile 名不足、trust store 無効、`ProfileOperations:Revoke:CLI` が `Deny`、対象 profile が未信頼。
- standard output: `SshProfileTrustOperationResult` JSON。

戻り値サンプル:

```json
{
  "Success": true,
  "ProfileName": "vps01",
  "Status": "revoked",
  "Message": ""
}
```

実行結果サンプル:

```text
{"Success":true,"ProfileName":"vps01","Status":"revoked","Message":""}
```

安全メモ:

- revoke 後の profile は、再度 `kelpiemcp profile add <profile>` するまで通常起動でロードされません。

### `kelpiemcp profile-capabilities [profile]`

目的:

指定 profile に対して、信頼操作の add/reload/revoke が可能か確認します。

構文:

```powershell
kelpiemcp profile-capabilities vps01
kelpiemcp profile-capabilities
```

引数詳細:

- `profile`: 省略可能。省略時は `kelpie open <profile>` で開いている profile を使用します。

処理内容:

profile file、trust store、`ProfileOperations:*:CLI` 設定を確認します。SSH target には接続しません。

戻り値:

- exit code `0`: capabilities を表示した。
- exit code non-zero: profile が指定されず、open profile もない。
- standard output: `SshProfileTrustCapabilities` JSON。
- `AddAllowed`、`ReloadAllowed`、`RevokeAllowed` は、trust store の状態と対応する `ProfileOperations:*:CLI` 設定の両方が許可する場合だけ `true` になります。

戻り値サンプル:

```json
{
  "ProfileName": "vps01",
  "AddAllowed": false,
  "ReloadAllowed": true,
  "RevokeAllowed": true,
  "Reason": ""
}
```

実行結果サンプル:

```text
{"ProfileName":"vps01","AddAllowed":false,"ReloadAllowed":true,"RevokeAllowed":true,"Reason":""}
```

安全メモ:

- ローカル読み取り専用コマンドです。profile file 本文や秘密情報は表示しません。

### `kelpiemcp service register`

目的:

`KelpieMCPServer` を Windows Service として登録します。

構文:

```powershell
kelpiemcp service register
```

引数詳細:

- なし。

引数サンプル:

- なし。

処理内容:

Windows Service 名 `KelpieMCPServer` を自動起動サービスとして登録し、サービス説明文も設定します。管理者権限のターミナルから実行してください。

実行結果サンプル:

```text
Windows Service registered: KelpieMCPServer
Binary path: "D:\Kelpie\bin\mcp\KelpieMCPServer.exe" --runtime-base "D:\Kelpie\bin"
```

### `kelpiemcp service unregister`

目的:

`KelpieMCPServer` の Windows Service 登録を解除します。

構文:

```powershell
kelpiemcp service unregister
```

引数詳細:

- なし。

引数サンプル:

- なし。

処理内容:

Windows Service 名 `KelpieMCPServer` の登録を解除します。実行中の場合は先に `Stop-Service KelpieMCPServer` で停止してください。管理者権限のターミナルから実行してください。

実行結果サンプル:

```text
Windows Service unregistered: KelpieMCPServer
```

### `kelpiemcp service status`

目的:

`KelpieMCPServer` の Windows Service 登録状態を表示します。

構文:

```powershell
kelpiemcp service status
```

引数詳細:

- なし。

引数サンプル:

- なし。

処理内容:

Windows Service 名 `KelpieMCPServer` の登録状態を確認します。

実行結果サンプル:

```text
Windows Service: registered (KelpieMCPServer)
STATE              : 1  STOPPED
```

### `kelpiemcp password <profile>`

目的:

対象プロファイルの SSH パスワードを入力し、起動中の `KelpieMCPServer` のメモリに一時保存します。保存されたパスワードはサーバープロセス終了時に消えます。

構文:

```powershell
kelpiemcp password vps01
```

引数詳細:

- `profile`: `KelpieHome/profiles/<profile>.json` の `<profile>` 部分。

引数サンプル:

- `vps01`

処理内容:

対象プロファイルの SSH パスワードを対話入力し、起動中の `KelpieMCPServer` のメモリへ一時保存します。

実行結果サンプル:

```text
Password:
SSH password stored for this KelpieMCPServer session.
```

MCP server が起動していない場合:

```text
KelpieMCPServer is not running.
```

### `kelpiemcp forget <profile>`

目的:

対象プロファイルの一時保存パスワードを、起動中の `KelpieMCPServer` のメモリから削除します。

構文:

```powershell
kelpiemcp forget vps01
```

引数詳細:

- `profile`: `KelpieHome/profiles/<profile>.json` の `<profile>` 部分。

引数サンプル:

- `vps01`

処理内容:

対象プロファイルの一時保存パスワードを、起動中の `KelpieMCPServer` のメモリから削除します。

実行結果サンプル:

```text
SSH password cleared for this KelpieMCPServer session.
```

### `kelpiemcp login <profile>`

目的:

互換用コマンドです。現在の推奨は `kelpiemcp password <profile>` です。

構文:

```powershell
kelpiemcp login vps01
```

引数詳細:

- `profile`: `kelpiemcp password <profile>` と同じ。

引数サンプル:

- `vps01`

処理内容:

互換用として `kelpiemcp password <profile>` と同等の処理を行います。

実行結果サンプル:

現在は `kelpiemcp password <profile>` と同じ形式で表示します。

### `kelpiemcp logout <profile>`

目的:

互換用コマンドです。現在の推奨は `kelpiemcp forget <profile>` です。

構文:

```powershell
kelpiemcp logout vps01
```

引数詳細:

- `profile`: `kelpiemcp forget <profile>` と同じ。

引数サンプル:

- `vps01`

処理内容:

互換用として `kelpiemcp forget <profile>` と同等の処理を行います。

実行結果サンプル:

現在は `kelpiemcp forget <profile>` と同じ形式で表示します。

### `kelpie init [--silent] [profile]`

目的:

`KelpieHome` 配下に初期ディレクトリとサンプル設定ファイルを作成します。既存ファイルは上書きしません。

構文:

```powershell
kelpie init
kelpie init vps01
kelpie init --silent
kelpie init --silent vps01
```

引数詳細:

- `profile`: 作成するプロファイル名。省略時は `sample`。
- `--silent`: 対話入力せず、既定値だけで `kelpiemcp.json` と profile ひな形を生成する。

引数サンプル:

- 省略時: `sample`
- 指定時: `vps01`

処理内容:

`KelpieHome` 配下に `config`、`profiles`、`keys` などの初期ディレクトリとサンプル設定ファイルを作成します。既存ファイルは上書きしません。
既定では、新規 `kelpiemcp.json` を作成する前に `LogDirectory`、`Server.Port`、`Server.ControlPipeName` を対話入力します。新規 profile file を作成する前には host address、port、SSH user、authentication method、private key file または password secret name、OS family、mode、allowed roots、deny pattern を対話入力します。Enter を押すと表示された既定値を使います。自動セットアップでは `--silent` を指定します。
初期化済み `KelpieHome` に profile ひな形だけを追加する場合は `kelpie profile create <profile>` を使います。

実行結果サンプル:

```text
Kelpie home: D:\Kelpie
Profile: vps01
Created directories:
  D:\Kelpie\config
  D:\Kelpie\profiles
  D:\Kelpie\keys
Created files:
  D:\Kelpie\config\kelpie.json
  D:\Kelpie\config\kelpiemcp.json
  D:\Kelpie\profiles\vps01.json
```

### `kelpie config --check`

目的:

SSH 接続を行わず、Kelpie CLI と MCP のローカル設定ファイルを検証します。
`kelpie init` 後、`config/kelpie.json` や `config/kelpiemcp.json` の編集後、SSH 側の問題を調べる前のローカル健全性確認として使います。

構文:

```powershell
kelpie config --check
kelpie config check
kelpie config check --no-pager
```

処理内容:

`config/kelpie.json` と `config/kelpiemcp.json` を読み、ファイル存在、JSON 構文、正規 `Editor` キー、MCP server 設定、runtime directory を確認します。
結果は `項目名: OK` または `項目名: NG (理由)` で表示します。複数値の項目は最初に項目名を表示し、1件ずつインデントして表示します。
最後に `Check summary: OK=<OK件数>/<check件数> NG=<NG件数>/<check件数>` を表示します。
対話 terminal では、1画面を超える長い出力を `-- more -- (Return to continue, q to quit)` でページングします。
`--no-pager` でページングを無効化できます。`--pager` でページングを要求できますが、redirect や非対話出力では停止せず全出力します。

実行結果サンプル:

```text
Kelpie config file: OK
Kelpie config JSON: OK
Editor: OK
MCP config file: OK
MCP config JSON: OK
Server: OK
Server.ControlPipeName: OK
Server.Port: OK
Directories:
  config: OK
  profiles: OK
  logs: OK
  bin: OK
  keys: OK
  dat: OK
Check summary: OK=14/14 NG=0/14
```

### `kelpie open <profile>`

目的:

ログイン対象のプロファイルを開き、現在の open profile として保存します。

構文:

```powershell
kelpie open vps01
```

引数詳細:

- `profile`: 開くプロファイル名。`KelpieHome/profiles/<profile>.json` が存在する必要があります。

引数サンプル:

- `vps01`

処理内容:

指定プロファイルの存在を確認し、現在の open profile としてランタイム状態ファイルへ保存します。

実行結果サンプル:

```text
Opened profile: vps01
Use `kelpie login` to start a session.
```

### `kelpie login`

目的:

現在 `kelpie open <profile>` で開いているプロファイルへログインします。既定モードが `cli` の場合は永続的な SSH 対話シェルを開始し、既定モードが `gui` の場合は KelpieDesktop を起動します。

構文:

```powershell
kelpie login
```

引数詳細:

- なし。対象プロファイルは事前に `kelpie open <profile>` で選択します。

引数サンプル:

- なし。

処理内容:

open 済みプロファイルへログインします。既定モードが `cli` の場合は SSH 対話シェル、`gui` の場合は KelpieDesktop を開始します。

実行結果サンプル:

```text
Connected profile: vps01
Type `exit` to close the remote shell.
kelpie:vps01> pwd
/home/deploy
kelpie:vps01> exit
Session closed: vps01
```

ポリシー違反の場合:

```text
KelpiePolicyError: command is forbidden: shutdown
```

### `kelpie login --console`

目的:

現在開いているプロファイルを使い、新しいコンソールウィンドウで `kelpie login` を起動します。

構文:

```powershell
kelpie login --console
```

引数詳細:

- `--console`: 今回だけコンソール対話として開始します。

引数サンプル:

- `--console`

処理内容:

open 済みプロファイルを使い、既定モードに関係なく新しいコンソールウィンドウで SSH 対話ログインを開始します。

実行結果サンプル:

```text
Kelpie login console started: vps01
```

### `kelpie login --desktop`

目的:

現在開いているプロファイルを使い、KelpieDesktop を起動して GUI セッションを開始します。

構文:

```powershell
kelpie login --desktop
```

引数詳細:

- `--desktop`: 今回だけ GUI セッションとして開始します。

引数サンプル:

- `--desktop`

処理内容:

open 済みプロファイルを使い、既定モードに関係なく KelpieDesktop を起動します。

実行結果サンプル:

```text
Kelpie GUI started: vps01
```

open済みプロファイルがない場合:

```text
No profile is open.
Use `kelpie open <profile>` first.
```

### `kelpie gui`

目的:

既定モードを GUI に切り替え、KelpieDesktop を起動します。以後、`kelpie login` は `--desktop` 相当として動作します。

構文:

```powershell
kelpie gui
```

引数詳細:

- なし。

引数サンプル:

- なし。

処理内容:

既定モードを GUI に切り替え、KelpieDesktop を起動します。

実行結果サンプル:

```text
Kelpie GUI started.
Kelpie mode: gui
```

### `kelpie cli`

目的:

既定モードを CLI に切り替えます。以後、`kelpie login` は現在のコンソール内で永続的な SSH 対話シェルを開始します。

構文:

```powershell
kelpie cli
```

引数詳細:

- なし。

引数サンプル:

- なし。

処理内容:

既定モードを CLI に切り替えます。

実行結果サンプル:

```text
Kelpie mode: cli
```

### `kelpie logout`

目的:

現在の対話セッションからログアウトする候補です。現時点では未実装です。

構文:

```powershell
kelpie logout
```

引数詳細:

- なし。

引数サンプル:

- なし。

処理内容:

現在の対話セッションからログアウトする候補です。現時点では未実装です。

実行結果サンプル:

```text
Session closed: ssh-a1b2c3d4e5f6
```

### `kelpie profiles`

目的:

設定済み SSH プロファイル一覧を表示します。

構文:

```powershell
kelpie profiles
```

引数詳細:

- なし。

引数サンプル:

- なし。

処理内容:

`KelpieHome/profiles` 配下の SSH プロファイル一覧を表示します。

実行結果サンプル:

```text
vps01
vps02
```

設定済みプロファイルがない場合:

```text
No SSH profiles found.
```

### `kelpie sessions`

目的:

起動中の `KelpieMCPServer` が保持している一時 SSH セッション一覧を表示します。

構文:

```powershell
kelpie sessions
```

引数詳細:

- なし。

引数サンプル:

- なし。

処理内容:

起動中の `KelpieMCPServer` が保持している一時 SSH セッション一覧を表示します。

実行結果サンプル:

```text
Sessions:
ssh-a1b2c3d4e5f6  vps01  password  2026-06-05 01:02:03Z
```

セッションがない場合:

```text
Sessions:
(none)
```

### `kelpie kill <handle>`

目的:

起動中の `KelpieMCPServer` が保持している一時 SSH セッションを、`kelpie sessions` で表示された handle で終了します。

構文:

```powershell
kelpie kill ssh-a1b2c3d4e5f6
```

引数詳細:

- `handle`: `kelpie sessions` に表示されたセッションハンドル。

引数サンプル:

- `ssh-a1b2c3d4e5f6`

処理内容:

指定 handle に対応する一時 SSH セッションを終了します。

実行結果サンプル:

```text
SSH session killed: ssh-a1b2c3d4e5f6
```

存在しないセッションを指定した場合:

```text
SSH session was not found: ssh-missing
```

### `kelpie profile create <profile>`

目的:

初期化済み `KelpieHome` に、新しい SSH profile ひな形を1つ作成します。コマンドは対話形式でひな形の値を尋ねます。Enter を押すと表示された既定値を使います。

構文:

```powershell
kelpie profile create vps02
kelpie profile create vps02 --silent
kelpie profile create vps02 --silent --host-address: demo
kelpie profile create vps02 --dry-run --host-address: demo
kelpie profile create vps02 --no-backup
```

引数詳細:

- `profile`: 作成する単一プロファイル名。`KelpieHome/profiles/<profile>.json` として作成されます。wildcard、パス区切り文字、ファイル名に使えない文字は拒否します。
- `--silent`: prompt を出さず、既定値または指定された template option だけで profile を作成します。
- `--host-address <value>`: `Host.Address` を上書きします。`--host-address: <value>` 形式も受け付けます。
- `--port <value>`: `Host.Port` を上書きします。`1` から `65535` の整数です。
- `--ssh-user <value>`: `DefaultUser` を上書きします。
- `--auth-method <privateKey|password>`: `Auth.Method` を上書きします。
- `--private-key-file <value>`: `Auth.PrivateKeyFile` を上書きします。`privateKey` 認証時に使います。
- `--password-secret-name <value>`: `Auth.PasswordSecretName` を上書きします。`password` 認証時に使います。
- `--os-family <value>`: `Platform.OsFamily` を上書きします。
- `--mode <ReadOnly|Safe|Maintenance|Expert>`: 生成する default user の `Mode` を上書きします。
- `--read-only-root <value>`: 生成する read-only root を上書きします。複数回指定できます。`-` で空リストにできます。
- `--read-write-root <value>`: 生成する read-write root を上書きします。複数回指定できます。`-` で空リストにできます。
- `--allowed-root <key=value[;...]>`: 生成する `AllowedRoots` map entry を上書きします。複数回指定できます。`ReadOnly` / `ReadWrite` は `$ReadOnly` / `$ReadWrite` に正規化し、`$Write` などの値はそのまま保持します。
- `--deny-pattern <value>`: 生成する deny pattern を上書きします。複数回指定できます。`-` で空リストにできます。
- `--special-path <key=value[;...]>`: 生成する `SpecialPaths` map entry を上書きします。複数回指定できます。`deny` / `confirm` / `allow` は `Deny` / `Confirm` / `Allow` に正規化します。
- `--dry-run`: 作成・上書き予定の profile path、backup 計画、生成 JSON を表示し、ファイルを変更しません。
- `--no-backup`: 既存 profile を上書きする場合に `.kelpie` backup を作成せず、即コミットとして書き込みます。

処理内容:

`kelpie init` 済みの `KelpieHome` を前提に、`profiles/<profile>.json` だけを新規作成します。`config/kelpie.json`、`config/kelpiemcp.json`、ディレクトリ、trust store、open profile 状態は作成・更新しません。既に同名 profile がある場合は上書き確認を行い、上書き時は旧ファイルを `profiles/<profile>.json.kelpie` として保存します。既に `.kelpie` backup がある場合は、先に `kelpie profile commit <profile>` または `kelpie profile rollback <profile>` を実行するよう案内して失敗します。`--no-backup` 指定時は、上書き時も `.kelpie` backup を作成せず、`Commit profile? [Y/n]:` も尋ねません。`--silent` 指定時は template 値の prompt を出さず、既定値 `Host.Address = localhost`、`Host.Port = 22`、`DefaultUser = deploy`、private-key auth、`Mode = Safe`、`Platform.OsFamily = debian`、read-only root `/var/log`、read-write root `/var/www`、deny pattern `**/.env` で作成します。`--dry-run` 指定時は prompt を出さず、作成先 profile path、backup 計画、生成 JSON を表示し、ファイルは書き込みません。dry-run では template option を `--silent` なしでも指定できます。silent template option は `<profile>` の前後どちらにも指定でき、`--name value`、`--name=value`、`--name: value` 形式を受け付けます。`--allowed-root` と `--special-path` は `;` 区切りの map entry を受け付けます。`;` を含む値は quote してください。PowerShell で `$` を含む場合は、`--allowed-root '/srv/www=$ReadWrite;/tmp=$Write'` のように single quote を推奨します。`--allowed-root` を指定すると既定 allowed-root map は置き換わります。ただし `--read-only-root` / `--read-write-root` も指定した場合は併用されます。`--special-path` を指定すると既定 special-path map は置き換わります。ただし `--deny-pattern` も指定した場合は併用されます。

host address、port、SSH user、authentication method、private key file または password secret name、OS family、mode、allowed roots、deny pattern を対話入力します。password authentication の場合も入力するのは `PasswordSecretName` だけで、パスワード実値は入力・保存しません。optional な allowed-root / deny-pattern prompt では1行に1 pattern を入力できます。空 Enter または `-` で、その prompt を省略または終了します。既存 profile を上書きした場合は最後に `Commit profile? [Y/n]:` を尋ね、`Y` なら `.kelpie` backup を削除し、`n` なら後で commit / rollback できるよう backup を残します。

MCPサーバーの protected trust store へ反映する場合は、作成した profile 内容を確認した後に `kelpiemcp profile add <profile>` を実行します。

戻り値:

- exit code `0`: profile ひな形を作成した。
- exit code non-zero: profile 名不足、profile 名不正、`KelpieHome` 未初期化、上書き拒否、pending backup 既存、またはファイル作成失敗。
- standard output: 作成した profile 名とファイルパス。
- standard error: 検証エラーまたはファイル操作エラー。

実行結果サンプル:

```text
Create SSH profile template.
Press Enter to use the default value.
Host address [localhost]:
Port [22]:
SSH user [deploy]:
Authentication method (privateKey/password) [privateKey]:
Private key file [vps02_ed25519]:
OS family [debian]:
Mode (ReadOnly/Safe/Maintenance/Expert) [Safe]:
Read-only root [Returnで続行]: /var/log/nginx
Read-only root [Returnで続行]:
Read-write root [Returnで続行]:
Deny pattern [Returnで続行]: **/.secret
Deny pattern [Returnで続行]:
Created profile: vps02
Profile file: D:\Kelpie\profiles\vps02.json
```

silent 実行結果サンプル:

```text
kelpie profile create demo --silent --host-address: demo
Created profile: demo
Profile file: D:\Kelpie\profiles\demo.json
```

dry-run 実行結果サンプル:

```text
kelpie profile create demo --dry-run --host-address: demo
Dry run: profile create
Would create profile: demo
Profile file: D:\Kelpie\profiles\demo.json
Would write:
{
  "Host": {
    "Address": "demo",
    "Port": 22
  }
}
No files were changed.
```

silent map 指定サンプル:

```powershell
kelpie profile create demo --silent `
  --allowed-root '/srv/www=$ReadWrite;/tmp=$Write' `
  --special-path '**/.env=Deny;**/.tmp=Allow'
```

既に存在する場合:

```text
Profile already exists: vps02. Overwrite? [Y/n]: Y
...
Commit profile? [Y/n]: n
Profile backup is pending: D:\Kelpie\profiles\vps02.json.kelpie
Run `kelpie profile commit vps02` or `kelpie profile rollback vps02`.
```

### `kelpie profile edit <profile>`

目的:

既存の SSH profile JSON を編集します。操作を指定しない場合は設定済みエディタで profile を開き、エディタ終了後に再パースと検証を行います。

構文:

```powershell
kelpie profile edit vps02
kelpie profile edit vps02 set Host.Port 2224
kelpie profile edit vps02 set Users.kelpie.Mode "Maintenance|WebUser|WebAdmin"
kelpie profile edit vps02 add-root /etc/nginx ReadWrite
kelpie profile edit vps02 rm-root /etc/nginx
kelpie profile edit vps02 add-deny "**/.htpasswd"
kelpie profile edit vps02 rm-deny "**/.htpasswd"
kelpie profile edit vps02 set Host.Port 2222 --no-backup
kelpie profile edit vps02 set Host.Port 2222 --dry-run
kelpie profile delete vps02
kelpie profile delete "vps-*"
kelpie profile clean vps02
kelpie profile commit vps02
kelpie profile rollback vps02
```

引数詳細:

- `profile`: 編集する profile 名。現在の `KelpieHome/profiles` から解決します。
- `dotPath`: `set` で更新する scalar path。許可値は `Host.Address`、`Host.Port`、`Auth.Method`、`Auth.PrivateKeyFile`、`Auth.PasswordSecretName`、`DefaultUser`、`Users.<user>.Mode`、`Platform.OsFamily`、`Platform.PackageManager` です。
- `value`: `set` の新しい値。`Host.Port` は `1` から `65535` の整数です。
- `path`: `add-root` / `rm-root` の allowed root path または glob です。
- `access`: `add-root` の権限。`ReadOnly`、`ReadWrite`、`$ReadOnly`、`$ReadWrite` を受け付け、`$...` 形式へ正規化します。
- `pattern`: `add-deny` / `rm-deny` の special path glob です。`**/.htpasswd` のように dot を含む pattern も扱えます。
- `--no-backup`: `.kelpie` backup を作成せず、編集結果を即コミットとして書き込みます。
- `--dry-run`: 明示的な編集操作を検証し、書き込み予定の JSON を表示します。ファイルは変更しません。

処理内容:

- `set` は scalar path のみを更新します。object、dictionary、array に相当する path は拒否し、`add-root` / `rm-root` / `add-deny` / `rm-deny` の利用を案内します。
- `profile edit` は単一 profile 名だけを受け入れます。wildcard は拒否します。
- `add-root`、`rm-root`、`add-deny`、`rm-deny` は `Users.<DefaultUser>` が object の場合はその user-level 設定を編集し、それ以外は profile 直下の設定を編集します。
- 既存 profile を変更する前に、現在のファイルを `profiles/<profile>.json.kelpie` として保存します。既に backup がある場合は、commit または rollback するまで編集を拒否します。
- 編集成功後は `Commit profile? [Y/n]:` を尋ねます。`Y` は backup を削除し、`n` は後で `kelpie profile commit <profile>` または `kelpie profile rollback <profile>` できるよう backup を残します。
- `--no-backup` 指定時は `.kelpie` backup を作成せず、`Commit profile? [Y/n]:` も尋ねません。
- `--dry-run` 指定時は `set`、`add-root`、`rm-root`、`add-deny`、`rm-deny` の編集を一時ファイル上で検証し、書き込み予定 JSON を表示します。backup 作成、profile 書き換え、commit prompt は行いません。エディタモード `kelpie profile edit <profile>` では `--dry-run` をサポートせず、明示的な編集操作の利用を案内します。
- `kelpie profile commit <profile-pattern>` は pending `.kelpie` backup を削除し、削除 pending を含む現在の profile JSON 状態を確定扱いにします。
- `kelpie profile rollback <profile-pattern>` は `.kelpie` backup を現在の profile JSON へ戻します。削除 pending の場合は削除済み profile file を復元します。一致する backup がない場合はエラーです。
- 非エディタ操作では、書き込み前に profile 全体を既存 loader/parser で再検証します。検証に失敗した場合は書き込みません。
- 非エディタ操作の書き込みは temp file からの置換で行い、UTF-8 BOMなし、LF 改行で保存します。
- エディタは `config/kelpie.json` の `Editor`、`KELPIE_EDITOR`、`VISUAL`、`EDITOR`、OS既定（Windows は `notepad`、Unix は `vi`）の順に解決します。
- `config/kelpie.json` に旧小文字 `editor` が残っている場合、`kelpie` コマンドは実行ごとに標準出力へ `Editor` へのリネームを促す warning を表示します。
- editor command alias の `vscode` は VS Code `code` CLI として解釈します。Windows では可能な場合に `PATH` / `PATHEXT` から `code` を解決するため、`"Editor": "vscode --wait"` でインストール済みの `code.cmd` を実パス決め打ちなしに使えます。
- special value の `default` は大文字小文字を区別せず、`.json` に関連付けられたアプリで profile file を開きます。`Notepad` も大文字小文字を区別せず Windows Notepad を起動します。
- エディタ起動は終了待ちします。`code` など即時終了するエディタは `"Editor": "code --wait"` のように待機オプション付きで設定します。
- エディタ終了後の検証に失敗した場合は、再編集または中止を選べます。中止すると元内容へ戻します。
- エディタモードは対話コンソール専用です。標準入力リダイレクト中はエラーにします。

戻り値:

- exit code `0`: profile を更新し、検証に成功した。
- exit code non-zero: profile 不存在、pending backup 既存、path または値の不正、profile 検証失敗、エディタ起動失敗、または非対話でのエディタモード実行。
- standard output: 更新した profile 名と解決済み profile file path。
- standard error: 検証エラーまたはエディタエラー。
- 秘密鍵、パスフレーズ、パスワード実値は表示しません。

実行結果サンプル:

```text
Updated profile: vps02
Profile file: D:\Kelpie\profiles\vps02.json
Commit profile? [Y/n]:
```

dry-run 実行結果サンプル:

```text
kelpie profile edit vps02 set Host.Port 2222 --dry-run
Dry run: profile edit
Would update profile: vps02
Profile file: D:\Kelpie\profiles\vps02.json
Would create backup: D:\Kelpie\profiles\vps02.json.kelpie
Would write:
{
  "Host": {
    "Address": "localhost",
    "Port": 2222
  }
}
No files were changed.
```

profile が存在しない場合:

```text
SSH profile was not found: vps02
Use `kelpie profile create vps02` to create it.
```

### `kelpie profile delete <profile-pattern>`

目的:

既存 SSH profile を、profile create/edit と同じ `.kelpie` transaction 方式で1件または複数件削除します。

構文:

```powershell
kelpie profile delete vps02
kelpie profile delete "vps-*"
kelpie profile delete "vps-*" --no-backup
kelpie profile delete "vps-*" --dry-run
```

引数詳細:

| 引数 | 必須 | 説明 |
| :--- | :---: | :--- |
| `<profile-pattern>` | yes | SSH profile 名、または wildcard pattern。`*` は0文字以上、`?` は1文字に一致します。path separator と `*` / `?` 以外の不正ファイル名文字は拒否します。 |
| `--no-backup` | no | `.kelpie` backup を作成せず、一致 profile の削除を即コミットとして扱います。 |
| `--dry-run` | no | 一致 profile、backup 計画、削除計画を表示し、ファイルは変更しません。 |

処理内容:

- wildcard を含まない場合は、既存の `profiles/<profile>.json` が必要です。
- wildcard を含む場合は、設定済み Kelpie home の `profiles/*.json` の file name に対して一致する profile を解決します。コマンドは確認前に一致した profile 名を表示します。
- 一致対象に `.kelpie` backup が既にある場合は warning を表示し、その profile を skip します。pending backup がない他の一致 profile は削除できます。
- 単一 profile の場合は、ファイル変更前に `Delete profile: <profile>? [Y/n]:` を尋ねます。
- wildcard pattern の場合は、ファイル変更前に ``Delete <count> profiles matching `<profile-pattern>`? [Y/n]:`` を尋ねます。
- 確認後、現在の各 profile を `profiles/<profile>.json.kelpie` として保存し、`profiles/<profile>.json` を削除します。
- 最後に、単一 profile では `Commit profile? [Y/n]:`、複数 wildcard match では `Commit profiles? [Y/n]:` を尋ねます。`Y` は backup を削除して削除を確定します。`n` は後で `kelpie profile rollback <profile>` で削除済み profile を復元、または `kelpie profile commit <profile>` で削除を確定できるよう backup を残します。
- `--no-backup` 指定時は `.kelpie` backup を作成せず、削除後の commit 確認も行いません。
- `--dry-run` 指定時は確認 prompt を出さず、backup 作成も profile 削除も行いません。

戻り値:

- profile 削除 transaction の作成またはユーザーによるキャンセル時は exit code `0`。
- profile file と pending backup のどちらにも一致がない、profile pattern が不正、backup または削除に失敗した場合は non-zero exit code。
- 標準出力には一致した profile 名、削除対象 profile 名、file path、pending transaction の案内を出します。
- 標準エラーには検証エラーとファイルシステムエラーを出します。

実行結果サンプル:

```text
Delete profile: vps02? [Y/n]: Y
Deleted profile: vps02
Profile file: D:\Kelpie\profiles\vps02.json
Commit profile? [Y/n]: n
Profile backup is pending: D:\Kelpie\profiles\vps02.json.kelpie
Run `kelpie profile commit vps02` or `kelpie profile rollback vps02`.
```

wildcard 実行結果サンプル:

```text
Matched profiles: 2
  vps-alpha
  vps-beta
Delete 2 profiles matching `vps-*`? [Y/n]: Y
Deleted profiles: 2
  vps-alpha: D:\Kelpie\profiles\vps-alpha.json
  vps-beta: D:\Kelpie\profiles\vps-beta.json
Commit profiles? [Y/n]: n
Profile backups are pending:
  D:\Kelpie\profiles\vps-alpha.json.kelpie
  D:\Kelpie\profiles\vps-beta.json.kelpie
Run `kelpie profile commit <profile>` or `kelpie profile rollback <profile>` for each pending profile.
```

### `kelpie profile clean <profile-pattern>`

目的:

profile file と pending `.kelpie` backup file をまとめて削除します。これは即時 cleanup コマンドであり、新しい backup は作成しません。cleanup 後の profile は `kelpie profile rollback` では復元できません。

構文:

```powershell
kelpie profile clean vps02
kelpie profile clean "vps-*"
kelpie profile clean "vps-*" --dry-run
```

引数詳細:

| 引数 | 必須 | 説明 |
| :--- | :---: | :--- |
| `<profile-pattern>` | yes | SSH profile 名、または wildcard pattern。`*` は0文字以上、`?` は1文字に一致します。path separator と `*` / `?` 以外の不正ファイル名文字は拒否します。 |
| `--dry-run` | no | 削除予定の profile file と backup file を表示し、ファイルは変更しません。 |

処理内容:

- wildcard を含まない場合は、存在する `profiles/<profile>.json` と `profiles/<profile>.json.kelpie` を削除します。
- wildcard を含む場合は、設定済み Kelpie home の `profiles/*.json` と `profiles/*.json.kelpie` の file name に対して一致する profile を重複なしで解決します。
- コマンドは確認前に一致した profile 名を表示します。
- 単一 profile の場合は、ファイル変更前に `Clean profile and backup: <profile>? [Y/n]:` を尋ねます。
- wildcard pattern の場合は、ファイル変更前に ``Clean <count> profiles and backups matching `<profile-pattern>`? [Y/n]:`` を尋ねます。
- 確認後、一致 profile の JSON file と `.kelpie` backup file を、存在するものだけ削除します。
- `--dry-run` 指定時は確認 prompt を出さず、profile file も backup file も削除しません。

戻り値:

- cleanup 実行またはユーザーによるキャンセル時は exit code `0`。
- profile file と pending backup のどちらにも一致がない、profile pattern が不正、または削除に失敗した場合は non-zero exit code。
- 標準出力には一致した profile 名、cleanup 対象 profile 名、file path を出します。
- 標準エラーには検証エラーとファイルシステムエラーを出します。

実行結果サンプル:

```text
Clean profile and backup: vps02? [Y/n]: Y
Cleaned profile: vps02
Removed profile file: D:\Kelpie\profiles\vps02.json
Removed backup: D:\Kelpie\profiles\vps02.json.kelpie
```

wildcard 実行結果サンプル:

```text
Matched profiles: 2
  vps-alpha
  vps-beta
Clean 2 profiles and backups matching `vps-*`? [Y/n]: Y
Cleaned profiles: 2
  vps-alpha: D:\Kelpie\profiles\vps-alpha.json
  vps-beta: D:\Kelpie\profiles\vps-beta.json
```

### `kelpie profile commit <profile-pattern>`

目的:

pending `.kelpie` backup を削除し、profile create/edit/delete の pending transaction を確定扱いにします。

構文:

```powershell
kelpie profile commit vps02
kelpie profile commit "vps-*"
kelpie profile commit "vps-*" --dry-run
```

引数詳細:

| 引数 | 必須 | 説明 |
| :--- | :---: | :--- |
| `<profile-pattern>` | yes | SSH profile 名、または wildcard pattern。`*` は0文字以上、`?` は1文字に一致します。path separator と `*` / `?` 以外の不正ファイル名文字は拒否します。 |
| `--dry-run` | no | 削除予定の backup file を表示し、ファイルは変更しません。 |

処理内容:

- wildcard を含まない場合は `profiles/<profile>.json.kelpie` が必要です。
- wildcard を含む場合は `profiles/*.json.kelpie` から一致する pending backup を解決します。
- `--dry-run` 指定時は確認 prompt を出さず、backup file を削除しません。
- `--dry-run` なしの場合、単一 profile は即時 backup を削除し、wildcard は確認後に一致 backup を削除します。

### `kelpie profile rollback <profile-pattern>`

目的:

pending `.kelpie` backup を profile JSON path へ戻し、profile create/edit/delete の pending transaction を取り消します。

構文:

```powershell
kelpie profile rollback vps02
kelpie profile rollback "vps-*"
kelpie profile rollback "vps-*" --dry-run
```

引数詳細:

| 引数 | 必須 | 説明 |
| :--- | :---: | :--- |
| `<profile-pattern>` | yes | SSH profile 名、または wildcard pattern。`*` は0文字以上、`?` は1文字に一致します。path separator と `*` / `?` 以外の不正ファイル名文字は拒否します。 |
| `--dry-run` | no | 復元予定の backup file と書き込み先 profile file を表示し、ファイルは変更しません。 |

処理内容:

- wildcard を含まない場合は `profiles/<profile>.json.kelpie` が必要です。
- wildcard を含む場合は `profiles/*.json.kelpie` から一致する pending backup を解決します。
- `--dry-run` 指定時は確認 prompt を出さず、profile file の復元も backup file の削除も行いません。
- `--dry-run` なしの場合、単一 profile は即時復元し、wildcard は確認後に一致 backup を復元します。

### `kelpie profile check <profile>`

目的:

SSH 接続を行わず、単一 SSH profile file を検証します。wildcard は対応しません。
`kelpie open` の前、profile 編集後、MCP profile baseline の信頼追加や再読み込み前の確認に使います。

構文:

```powershell
kelpie profile check vps01
kelpie profile check vps01 --no-pager
```

処理内容:

`profiles/<profile>.json` を読み、ファイル存在、JSON 構文、profile schema、接続項目、認証参照、command provider 対応、policy list、user、pending `.kelpie` backup を確認します。
結果は `項目名: OK` または `項目名: NG (理由)` で表示します。複数値の項目は `kelpie profile show` と同様に、最初に項目名を表示し、1件ずつインデントして表示します。空リストは `(empty list): OK` と表示します。
最後に `Check summary: OK=<OK件数>/<check件数> NG=<NG件数>/<check件数>` を表示します。
対話 terminal では、1画面を超える長い出力を `-- more -- (Return to continue, q to quit)` でページングします。
`--no-pager` でページングを無効化できます。`--pager` でページングを要求できますが、redirect や非対話出力では停止せず全出力します。
`User` または `Users` に直接 `root` login がある場合は NG です。private-key 認証では、解決後の秘密鍵ファイルが存在するかを確認します。

実行結果サンプル:

```text
Profile file: OK
Profile JSON: OK
Profile schema: OK
Host.Address: OK
Host.Port: OK
User: OK
Auth.Method: OK
Auth.PrivateKeyFile: OK
Platform.OsFamily: OK
Platform.PackageManager: OK
Mode: OK
Command providers:
  DebianDiagnosticCommandProvider: OK
Capabilities:
  (empty list): OK
Roles:
  Safe: OK
Allowed roots:
  /var/www: OK
Special paths:
  **/.env: OK
Users:
  deploy: OK
Pending backup: OK
Check summary: OK=18/18 NG=0/18
```

### `kelpie profile show <profile-pattern>`

目的:

対象プロファイルの概要を表示します。秘密鍵パスやパスワードそのものは表示しません。

構文:

```powershell
kelpie profile show vps01
kelpie profile show vps01 --no-pager
```

引数詳細:

- `profile`: 表示するプロファイル名。

引数サンプル:

- `vps01`

処理内容:

対象プロファイルを読み込み、接続先、OS family、command provider、mode、認証方式などの概要を表示します。
`Command providers`、`Capabilities`、`Roles`、`Allowed roots`、`Special paths`、`Services`、`Users` などの複数値項目は、インデント付きで1行に1件ずつ表示します。
空の複数値項目は `(empty list)` と表示します。マップ形式の複数値項目では key と value の間に `=>` を表示し、key 列の幅を揃えて value 列を見やすくします。
対話 terminal では、1画面を超える長い出力を `-- more -- (Return to continue, q to quit)` でページングします。
`--no-pager` でページングを無効化できます。`--pager` でページングを要求できますが、redirect や非対話出力では停止せず全出力します。

実行結果サンプル:

```text
Profile: vps01
Host: example.invalid
Port: 22
User: deploy
OS family: alma
Package manager: dnf
Command OS family: rhel
Command providers:
  CommonDiagnosticCommandProvider
  RhelDnfCommandProvider
Capabilities:
  AllowListPackage
Roles:
  Safe
Effective mode: Safe
Allowed roots:
  /var/www  => @Read|@List|@CD|@Write
Special paths:
  **/.env  => Deny
Services:
  (empty list)
Users:
  deploy  => Safe
Authentication: privateKey
Private key: (configured)
```

存在しないプロファイルを指定した場合:

```text
SSH profile was not found: missing
```

### `kelpie status <profile>`

目的:

対象プロファイルの概要と、`KelpieMCPServer` の起動状態を表示します。

構文:

```powershell
kelpie status vps01
```

引数詳細:

- `profile`: 状態を表示するプロファイル名。

引数サンプル:

- `vps01`

処理内容:

対象プロファイルの概要と、`KelpieMCPServer` の起動状態をまとめて表示します。

実行結果サンプル:

```text
Profile: vps01
Host: example.invalid
Port: 22
User: deploy
OS family: alma
Package manager: dnf
Command OS family: rhel
Command providers:
  CommonDiagnosticCommandProvider
  RhelDnfCommandProvider
Capabilities:
  AllowListPackage
Roles:
  Safe
Effective mode: Safe
Allowed roots:
  /var/www  => @Read|@List|@CD|@Write
Special paths:
  **/.env  => Deny
Services:
  (empty list)
Users:
  deploy  => Safe
Authentication: privateKey

KelpieMCPServer: running
MCP URL: http://127.0.0.1:45432/mcp
Health URL: http://127.0.0.1:45432/health
Control pipe: KelpieMCPServer.Control
Registered as Windows service: yes
```

### `kelpie diag <profile>`

目的:

対象プロファイルに対して、診断系 SSH コマンドをまとめて実行します。

構文:

```powershell
kelpie diag vps01
```

引数詳細:

- `profile`: 診断対象のプロファイル名。

引数サンプル:

- `vps01`

処理内容:

対象プロファイルに対して、許可済み診断コマンドをまとめて SSH 実行します。
password profile の場合、CLI は最初に1回だけパスワードを尋ね、現在の `kelpie diag` プロセス内で各診断コマンドに使い回します。
SSH接続失敗は raw stack trace ではなく、短い standard error メッセージとして表示します。

実行結果サンプル:

```text
# get_system_info
Linux example 5.14.0-000.el9.x86_64 #1 SMP PREEMPT_DYNAMIC x86_64 GNU/Linux
# get_disk_usage
Filesystem      Size  Used Avail Use% Mounted on
/dev/vda2        40G  9.2G   31G  24% /
# get_memory_usage
               total        used        free      shared  buff/cache   available
Mem:            1780         420         190          12        1170        1180
# get_listening_ports
Netid State  Recv-Q Send-Q Local Address:Port Peer Address:Port
tcp   LISTEN 0      128          0.0.0.0:22        0.0.0.0:*
# get_failed_services
0 loaded units listed.
```

### `kelpie logs <profile> <service> [lines]`

目的:

対象サービスのログを SSH 経由で取得します。`lines` 省略時は `100` 行を取得します。

構文:

```powershell
kelpie logs vps01 nginx.service
kelpie logs vps01 nginx.service 200
```

引数詳細:

- `profile`: ログ取得対象のプロファイル名。
- `service`: systemd サービス名。例: `nginx.service`。
- `lines`: 取得行数。省略時は `100`。

引数サンプル:

- `profile`: `vps01`
- `service`: `nginx.service`
- `lines`: `200`

処理内容:

対象プロファイルで `tail_log` 相当の許可済み SSH コマンドを実行し、指定 service のログを取得します。
password profile の場合、CLI は現在の `kelpie logs` プロセス用にパスワードを尋ねます。
SSH接続失敗は raw stack trace ではなく、短い standard error メッセージとして表示します。

実行結果サンプル:

```text
# tail_log
Jun 05 06:20:01 example nginx[1234]: start worker process 1235
Jun 05 06:21:14 example nginx[1235]: client 203.0.113.10 closed keepalive connection
```

サービス名に危険な文字が含まれる場合:

```text
SSH command argument contains a dangerous fragment: service
```

### `kelpie env keys <profile>`

目的:

対象 profile の SSH user から見える remote 環境変数名を一覧表示します。

構文:

```powershell
kelpie env keys vps01
```

引数詳細:

- `profile`: 環境変数名を取得するプロファイル名。

引数サンプル:

- `profile`: `vps01`

処理内容:

profile の `Capabilities` に `AllowPeekEnvironmentKeys` がある場合だけ実行できます。
`EnvironmentValues` で `Hidden` にした key は出力から除外します。
このコマンドは値を表示しません。

実行結果サンプル:

```text
HOME
LANG
PATH
SHELL
```

### `kelpie env peek <profile> <key>`

目的:

profile が許可した remote 環境変数値を1つ参照します。

構文:

```powershell
kelpie env peek vps01 PATH
```

引数詳細:

- `profile`: 環境変数値を参照するプロファイル名。
- `key`: 参照する環境変数名。

引数サンプル:

- `profile`: `vps01`
- `key`: `PATH`

処理内容:

profile の `Capabilities` に `AllowPeekEnvironmentValues` が必要です。
さらに、対象 key が `EnvironmentValues` で `PeekCommon`、`PeekSecret`、または `Masked` として許可されている必要があります。
`Masked` の場合は実値を返さず、masked value と長さだけを表示します。
`Hidden` と未定義 key の値は参照できません。

実行結果サンプル:

```text
/usr/local/bin:/usr/bin:/bin
```

masked の場合:

```text
************ (length=12)
```

### `kelpie env set <profile> <key> <value> -- <command>`

目的:

1回の command execution にだけ remote 環境変数値を付与して実行します。remote host に新しい値を永続保存しません。
実行前に `~/.kelpie/.env` が存在する場合は source してから、指定した `<key> <value>` で上書きします。

構文:

```powershell
kelpie env set vps01 APP_ENV production -- printenv APP_ENV
```

引数詳細:

- `profile`: 環境変数を一時設定して command を実行するプロファイル名。
- `key`: 設定する環境変数名。
- `value`: 1回の command execution に付与する値。
- `command`: `--` 以降に書く実行コマンド。

引数サンプル:

- `profile`: `vps01`
- `key`: `APP_ENV`
- `value`: `production`
- `command`: `printenv APP_ENV`

処理内容:

profile の `Capabilities` に `AllowSetEnvironmentValues` が必要です。
さらに、対象 key が `EnvironmentValues` で `SetCommon` または `SetSecret` として許可されている必要があります。
`--` 以降の command は `kelpie login` の対話コマンドと同じ raw-command policy で検査されます。
環境変数値を公開ログや issue に貼り付けないでください。

実行結果サンプル:

```text
production
```

### `kelpie env list <profile>`

目的:

remote の Kelpie env file に保存されている環境変数名を一覧表示します。

構文:

```powershell
kelpie env list vps01
```

引数詳細:

- `profile`: 永続 env file を確認するプロファイル名。

引数サンプル:

- `profile`: `vps01`

処理内容:

対象 remote user の `~/.kelpie/.env` から環境変数名だけを抽出します。
profile の `Capabilities` に `AllowPeekEnvironmentKeys` が必要です。
`EnvironmentValues` で `Hidden` にした key は出力から除外します。
値は表示しません。

実行結果サンプル:

```text
APP_ENV
PATH
```

### `kelpie env persist <profile> <key> <value>`

目的:

remote の Kelpie env file に環境変数値を保存します。

構文:

```powershell
kelpie env persist vps01 APP_ENV production
```

引数詳細:

- `profile`: 環境変数を保存するプロファイル名。
- `key`: 保存する環境変数名。
- `value`: 保存する値。

引数サンプル:

- `profile`: `vps01`
- `key`: `APP_ENV`
- `value`: `production`

処理内容:

対象 remote user の `~/.kelpie/.env` を更新します。
書き込み前に `~/.kelpie/.env.20260617T120000Z.kelpie` のような timestamp 付き backup を作成します。
profile の `Capabilities` に `AllowSetEnvironmentValues` が必要です。
さらに、対象 key が `EnvironmentValues` で `SetCommon` または `SetSecret` として許可されている必要があります。
既存プロセスには自動反映されません。cron、shell、Kelpie 実行などが次回 source した時点で反映されます。

実行結果サンプル:

```text
Updated ~/.kelpie/.env
Backup: ~/.kelpie/.env.20260617T120000Z.kelpie
```

### `kelpie env remove <profile> <key>`

目的:

remote の Kelpie env file から環境変数を削除します。

構文:

```powershell
kelpie env remove vps01 APP_ENV
```

引数詳細:

- `profile`: 環境変数を削除するプロファイル名。
- `key`: 削除する環境変数名。

引数サンプル:

- `profile`: `vps01`
- `key`: `APP_ENV`

処理内容:

対象 remote user の `~/.kelpie/.env` から指定 key の行を削除します。
書き込み前に timestamp 付き `.kelpie` backup を作成します。
profile の `Capabilities` に `AllowSetEnvironmentValues` が必要です。
さらに、対象 key が `EnvironmentValues` で `SetCommon` または `SetSecret` として許可されている必要があります。

実行結果サンプル:

```text
Removed from ~/.kelpie/.env
Backup: ~/.kelpie/.env.20260617T120000Z.kelpie
```

### `kelpie version`

目的:

バージョン情報を表示します。

構文:

```powershell
kelpie version
kelpie --version
kelpie -v
```

引数詳細:

- なし。`--version` / `-v` は別名です。

引数サンプル:

- なし。

処理内容:

`kelpie` のバージョン情報を表示します。

実行結果サンプル:

```text
kelpie 0.3.1.0
```

### `kelpie help`

目的:

コマンドヘルプを表示します。

構文:

```powershell
kelpie help
kelpie --help
kelpie -h
```

引数詳細:

- なし。`--help` / `-h` は別名です。

引数サンプル:

- なし。

処理内容:

利用可能な `kelpie` コマンドと option を表示します。

実行結果サンプル:

```text
Usage:
  kelpie init [--silent] [profile]
  kelpie open <profile>
  kelpie gui
  kelpie cli
  kelpie login
  kelpie login --console
  kelpie login --desktop
  kelpie logout
  kelpie profiles
  kelpie sessions
  kelpie kill <handle>
  kelpie profile create <profile> [--silent] [--no-backup] [--dry-run] [options]
  kelpie profile edit <profile> [--no-backup]
  kelpie profile edit <profile> set <dotPath> <value> [--no-backup] [--dry-run]
  kelpie profile edit <profile> add-root <path> <access> [--no-backup] [--dry-run]
  kelpie profile edit <profile> rm-root <path> [--no-backup] [--dry-run]
  kelpie profile edit <profile> add-deny <pattern> [--no-backup] [--dry-run]
  kelpie profile edit <profile> rm-deny <pattern> [--no-backup] [--dry-run]
  kelpie profile delete <profile-pattern> [--no-backup] [--dry-run]
  kelpie profile clean <profile-pattern> [--dry-run]
  kelpie profile commit <profile-pattern> [--dry-run]
  kelpie profile rollback <profile-pattern> [--dry-run]
  kelpie profile check <profile>
  kelpie profile show <profile-pattern>
  kelpie status <profile>
  kelpie diag <profile>
  kelpie logs <profile> <service> [lines]
  kelpie env keys <profile>
  kelpie env peek <profile> <key>
  kelpie env set <profile> <key> <value> -- <command>
  kelpie env list <profile>
  kelpie env persist <profile> <key> <value>
  kelpie env remove <profile> <key>
  kelpie version
  kelpie help

Options:
  --version, -v  Show version information.
  --help, -h     Show command help.
  --config-dir <dir>    Override the config directory.
  --profiles-dir <dir>  Override the SSH profile directory.
  --logs-dir <dir>      Override the log directory.
  --bin-dir <dir>       Override the binary directory.
  --keys-dir <dir>      Override the key directory.
  --dat-dir <dir>       Override the runtime data directory.
```

### `kelpiemcp version`

目的:

`kelpiemcp` のバージョン情報を表示します。

構文:

```powershell
kelpiemcp version
kelpiemcp --version
kelpiemcp -v
```

引数詳細:

- なし。`--version` / `-v` は別名です。

引数サンプル:

- なし。

処理内容:

`kelpiemcp` のバージョン情報を表示します。

実行結果サンプル:

```text
kelpiemcp 0.3.4.0
```

### `kelpiemcp help`

目的:

`kelpiemcp` のコマンドヘルプを表示します。

構文:

```powershell
kelpiemcp help
kelpiemcp --help
kelpiemcp -h
```

引数詳細:

- なし。`--help` / `-h` は別名です。

引数サンプル:

- なし。

処理内容:

利用可能な `kelpiemcp` コマンドと option を表示します。

実行結果サンプル:

```text
Usage:
  kelpiemcp start [--reload-config]
  kelpiemcp stop
  kelpiemcp status
  kelpiemcp service register
  kelpiemcp service unregister
  kelpiemcp service status
  kelpiemcp profile add <profile>
  kelpiemcp profile reload <profile>
  kelpiemcp profile revoke <profile>
  kelpiemcp profile-capabilities [profile]
  kelpiemcp password <profile>
  kelpiemcp forget <profile>
  kelpiemcp version
  kelpiemcp help

Options:
  --version, -v  Show version information.
  --help, -h     Show command help.
  --config-dir <dir>    Override the config directory.
  --profiles-dir <dir>  Override the SSH profile directory.
  --logs-dir <dir>      Override the log directory.
  --bin-dir <dir>       Override the binary directory.
  --keys-dir <dir>      Override the key directory.
  --dat-dir <dir>       Override the runtime data directory.
```

### `kelpie services <profile>`

目的:

対象プロファイルのサービス状態を表示する候補です。現時点では未実装です。

構文:

```powershell
kelpie services vps01
```

引数詳細:

- `profile`: サービス状態を表示するプロファイル名。

引数サンプル:

- `vps01`

処理内容:

対象プロファイルの service 状態を表示する候補です。現時点では未実装です。

実行結果サンプル:

```text
Not implemented.
```

### `kelpie pkg ...`

目的:

パッケージ操作系 CLI コマンドの候補です。現時点では未実装です。MCP tool としては `MCP_COMMANDS.ja.md` の package tools を正とします。

構文:

```powershell
kelpie pkg check-updates vps01
kelpie pkg simulate-install vps01 nginx
kelpie pkg install vps01 nginx
```

引数詳細:

- `check-updates`: 更新可能な package を確認する候補。
- `simulate-install <profile> <package>`: 対象 package の install dry-run 候補。
- `install <profile> <package>`: 対象 package の確認付き install 候補。
- `profile`: 操作対象のプロファイル名。
- `package`: 操作対象の package 名。

引数サンプル:

- `profile`: `vps01`
- `package`: `nginx`

処理内容:

package 操作系 CLI コマンドの候補です。現時点では未実装です。

実行結果サンプル:

```text
Not implemented.
```

## Safety Notes

- `COMMANDS.ja.md` は通常のターミナルから直接実行する CLI コマンドの正本です。MCP callable tool は `MCP_COMMANDS.ja.md` を参照してください。
- 平文パスワードは `profiles/<profile>.json` に保存しません。
- 秘密鍵パスやパスワードそのものは CLI 表示に出しません。
- `kelpie login` の対話セッションでは、送信前に Kelpie の `Mode` / `Capabilities` によるポリシー評価を行います。
- `kelpiemcp password <profile>` で保存したパスワードは、起動中の `KelpieMCPServer` のメモリ上だけに保持します。
- `/mcp` は MCP クライアント用の Streamable HTTP endpoint です。ブラウザで起動確認する場合は `http://127.0.0.1:45432/health` を使います。

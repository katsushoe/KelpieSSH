# KelpieSSH Commands

最終更新: 2026-06-18

このファイルは、利用者が通常のターミナルから直接実行する `kelpie` / `kelpiemcp` CLI コマンドの正本です。
MCP callable tool の仕様と実行例は `MCP_COMMANDS.ja.md` を正本とします。

## Command Groups

| Group | Command | 内容 |
| :--- | :--- | :--- |
| MCP server control | `kelpiemcp start [--reload:<profile>]`, `kelpiemcp stop`, `kelpiemcp status` | `KelpieMCPServer` の起動、停止、状態確認を行う。 |
| MCP Windows Service | `kelpiemcp service register`, `kelpiemcp service unregister`, `kelpiemcp service status` | Windows Service 登録、登録解除、登録状態確認を行う。 |
| MCP password session | `kelpiemcp password`, `kelpiemcp forget` | 起動中の MCP server に SSH パスワードを一時保存、削除する。 |
| Compatibility | `kelpiemcp login`, `kelpiemcp logout` | 旧名互換。新規利用では `password` / `forget` を使う。 |
| Initialization | `kelpie init` | `KelpieHome` 配下の初期ディレクトリとサンプル設定を作成する。 |
| Profile/session | `kelpie open`, `kelpie login`, `kelpie logout`, `kelpie profiles`, `kelpie sessions`, `kelpie kill` | SSH プロファイル選択、ログイン、セッション表示、セッション終了を行う。 |
| Mode/UI | `kelpie gui`, `kelpie cli`, `kelpie login --console`, `kelpie login --desktop` | CLI/GUI モードや一時的な起動方式を切り替える。 |
| Diagnostics | `kelpie profile show`, `kelpie status`, `kelpie diag`, `kelpie logs` | プロファイル情報、MCP server 状態、SSH 診断、サービスログを表示する。 |
| Environment | `kelpie env keys`, `kelpie env peek`, `kelpie env set`, `kelpie env list`, `kelpie env persist`, `kelpie env remove` | profile policy に従って remote 環境変数の key 表示、値参照、一時設定、永続化を行う。 |
| Help/version | `kelpie version`, `kelpie help` | バージョンとヘルプを表示する。 |
| Candidates | `kelpie services`, `kelpie pkg ...` | 今後追加候補。 |

## Common Options

`KelpieHome` は `kelpie` / `kelpiemcp` の配置ディレクトリの1つ上に固定します。たとえば `D:\Kelpie\bin\kelpie.exe` から実行した場合、`KelpieHome` は `D:\Kelpie` です。

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
| `RhelDnfCommandProvider` | `rhel` | `dnf` | `dnf` | `pkg_check_updates`, `pkg_simulate_install`, `pkg_install`, `pkg_simulate_remove`, `pkg_remove` |

## Commands

この章では、各コマンドに目的、構文、引数詳細、引数サンプル、処理内容、実行結果サンプル、安全上の注意を記載します。

### `kelpiemcp start`

目的:

`KelpieMCPServer` 本体プロセスの起動を要求します。コマンド自体はすぐ終了します。

構文:

```powershell
kelpiemcp start [--reload:<profile>]
```

引数詳細:

| 引数 | 必須 | 説明 |
| :--- | :---: | :--- |
| `--reload:<profile>` | no | 管理者が編集済み SSH profile を明示的に再読み込み対象として指定する。対象 profile の JSON が正常な場合、その内容を今回の起動で採用し、次回起動時の trust store 基準 hash として更新する。複数 profile を指定する場合は、このオプションを繰り返す。 |

引数サンプル:

```powershell
kelpiemcp start --reload:vps01
```

処理内容:

起動中でなければ `KelpieMCPServer` の起動を要求します。Windows で `KelpieMCPServer` が Windows Service として登録済みの場合は Windows Service を開始します。この場合は管理者権限のターミナルから実行してください。未登録の場合は通常のローカルプロセスとして起動します。すでに起動中の場合は二重起動せず、起動中であることを返します。

MCPサーバー起動時は、SSH profile ファイルの hash を protected trust store と照合します。通常起動で hash が一致しない profile は load エラーになり、他の profile はロード継続します。正規に profile を編集した場合は、`--reload:<profile>` を指定して起動します。対象 profile の JSON が正常な場合だけ trust store を更新します。trust store の復号または認証に失敗した場合、MCPサーバーは起動失敗します。起動ユーザーは全 profile に不正がないことを確認し、trust store を退避または削除して再起動します。削除した場合、次回起動時に全 profile が新規 baseline として登録されます。

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

- `--reload:<profile>` は、編集した profile が意図した内容であり、不正変更がないことを確認してから使う。
- trust store を削除すると、次回起動時に全 profile が信頼済み baseline として再登録される。

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

### `kelpie init [profile]`

目的:

`KelpieHome` 配下に初期ディレクトリとサンプル設定ファイルを作成します。既存ファイルは上書きしません。

構文:

```powershell
kelpie init
kelpie init vps01
```

引数詳細:

- `profile`: 作成するプロファイル名。省略時は `sample`。

引数サンプル:

- 省略時: `sample`
- 指定時: `vps01`

処理内容:

`KelpieHome` 配下に `config`、`profiles`、`keys` などの初期ディレクトリとサンプル設定ファイルを作成します。既存ファイルは上書きしません。

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

### `kelpie profile show <profile>`

目的:

対象プロファイルの概要を表示します。秘密鍵パスやパスワードそのものは表示しません。

構文:

```powershell
kelpie profile show vps01
```

引数詳細:

- `profile`: 表示するプロファイル名。

引数サンプル:

- `vps01`

処理内容:

対象プロファイルを読み込み、接続先、OS family、command provider、mode、認証方式などの概要を表示します。

実行結果サンプル:

```text
Profile: vps01
Host: example.invalid
Port: 22
User: deploy
OS family: alma
Package manager: dnf
Command OS family: rhel
Command providers: CommonDiagnosticCommandProvider, RhelDnfCommandProvider
Mode: Safe
Capabilities: AllowListPackage
Allowed roots: /var/www
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
Command providers: CommonDiagnosticCommandProvider, RhelDnfCommandProvider
Mode: Safe
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
kelpie 0.1.3.3
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
  kelpie init [profile]
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
  kelpie profile show <profile>
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

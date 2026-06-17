# KelpieSSH

KelpieSSH は、SSH 越しの VPS 診断と保守を安全に補助するためのローカル MCP サーバーです。

English documentation is available in [README.md](../../README.md).

コマンドの詳細は [COMMANDS.ja.md](COMMANDS.ja.md) を参照してください。

設定の詳細は [CONFIG.ja.md](CONFIG.ja.md) を参照してください。

`kelpie` は `config/kelpie.json` を読み込みます。`kelpiemcp` と `KelpieMCPServer` は `config/kelpiemcp.json` を読み込みます。

設定サンプルは `config_samples/` 配下にあります。

```text
config_samples/
├─ kelpie.json
├─ kelpiemcp.json
└─ servers/
   └─ vps01.json
```

## はじめに

KelpieSSH の使い方に合わせて導入方法を選びます。

### バイナリ利用者

通常利用では、GitHub Releases から KelpieSSH の `.msi` インストーラーをダウンロードして実行します。

インストール後、新しいターミナルを開いてください。

コマンドを実行できることを確認します。

```powershell
kelpie version
```

出力例:

```text
kelpie 0.1.4.1
```

ターミナルで次を実行します。

```powershell
kelpie init
```

初期化時に名前付き SSH profile を作成するには、次のように実行します。

```powershell
kelpie init vps01
```

接続前に生成された profile を編集してください。profile ファイルは次の場所に作成されます。

```text
<KelpieHome>\profiles\vps01.json
```

Windows が不明な発行元または SmartScreen 警告を表示する場合は、MSI が公式 GitHub Release からダウンロードされたものか確認し、提供されている場合は公開 checksum と照合してください。

### 手動バイナリ配置

zip 形式の配布物または一時的なローカル配置を使う場合は、コマンドを `bin` ディレクトリ配下に置きます。

```text
D:\Kelpie
└─ bin
   ├─ kelpie.exe
   ├─ kelpiemcp.exe
   └─ mcp
      └─ KelpieMCPServer.exe
```

`D:\Kelpie\bin` をユーザー `PATH` に追加します。

```powershell
$kelpieBin = "D:\Kelpie\bin"
$userPath = [Environment]::GetEnvironmentVariable("Path", "User")
if (($userPath -split ";") -notcontains $kelpieBin) {
  $newUserPath = if ([string]::IsNullOrWhiteSpace($userPath)) { $kelpieBin } else { $userPath.TrimEnd(";") + ";" + $kelpieBin }
  [Environment]::SetEnvironmentVariable("Path", $newUserPath, "User")
}
```

`PATH` 更新後は新しいターミナルを開き、同じ確認コマンドを実行します。

```powershell
kelpie version
kelpiemcp status
```

ターミナルで次を実行します。

```powershell
D:\Kelpie\bin\kelpie.exe init
```

`D:\Kelpie\bin\kelpie.exe` から `kelpie init` を実行した場合、`D:\Kelpie` 配下に次のファイルとディレクトリが作成されます。

```text
D:\Kelpie
├─ config
│  ├─ kelpie.json
│  └─ kelpiemcp.json
├─ profiles
│  └─ sample.json
├─ keys
├─ dat
├─ logs
└─ bin
```

初期化時に名前付き profile を作成するには、次のように実行します。

```powershell
D:\Kelpie\bin\kelpie.exe init vps01
```

接続前に生成された profile を編集してください。

```text
D:\Kelpie\profiles\vps01.json
```

`PATH` を更新したくない場合は、`D:\Kelpie\bin\kelpie.exe` のようなフルパスを使い続けてください。

### 開発者

ソースから KelpieSSH をビルドする場合にのみ Visual Studio が必要です。

ソリューションをビルドしてテストします。

```powershell
dotnet build
dotnet test
```

コマンド バイナリを手動配置用ディレクトリへ publish します。

```powershell
dotnet publish src\KelpieClientCommand\KelpieClientCommand.csproj -c Release -o D:\Kelpie\bin
dotnet publish src\KelpieServerCommand\KelpieServerCommand.csproj -c Release -o D:\Kelpie\bin
dotnet publish src\KelpieMCPServer\KelpieMCPServer.csproj -c Release -o D:\Kelpie\bin\mcp
D:\Kelpie\bin\kelpie.exe init
```

`kelpie init` は既存の設定ファイルを上書きしません。利用前に生成された host、user、key、policy の値を編集してください。

## MCP サーバー

Codex から接続する前に、ローカル MCP サーバーを起動します。

```powershell
kelpiemcp start
```

停止するには次を実行します。

```powershell
kelpiemcp stop
```

ローカル サーバーの状態を確認します。

```powershell
kelpiemcp status
```

パスワード認証の SSH profile を使う場合は、実行中のサーバー セッションにパスワードを保存または削除します。

```powershell
kelpiemcp password vps01
kelpiemcp forget vps01
```

Kelpie CLI のヘルプやバージョン情報は次のコマンドで確認できます。

```powershell
kelpie init
kelpie init vps01
kelpie help
kelpie --help
kelpie version
kelpie --version
```

設定済み SSH profile を確認します。

```powershell
kelpie profiles
kelpie profile show vps01
kelpie status vps01
```

VPS 診断や service log の tail を実行します。

```powershell
kelpie diag vps01
kelpie logs vps01 nginx.service
kelpie logs vps01 nginx.service 200
```

現時点では、`kelpie diag` と `kelpie logs` は CLI プロセスから SSH コマンドを直接実行し、秘密鍵 profile を主な対象とします。`kelpiemcp password` は実行中の `KelpieMCPServer` セッションにのみパスワード認証情報を保存します。

パスワードはローカル control pipe を通して実行中の `KelpieMCPServer` へ送られ、そのサーバー プロセスのメモリ内にのみ保持されます。

既定では、`KelpieMCPServer` は port `45432` で待ち受け、次の MCP endpoint を公開します。

```text
http://127.0.0.1:45432/mcp
```

port は `config/kelpiemcp.json` で設定します。

## セキュリティ

KelpieSSH は、読み取り中心の診断と許可リスト方式の SSH コマンド実行から始める設計です。

実ホスト名、実ユーザー名、パスワード、passphrase、秘密鍵、本番 profile ファイルをコミットしないでください。実際の `profiles/*.json`、`keys/`、`dat/`、`logs/` は公開リポジトリの外に置いてください。

パスワード認証は、実行中の `KelpieMCPServer` プロセスに対するセッション ベースです。平文パスワードを JSON 設定ファイルに保存してはいけません。

脆弱性報告と対応対象 version の方針は [SECURITY.ja.md](SECURITY.ja.md) を参照してください。

## ライセンス

KelpieSSH は MIT License で公開されています。詳細は [LICENSE](../../LICENSE) を参照してください。

Copyright (c) 2026 Akatsukisoft.

MIT License は、著作権表示と許諾表示をソフトウェアの複製または重要な部分に含めることを条件として、KelpieSSH の商用利用、改変、再配布、サブライセンス、販売を許可します。

KelpiePro は有償の closed-source desktop product として計画されています。KelpiePro は、OSS 実装を closed-source product repository へ fork または copy せず、KelpieSSH と Kelpie Core libraries を NuGet packages として参照できます。この repository は OSS 実装と package metadata の upstream source として維持されます。

KelpieSSH packages または binaries を KelpiePro と再配布する場合は、KelpieSSH の MIT license notice と [THIRD_PARTY_NOTICES.ja.md](THIRD_PARTY_NOTICES.ja.md) に記載された third-party notices を installer、application about box、bundled documentation、または同等の notices location に含めてください。

現在の runtime dependency review では、KelpieSSH runtime packages に GPL、AGPL、LGPL、SSPL、Commons Clause、その他の non-permissive dependencies は確認されていません。package version を追加または更新した場合は、`THIRD_PARTY_NOTICES.md` を再確認してください。

## Codex MCP 設定

Codex MCP 設定へ Streamable HTTP MCP server URL を追加します。

```toml
[mcp_servers.kelpie]
url = "http://127.0.0.1:45432/mcp"
```

`Server:Port` を変更した場合は、Codex 側の URL も合わせて更新してください。

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

## SSH profiles

SSH connection profiles は、`KelpieHome/profiles` 配下にサーバーごと1つの JSON ファイルとして設定します。

runtime configuration は既定 SSH profile を設定しません。`kelpie open vps01` や MCP tool の `profileName` のように、profile を明示してください。

Profiles は host 側の永続化 adapter であり、core library の実行境界ではありません。Application API は `SshRemoteOperation` も受け取ります。`SshRemoteOperation` は endpoint、credential、policy、operation、options を持つ1回の SSH 操作を表します。既存の CLI と MCP profile loaders は、保存済み profile を実行前に `SshRemoteOperation` へ変換します。edition limits、license state、ads、support、display order、notes、customer data などの製品固有概念は KelpieSSH の外側に置きます。

```json
{
  "LogDirectory": "D:\\Kelpie\\logs"
}
```

各 `KelpieHome/profiles/*.json` ファイルが1つの profile です。ファイル名が profile 名になるため、`profiles/vps01.json` は profile `vps01` です。
公開サンプル profile は `config_samples/servers/vps01.json` です。

```json
{
  "Host": {
    "Address": "example.invalid",
    "Port": 22
  },
  "Auth": {
    "UserName": "deploy",
    "Method": "privateKey",
    "PrivateKeyFile": "vps01_ed25519"
  },
  "Connection": {
    "TimeoutSeconds": 10
  },
  "Platform": {
    "OsFamily": "debian"
  },
  "Mode": "Safe",
  "Capabilities": [
    "AllowListPackage"
  ],
  "Rights": {
    "$WebDeploy": "$ReadWrite|@Import",
    "$LogRead": "$ReadOnly"
  },
  "AllowedRoots": {
    "/var/www": "$WebDeploy",
    "/var/log": "$LogRead"
  }
}
```

`Auth` と `Authentication` はどちらも SSH 認証設定として受け付けます。`Authentication` が正式名で、`Auth` は手書きしやすい短縮 alias です。
`Auth.PrivateKeyFile` / `Authentication.PrivateKeyFile` が相対パスの場合は `KelpieHome/keys` 配下として解決されます。実ホスト、実ユーザー、秘密鍵ファイル、passphrase、password をコミットしないでください。
`Auth.Method` / `Authentication.Method` は `privateKey` と `password` をサポートします。パスワード認証は `PasswordSecretName` を使用します。平文パスワードを JSON ファイルに保存してはいけません。現在の password provider は、`kelpiemcp password <profile>` で設定する in-memory session store です。
`Platform.OsFamily`、任意の `Platform.PackageManager`、`Mode`、`Capabilities` は、各 profile の安全な command behavior の選択と評価に使われます。
`Mode` は CLI と MCP で共有される permission preset です。サポートされる mode は `ReadOnly`、`Safe`、`Maintenance`、`Expert` です。
`Capabilities` は CLI 専用 override です。MCP 実行では `Capabilities` を無視し、`Mode` ベースの権限だけを評価します。`Expert` mode でも、MCP から secrets が表示されることはありません。
`Capabilities` は `["AllowAlias", "AllowSudo"]` のような array、または `"AllowAlias|AllowSudo"` のような pipe 区切り string を受け付けます。不明な capability 名は configuration error です。
`AllowedRoots` は path-based operations を制限します。`@Read|@List|@Write|@CD` のような pipe 区切り raw flags、`Rights` で定義した `$` prefix の named presets、または `@Read|@List|@Write|@Import|@Export|@CD` を意味する `$ALL` を使います。built-in `$ReadOnly`、`$ReadWrite`、`$ALL` は利用可能で、上書きできません。`Read`、`Write`、`ALL` のような bare tokens は configuration error です。`*` と `**` は明示的な global path values です。`AllowedRoots` が省略または空の場合、path-based operations は policy 上許可されません。
実際の `profiles/*.json` files はコミットしないでください。

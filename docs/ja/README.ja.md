# KelpieSSH

KelpieSSH は、SSH 越しの VPS 診断と保守を安全に補助するためのローカル MCP サーバーです。

English documentation is available in [README.md](../../README.md).

コマンドの詳細は [COMMANDS.ja.md](COMMANDS.ja.md) を参照してください。

MCP command の詳細は [MCP_COMMANDS.ja.md](MCP_COMMANDS.ja.md) を参照してください。

設定の詳細は [CONFIG.ja.md](CONFIG.ja.md) を参照してください。

SSH profile 設定は [PROFILE_GUIDE.ja.md](PROFILE_GUIDE.ja.md) を参照してください。

AI MCP サーバー設定は [MCP_GUIDE.ja.md](MCP_GUIDE.ja.md) を参照してください。

`kelpie` は `config/kelpie.json` を読み込みます。

設定サンプルは `config_samples/` 配下にあります。

```text
config_samples/
├─ kelpie.json
└─ servers/
   └─ vps01.json
```

## はじめに

KelpieSSH の使い方に合わせて導入方法を選びます。

### バイナリ利用者

#### 1. バイナリ（`.msi`）のインストール

通常利用では、GitHub Releases から KelpieSSH の `.msi` インストーラーをダウンロードして実行します。

インストール後、新しいターミナルを開いてください。

コマンドを実行できることを確認します。

```powershell
kelpie version
```

出力例:

```text
kelpie 0.1.5.0
```

#### 2. Kelpie home の初期化と profile 作成

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

Profile の記述方法の詳細は [PROFILE_GUIDE.ja.md](PROFILE_GUIDE.ja.md) を参照してください。

このファイルに、接続先 host、SSH user、認証方式、鍵またはパスワード secret 参照を設定します。秘密鍵認証では、秘密鍵ファイルを `<KelpieHome>\keys` 配下に置き、`Auth.PrivateKeyFile` にそのファイル名を設定します。対応する公開鍵は、事前にサーバー側へ登録されている必要があります。パスワード認証では、`Auth.Method` を `password` にし、`Auth.PasswordSecretName` を設定します。平文パスワードを profile に保存してはいけません。

#### 3. サーバーへの接続

profile を編集した後、対象サーバーを開きます。

```powershell
kelpie open vps01
```

パスワード認証の profile では、対象を開いた後にログインします。

```powershell
kelpie login
```

Windows が不明な発行元または SmartScreen 警告を表示する場合は、MSI が公式 GitHub Release からダウンロードされたものか確認し、提供されている場合は公開 checksum と照合してください。

### Zip 配布版の利用者

#### 1. Zip バイナリの配置

`KelpieSSH-x.x.x.x-x64.zip` を `D:\Kelpie` に展開します。CLI 利用に関係する主なファイルは次の構成で配置されます。

```text
D:\Kelpie
├─ bin
│  ├─ kelpie.exe
│  └─ kelpiemcp.exe
├─ config_samples
├─ docs
├─ README.md
├─ COMMANDS.md
├─ CONFIG.md
├─ MCP_GUIDE.md
└─ PROFILE_GUIDE.md
```

#### 2. `PATH` の追加とコマンド確認

`D:\Kelpie\bin` をユーザー `PATH` に追加します。

```powershell
$kelpieBin = "D:\Kelpie\bin"
$userPath = [Environment]::GetEnvironmentVariable("Path", "User")
if (($userPath -split ";") -notcontains $kelpieBin) {
  $newUserPath = if ([string]::IsNullOrWhiteSpace($userPath)) { $kelpieBin } else { $userPath.TrimEnd(";") + ";" + $kelpieBin }
  [Environment]::SetEnvironmentVariable("Path", $newUserPath, "User")
}
```

`PATH` 更新後は新しいターミナルを開いてください。

コマンドを実行できることを確認します。

```powershell
kelpie version
```

出力例:

```text
kelpie 0.1.5.0
```

`PATH` を更新したくない場合は、`D:\Kelpie\bin\kelpie.exe` のようなフルパスを使い続けてください。

#### 3. Kelpie home の初期化と profile 作成

ターミナルで次を実行します。

```powershell
D:\Kelpie\bin\kelpie.exe init
```

`D:\Kelpie\bin\kelpie.exe` から `kelpie init` を実行した場合、CLI 利用に関係する主なファイルとディレクトリが `D:\Kelpie` 配下に作成されます。

```text
D:\Kelpie
├─ config
│  └─ kelpie.json
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

Profile の記述方法の詳細は [PROFILE_GUIDE.ja.md](PROFILE_GUIDE.ja.md) を参照してください。

このファイルに、接続先 host、SSH user、認証方式、鍵またはパスワード secret 参照を設定します。秘密鍵認証では、秘密鍵ファイルを `D:\Kelpie\keys` 配下に置き、`Auth.PrivateKeyFile` にそのファイル名を設定します。対応する公開鍵は、事前にサーバー側へ登録されている必要があります。パスワード認証では、`Auth.Method` を `password` にし、`Auth.PasswordSecretName` を設定します。平文パスワードを profile に保存してはいけません。

#### 4. サーバーへの接続

profile を編集した後、対象サーバーを開きます。

```powershell
kelpie open vps01
```

パスワード認証の profile では、対象を開いた後にログインします。

```powershell
kelpie login
```

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
D:\Kelpie\bin\kelpie.exe init
```

名前付き profile を作成する場合は、接続前に初期化します。

```powershell
D:\Kelpie\bin\kelpie.exe init vps01
```

`kelpie init` は既存の設定ファイルを上書きしません。利用前に生成された profile を編集してください。

```text
D:\Kelpie\profiles\vps01.json
```

Profile の記述方法の詳細は [PROFILE_GUIDE.ja.md](PROFILE_GUIDE.ja.md) を参照してください。

`kelpie open vps01` を実行する前に、host、user、認証方式、秘密鍵ファイル名またはパスワード secret 参照を設定します。

### AI users

AI の MCP サーバーとして Kelpie を使う場合は、[MCP_GUIDE.ja.md](MCP_GUIDE.ja.md) にしたがってサーバーを設定、起動してください。
MCP サーバーの停止とパスワードセッションの削除も [MCP_GUIDE.ja.md](MCP_GUIDE.ja.md) を参照してください。

### 切断とログアウト

`kelpie login` で開始した対話 SSH session を閉じるには、session 内で `logout` または `exit` を入力します。

```text
logout
```

## Kelpie command-line tools

通常のターミナル利用では、`kelpie` command-line tools に MCP サーバーは不要です。ローカル設定の初期化、profile 確認、対象 profile の open、対話 SSH session、診断、service log の tail は、ターミナルから直接実行できます。

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

対話 SSH session を実行します。

```powershell
kelpie open vps01
kelpie login
```

VPS 診断や service log の tail を実行します。

```powershell
kelpie diag vps01
kelpie logs vps01 nginx.service
kelpie logs vps01 nginx.service 200
```

`kelpie login`、`kelpie diag`、`kelpie logs` は CLI プロセスから SSH 操作を直接実行します。password profile の場合、CLI は実行時にパスワードを尋ね、現在の command process 内にだけ保持します。`kelpie status` はローカル MCP サーバーの状態も表示できますが、上記 command-line tools の利用に MCP サーバーは不要です。

## セキュリティ

KelpieSSH は、読み取り中心の診断と許可リスト方式の SSH コマンド実行から始める設計です。

実ホスト名、実ユーザー名、パスワード、passphrase、秘密鍵、本番 profile ファイルをコミットしないでください。実際の `profiles/*.json`、`keys/`、`dat/`、`logs/` は公開リポジトリの外に置いてください。

パスワード認証は runtime のみで扱います。CLI SSH command は現在の command process 用にパスワードを尋ね、`kelpiemcp password <profile>` は実行中の `KelpieMCPServer` プロセス内にのみパスワードを保持します。平文パスワードを JSON 設定ファイルに保存してはいけません。

脆弱性報告と対応対象 version の方針は [SECURITY.ja.md](SECURITY.ja.md) を参照してください。

## ライセンス

KelpieSSH は MIT License で公開されています。詳細は [LICENSE](../../LICENSE) を参照してください。

Copyright (c) 2026 Akatsukisoft.

MIT License は、著作権表示と許諾表示をソフトウェアの複製または重要な部分に含めることを条件として、KelpieSSH の商用利用、改変、再配布、サブライセンス、販売を許可します。

KelpiePro は有償の closed-source desktop product として計画されています。KelpiePro は、OSS 実装を closed-source product repository へ fork または copy せず、KelpieSSH と Kelpie Core libraries を NuGet packages として参照できます。この repository は OSS 実装と package metadata の upstream source として維持されます。

KelpieSSH packages または binaries を KelpiePro と再配布する場合は、KelpieSSH の MIT license notice と [THIRD_PARTY_NOTICES.ja.md](THIRD_PARTY_NOTICES.ja.md) に記載された third-party notices を installer、application about box、bundled documentation、または同等の notices location に含めてください。

現在の runtime dependency review では、KelpieSSH runtime packages に GPL、AGPL、LGPL、SSPL、Commons Clause、その他の non-permissive dependencies は確認されていません。package version を追加または更新した場合は、`THIRD_PARTY_NOTICES.md` を再確認してください。

## SSH profiles

SSH connection profiles は、`KelpieHome/profiles` 配下にサーバーごと1つの JSON ファイルとして設定します。

runtime configuration は既定 SSH profile を設定しません。`kelpie open vps01` や MCP tool の `profileName` のように、profile を明示してください。

Profiles は、KelpieSSH library が扱う保存済み SSH 接続設定です。`SshConnectionProfile`、`SshConnectionProfileFileLoader`、`SshConnectionProfileCatalog` は、CLI、MCP、KelpiePro などの製品から利用できる library level の profile API です。

Application API は `SshRemoteOperation` も受け取ります。`SshRemoteOperation` は endpoint、credential、policy、operation、options を持つ1回の SSH 操作を表します。保存済み profile に依存せず、1回限りの操作を直接実行したい caller 向けの入力です。既存の CLI と MCP profile loaders は、保存済み profile を実行前に `SshRemoteOperation` へ変換できます。

profile count limits、edition limits、license state、ads、support、display order、notes、customer data などの製品固有概念は KelpieSSH の外側に置きます。KelpiePro は、OSS profile model に edition policy を入れず、読み込むまたは表示する OSS profile 数を制限することで Free / Standard の差を実装できます。

各 `KelpieHome/profiles/*.json` ファイルが1つの profile です。ファイル名が profile 名になるため、`profiles/vps01.json` は profile `vps01` です。
公開サンプル profile は `config_samples/servers/vps01.json` です。

Profile の項目詳細、サンプル、validation checklist、troubleshooting は [PROFILE_GUIDE.ja.md](PROFILE_GUIDE.ja.md) を参照してください。
実際の `profiles/*.json` files はコミットしないでください。

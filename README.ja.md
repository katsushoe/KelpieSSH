# KelpieSSH

KelpieSSH は、SSH 越しの VPS 診断と保守を安全に補助するためのローカル MCP サーバーです。

英語版は [README.md](README.md) を参照してください。

コマンドの詳細は [docs/ja/COMMANDS.ja.md](docs/ja/COMMANDS.ja.md) を参照してください。

コマンドラインオプションは [CLI_OPTIONS.md](CLI_OPTIONS.md) を参照してください。

MCP ツールの詳細は [docs/ja/MCP_COMMANDS.ja.md](docs/ja/MCP_COMMANDS.ja.md) を参照してください。

設定の詳細は [docs/ja/CONFIG.ja.md](docs/ja/CONFIG.ja.md) を参照してください。

SSH プロファイルの設定方法は [docs/ja/PROFILE_GUIDE.ja.md](docs/ja/PROFILE_GUIDE.ja.md) を参照してください。

AI 用 MCP サーバーの設定は [docs/ja/MCP_GUIDE.ja.md](docs/ja/MCP_GUIDE.ja.md) を参照してください。

プロバイダーの対応状況と実装状況は [PROVIDERS.md](PROVIDERS.md) を参照してください。

`kelpie` は `config/kelpie.json` を読み込みます。

設定サンプルは `config_samples/` 配下にあります。

```text
config_samples/
├─ kelpie.json
└─ servers/
   └─ vps01.json
```

公開サンプルはプレースホルダー値を使います。検証時は、サンプルプロファイルをローカルの `KelpieHome/profiles` へコピーし、ローカル Docker SSH コンテナなどの使い捨て SSH ターゲット向けに編集してから、接続前に check コマンドを実行してください。実ホスト名、実ユーザー名、秘密鍵、パスワード、raw log はこのリポジトリに入れないでください。

## はじめに

KelpieSSH の使い方に合わせて導入方法を選んでください。Alpha リリースでは Windows ZIP を主なバイナリ配布物とします。MSI はリリースに添付される場合がありますが、任意であり、未署名の場合があります。

### Alpha バイナリ利用者

#### 1. リリースバイナリをダウンロードする

Alpha リリースでは、GitHub Releases から `KelpieSSH-x.x.x.x-win-x64.zip` をダウンロードし、下の ZIP 配布版の手順に従ってください。

同じリリースに MSI が添付されている場合は、MSI を使うこともできます。Alpha MSI は未署名の場合があるため、Windows が不明な発行元または SmartScreen 警告を表示することがあります。

インストール後、または `PATH` 設定後に、新しいターミナルを開いてください。

コマンドを実行できることを確認します。

```powershell
kelpie version
```

出力例:

```text
kelpie 0.3.4.0
```

#### 2. Kelpie home を初期化してプロファイルを作成する

ターミナルで次を実行します。

```powershell
kelpie init
```

初期化時に名前付き SSH プロファイルを作成するには、次のように実行します。

```powershell
kelpie init vps01
```

対話形式の出力例:

```text
Create MCP server configuration.
Press Enter to use the default value.
MCP log directory [F:\Kelpie\logs]: F:\Kelpie\logs
MCP control pipe name [KelpieMCPServer.Control]: KelpieMCPServer.Control
Create SSH profile template.
Press Enter to use the default value.
Host address [localhost]: example.org
Port [22]: 2222
SSH user [deploy]: ops
Authentication method (privateKey/password) [privateKey]: password
Password secret name [kelpie:vps01]: kelpie:vps01
OS family [debian]: ubuntu
Mode (ReadOnly/Safe/Maintenance/Expert) [Safe]: ReadOnly
Read-only root, '-' to omit [/var/log]: /var/log/nginx
Read-write root, '-' to omit [/var/www]: -
Deny pattern, '-' to omit [**/.env]: **/.secret
Kelpie home: F:\Kelpie
Profile: vps01
Created directories:
  F:\Kelpie\config
  F:\Kelpie\profiles
  F:\Kelpie\keys
  F:\Kelpie\dat
  F:\Kelpie\bin\mcp
Created files:
  F:\Kelpie\config\kelpie.json
  F:\Kelpie\config\kelpiemcp.json
  F:\Kelpie\profiles\vps01.json
```

接続前に、生成されたプロファイルを編集してください。プロファイルファイルは次の場所に作成されます。

```text
<KelpieHome>\profiles\vps01.json
```

プロファイルの記述方法は [docs/ja/PROFILE_GUIDE.ja.md](docs/ja/PROFILE_GUIDE.ja.md) を参照してください。

このファイルに、接続先ホスト、SSH ユーザー、認証方式、秘密鍵またはパスワード参照を設定します。秘密鍵認証では、秘密鍵ファイルを `<KelpieHome>\keys` 配下に置き、`Auth.PrivateKeyFile` にそのファイル名を設定します。対応する公開鍵は、事前にサーバー側へ登録しておく必要があります。パスワード認証では、`Auth.Method` を `password` にし、`Auth.PasswordSecretName` を設定します。平文パスワードをプロファイルに保存してはいけません。

接続前に、SSH 接続を行わずローカル設定とプロファイルを検証します。

```powershell
kelpie config check
kelpie profile check vps01
```

最初の確認には、ローカル Docker SSH コンテナなどの使い捨て SSH ターゲットを使うと安全です。`kelpie config check` と `kelpie profile check <profile>` は、SSH 接続を開始する前に、ローカルファイル、JSON、スキーマ、認証参照、プロバイダー、ポリシー、許可ルート、特別パス、ユーザー、保留中バックアップ状態を検証します。

#### 3. サーバーに接続する

プロファイルを編集したら、対象サーバーを開きます。

```powershell
kelpie open vps01
```

パスワード認証のプロファイルでは、対象を開いた後にログインします。

```powershell
kelpie login
```

MSI で Windows が不明な発行元または SmartScreen 警告を表示した場合は、MSI が公式 GitHub Release からダウンロードしたものか確認してください。チェックサムが公開されている場合は、あわせて照合してください。

### ZIP 配布版の利用者

#### 1. ZIP からインストールする

`KelpieSSH-x.x.x.x-win-x64.zip` を展開し、次を実行します。

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-Kelpie.ps1
```

KelpieSSH は `%LOCALAPPDATA%\Programs\KelpieSSH` に配置され、その `bin` ディレクトリがユーザー `PATH` に追加されます。既存の設定やユーザーデータは削除されません。

配置先を変更する場合:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-Kelpie.ps1 -InstallDirectory F:\Kelpie
```

インストール後は新しいターミナルを開いてください。

配置される主なファイルは次の構成です。

```text
%LOCALAPPDATA%\Programs\KelpieSSH
├─ bin
│  ├─ kelpie.exe
│  ├─ kelpiemcp.exe
│  └─ mcp
│     └─ KelpieMCPServer.exe
├─ config_samples
├─ docs
├─ README.md
├─ README.ja.md
├─ COMMANDS.md
├─ CONFIG.md
├─ MCP_GUIDE.md
└─ PROFILE_GUIDE.md
```

#### 2. コマンドを確認する

```powershell
kelpie version
```

出力例:

```text
kelpie x.x.x.x
```

#### 3. Kelpie home を初期化してプロファイルを作成する

ターミナルで次を実行します。

```powershell
kelpie init
```

`kelpie init` を実行すると、CLI 利用に関係する主なファイルとディレクトリがインストール先に作成されます。

```text
%LOCALAPPDATA%\Programs\KelpieSSH
├─ config
│  └─ kelpie.json
├─ profiles
│  └─ sample.json
├─ keys
├─ dat
├─ logs
└─ bin
```

初期化時に名前付きプロファイルを作成するには、次のように実行します。

```powershell
kelpie init vps01
```

接続前に、生成されたプロファイルを編集してください。

```text
%LOCALAPPDATA%\Programs\KelpieSSH\profiles\vps01.json
```

プロファイルの記述方法は [docs/ja/PROFILE_GUIDE.ja.md](docs/ja/PROFILE_GUIDE.ja.md) を参照してください。

このファイルに、接続先ホスト、SSH ユーザー、認証方式、秘密鍵またはパスワード参照を設定します。秘密鍵認証では、秘密鍵ファイルをインストール先の `keys` ディレクトリに置き、`Auth.PrivateKeyFile` にそのファイル名を設定します。対応する公開鍵は、事前にサーバー側へ登録しておく必要があります。パスワード認証では、`Auth.Method` を `password` にし、`Auth.PasswordSecretName` を設定します。平文パスワードをプロファイルに保存してはいけません。

接続前に、SSH 接続を行わずローカル設定とプロファイルを検証します。

```powershell
kelpie config check
kelpie profile check vps01
```

最初の確認には、ローカル Docker SSH コンテナなどの使い捨て SSH ターゲットを使うと安全です。`kelpie config check` と `kelpie profile check <profile>` は、SSH 接続を開始する前に、ローカルファイル、JSON、スキーマ、認証参照、プロバイダー、ポリシー、許可ルート、特別パス、ユーザー、保留中バックアップ状態を検証します。

#### 4. サーバーに接続する

プロファイルを編集したら、対象サーバーを開きます。

```powershell
kelpie open vps01
```

パスワード認証のプロファイルでは、対象を開いた後にログインします。

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

コマンドバイナリを手動配置用ディレクトリへ publish します。

```powershell
dotnet publish src\KelpieClientCommand\KelpieClientCommand.csproj -c Release -o F:\Kelpie\bin
dotnet publish src\KelpieServerCommand\KelpieServerCommand.csproj -c Release -o F:\Kelpie\bin
F:\Kelpie\bin\kelpie.exe init
```

名前付きプロファイルを作成する場合は、接続前に初期化します。

```powershell
F:\Kelpie\bin\kelpie.exe init vps01
```

`kelpie init` は既存の設定ファイルを上書きしません。利用前に、生成されたプロファイルを編集してください。

```text
F:\Kelpie\profiles\vps01.json
```

プロファイルの記述方法は [docs/ja/PROFILE_GUIDE.ja.md](docs/ja/PROFILE_GUIDE.ja.md) を参照してください。

`kelpie open vps01` を実行する前に、接続先ホスト、ユーザー、認証方式、秘密鍵ファイル名またはパスワード参照を設定します。

プロファイルを開く前に、ローカル設定とプロファイルを検証します。

```powershell
F:\Kelpie\bin\kelpie.exe config check
F:\Kelpie\bin\kelpie.exe profile check vps01
```

### AI 利用者

Kelpie を AI 用 MCP サーバーとして使う場合は、[docs/ja/MCP_GUIDE.ja.md](docs/ja/MCP_GUIDE.ja.md) にしたがってサーバーを設定、起動してください。
MCP サーバーの停止とパスワードセッションの削除も [docs/ja/MCP_GUIDE.ja.md](docs/ja/MCP_GUIDE.ja.md) を参照してください。

### 切断とログアウト

`kelpie login` で開始した対話 SSH セッションを閉じるには、セッション内で `logout` または `exit` を入力します。

```text
logout
```

## Kelpie のコマンドラインツール

通常のターミナル利用では、`kelpie` コマンドラインツールに MCP サーバーは不要です。ローカル設定の初期化、プロファイルの確認、対象プロファイルのオープン、対話 SSH セッション、診断、サービスログの表示は、ターミナルから直接実行できます。

KelpieSSH はプロバイダーを使い、対象 OS、パッケージマネージャー、サービス、公開 Web ルートごとに制限された SSH 操作を公開します。プロバイダーは許可リスト方式で、任意のシェル実行を開放せず、名前付きで引数検証された操作だけを追加します。現在のプロバイダー一覧と実装状況は [PROVIDERS.md](PROVIDERS.md) を参照してください。

Kelpie CLI のヘルプやバージョン情報は次のコマンドで確認できます。

```powershell
kelpie init
kelpie init vps01
kelpie help
kelpie --help
kelpie version
kelpie --version
```

ローカル設定と SSH プロファイルを検証します。

```powershell
kelpie config check
kelpie profile check vps01
```

これらの check コマンドは、通常運用で `kelpie open`、`kelpie login`、`kelpie diag`、MCP 利用の前に実行する想定です。SSH 接続を開始しないため、ローカル Docker SSH コンテナなどの使い捨てテスト対象にも向いています。

設定済み SSH プロファイルを表示します。

```powershell
kelpie profiles
kelpie status vps01
kelpie profile show vps01
```

対話 SSH セッションを実行します。

```powershell
kelpie open vps01
kelpie login
```

VPS 診断やサービスログの表示を実行します。

```powershell
kelpie diag vps01
kelpie logs vps01 nginx.service
kelpie logs vps01 nginx.service 200
```

`kelpie login`、`kelpie diag`、`kelpie logs` は CLI プロセスから SSH 操作を直接実行します。パスワード認証のプロファイルでは、CLI が実行時にパスワードを尋ね、そのコマンドプロセス内だけに保持します。`kelpie status` はローカル MCP サーバーの状態も表示できますが、上記のコマンドラインツールを使うだけなら MCP サーバーは不要です。

ローカル静的検査、MCPサーバー疎通、MCP実行ホスト診断、SSH対象診断は、それぞれ確認範囲が異なります。1つの確認結果を別の実行境界の正常性と解釈する前に、[MCP_GUIDE.ja.md](docs/ja/MCP_GUIDE.ja.md#診断の責務境界)を参照してください。

## プロファイルの検証と確認方法

通常運用では、まず `kelpie profile check <profile>` を使います。このコマンドは SSH 接続を行わず、profile file、JSON 構文、schema、認証参照、command provider、policy、user、pending backup を検証します。

```powershell
kelpie config check
kelpie profile check vps01
```

`kelpie profile check vps01` の出力例:

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

解決済みのプロファイル概要を安全に確認するには `kelpie profile show <profile>` を使います。

```powershell
kelpie profile show vps01
```

`kelpie profile show vps01` の出力例:

```text
Profile: vps01
Host: example.invalid
Port: 22
User: deploy
OS family: debian
Package manager: apt
Command OS family: debian
Command providers:
  CommonDiagnosticCommandProvider
  DebianAptCommandProvider
Capabilities:
  (empty list)
Roles:
  Safe
Effective mode: Safe
Allowed roots:
  /var/www  => @Read|@List|@Write|@CD
Special paths:
  **/.env  => Deny
Services:
  (empty list)
Users:
  deploy  => Safe
Authentication: privateKey
Private key: (configured)
```

## 初期化済みディレクトリで新しいプロファイルを作成する

`KelpieHome` がすでに初期化済みの場合は、`kelpie profile create <profile>` を使って、設定ファイルやディレクトリを作り直さずに新しいプロファイルのひな形を1つ追加できます。

```powershell
kelpie profile create vps02
```

対話形式の出力例:

```text
Create SSH profile template.
Press Enter to use the default value.
Host address [localhost]: example.org
Port [22]: 2222
SSH user [deploy]: ops
Authentication method (privateKey/password) [privateKey]: password
Password secret name [kelpie:vps02]: kelpie:vps02
OS family [debian]: ubuntu
Mode (ReadOnly/Safe/Maintenance/Expert) [Safe]: ReadOnly
Read-only root, '-' to omit [/var/log]: /var/log/nginx
Read-write root, '-' to omit [/var/www]: -
Deny pattern, '-' to omit [**/.env]: **/.secret
Created profile: vps02
Profile file: F:\Kelpie\profiles\vps02.json
```

`profiles\vps02.json` がすでに存在する場合、このコマンドは上書きするか確認します。上書きした場合、古いファイルは profile 変更を commit または rollback するまで `profiles\vps02.json.kelpie` として保持されます。

## コマンドラインオプション

Kelpie は、非対話 profile 作成、dry-run preview、runtime directory override、profile transaction の即コミット動作に関するコマンドラインオプションを提供します。

Dry-run、Silent モード、runtime directory override、即コミット profile 操作に関するオプションは [CLI_OPTIONS.md](CLI_OPTIONS.md) を参照してください。

## コントリビューション

コントリビューションを歓迎します。ドキュメント更新、誤字修正、テスト、範囲の小さい不具合修正などは、pull request として送ってください。

大きな変更、新しいコマンド、セキュリティに関係する挙動、SSH ポリシー変更、MCP ツール変更、互換性に影響する変更では、実装前に issue を開き、範囲と安全要件を相談してください。

Issue 報告時には、実ホスト名、実ユーザー名、パスワード、パスフレーズ、秘密鍵、本番プロファイルファイル、秘密情報を含み得る raw log を含めないでください。

## 連絡先

プロジェクトへの質問、コントリビューションの相談、秘密情報を含まないサポート問い合わせは、可能な限り GitHub Issues を利用してください。

直接連絡する場合は [shoe0604@akatsukisoft.com](mailto:shoe0604@akatsukisoft.com) へメールしてください。

## セキュリティ

KelpieSSH は、読み取り中心の診断と許可リスト方式の SSH コマンド実行から始める設計です。

実ホスト名、実ユーザー名、パスワード、パスフレーズ、秘密鍵、本番プロファイルファイルをコミットしないでください。実際の `profiles/*.json`、`keys/`、`dat/`、`logs/` は公開リポジトリの外に置いてください。

パスワード認証は実行時のみ扱います。CLI SSH コマンドは現在のコマンドプロセス用にパスワードを尋ね、`kelpiemcp password <profile>` は実行中の `KelpieMCPServer` プロセス内にのみパスワードを保持します。平文パスワードを JSON 設定ファイルに保存してはいけません。

脆弱性や秘密情報を含む詳細を公開 issue で報告しないでください。脆弱性報告と対応対象バージョンの方針は [docs/ja/SECURITY.ja.md](docs/ja/SECURITY.ja.md) を参照してください。

## ライセンス

KelpieSSH は Apache License 2.0 で公開されています。詳細は [LICENSE](LICENSE) を参照してください。

Copyright (c) 2026 Akatsukisoft.

Apache License 2.0 は、ライセンス条件にしたがって KelpieSSH の商用利用、改変、再配布、サブライセンス、特許利用を許可します。また、明示的な contributor patent grant と patent termination clause を含みます。

KelpiePro は有償のクローズドソースデスクトップ製品として計画されています。KelpiePro は、OSS 実装をクローズドソース製品リポジトリへフォークまたはコピーせず、KelpieSSH と Kelpie Core ライブラリを NuGet パッケージとして参照できます。このリポジトリは、OSS 実装とパッケージメタデータの上流ソースとして維持されます。

KelpieSSH パッケージまたはバイナリを KelpiePro と再配布する場合は、KelpieSSH の Apache License 2.0 表示と [docs/ja/THIRD_PARTY_NOTICES.ja.md](docs/ja/THIRD_PARTY_NOTICES.ja.md) に記載されたサードパーティ通知を、インストーラー、アプリケーションの about 画面、同梱ドキュメント、または同等の場所に含めてください。

現在の実行時依存関係の確認では、KelpieSSH の実行時パッケージに GPL、AGPL、LGPL、SSPL、Commons Clause、その他の非パーミッシブライセンスの依存関係は見つかっていません。パッケージバージョンを追加または更新した場合は、`THIRD_PARTY_NOTICES.md` を再確認してください。

## SSH プロファイル

SSH 接続プロファイルは、`KelpieHome/profiles` 配下にサーバーごと1つの JSON ファイルとして設定します。

実行時設定では既定の SSH プロファイルを指定しません。`kelpie open vps01` や MCP ツールの `profileName` のように、プロファイルを明示してください。

プロファイルは、KelpieSSH ライブラリが扱う保存済み SSH 接続設定です。`SshConnectionProfile`、`SshConnectionProfileFileLoader`、`SshConnectionProfileCatalog` は、CLI、MCP、KelpiePro などの製品から利用できるライブラリレベルのプロファイル API です。

Application API は `SshRemoteOperation` も受け取ります。`SshRemoteOperation` は接続先、認証情報、ポリシー、操作、オプションを持つ1回の SSH 操作を表します。保存済みプロファイルに依存せず、1回限りの操作を直接実行したい呼び出し元向けの入力です。既存の CLI と MCP のプロファイルローダーは、保存済みプロファイルを実行前に `SshRemoteOperation` へ変換できます。

プロファイル数の上限、エディション制限、ライセンス状態、広告、サポート、表示順、メモ、顧客データなどの製品固有概念は KelpieSSH の外側に置きます。KelpiePro は、OSS のプロファイルモデルにエディションポリシーを入れず、読み込むまたは表示する OSS プロファイル数を制限することで Free / Standard の差を実装できます。

各 `KelpieHome/profiles/*.json` ファイルが1つのプロファイルです。ファイル名がプロファイル名になるため、`profiles/vps01.json` はプロファイル `vps01` です。
公開サンプルプロファイルは `config_samples/servers/vps01.json` です。

プロファイルの項目詳細、サンプル、検証チェックリスト、トラブルシューティングは [docs/ja/PROFILE_GUIDE.ja.md](docs/ja/PROFILE_GUIDE.ja.md) を参照してください。
実際の `profiles/*.json` ファイルはコミットしないでください。

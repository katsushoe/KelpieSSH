# Windowsコマンドのインストール

英語版は [INSTALLATION.md](../INSTALLATION.md) を参照してください。

この手順では、現在のWindowsユーザーが任意の作業ディレクトリから`kelpie`と`kelpiemcp`を呼び出せるようにします。ZIPインストーラーと生成されるMSIは、ユーザー単位の`PATH`を登録します。両コマンドはインストール先の`bin`に、MCPサーバーは`bin/mcp`に配置されます。

## 前提条件

導入するリリースのWindows x64用パッケージを使用してください。現在のビルドスクリプトは、.NET 8のフレームワーク依存アプリケーションを作成します。この配布物にはWindows x64用の.NET 8ランタイムとASP.NET Core 8ランタイムが必要です。新しいメジャー版のランタイムだけでは、必要な.NET 8のフレームワークを満たしません。必要なフレームワークは、同梱の`*.runtimeconfig.json`に記載されています。

.NETが導入済みの場合は、利用可能なランタイムを確認します。

```powershell
dotnet --list-runtimes
```

現在のフレームワーク依存パッケージでは、`Microsoft.NETCore.App 8.x`と`Microsoft.AspNetCore.App 8.x`の両方を確認します。この前提条件と、コマンドを`PATH`へ登録する処理は別です。

## 配布物をインストールする

### ZIPインストーラー

`KelpieSSH-<version>-win-x64.zip`を一時ディレクトリへ展開します。`Install-Kelpie.ps1`と`bin`がある展開先でPowerShellを開き、次を実行します。

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-Kelpie.ps1
```

既定の配置先は`%LOCALAPPDATA%\Programs\KelpieSSH`です。スクリプトは実行ファイル・サンプル・文書をコピーし、`<配置先>\bin`を現在のユーザーの永続的な`PATH`へ登録して、`kelpie version`を確認します。SSHプロファイルの初期化やMCPサーバーの起動は行いません。不完全な配布物、展開元と配置先が同じ場合、配置先から動作するKelpieプロセスを検出した場合は拒否します。

配置先を変更する場合は、パスを引用符で囲んで指定します。

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-Kelpie.ps1 -InstallDirectory 'C:\Tools\KelpieSSH'
```

ユーザーの`PATH`を変更せず、配布物だけをコピーする場合は次のようにします。

```powershell
powershell -ExecutionPolicy Bypass -File .\Install-Kelpie.ps1 -InstallDirectory 'C:\Tools\KelpieSSH' -NoPath
```

`-NoPath`を指定した場合は、実行ファイルを絶対パスで呼び出すか、Windowsのユーザー環境変数設定で、その配置先の`bin`を登録します。登録するのは`bin`であり、`bin/mcp`やインストール先のルートではありません。

### MSIパッケージ

リリースにMSIがある場合は、そのリリースのインストーラーを実行します。現在のWiX定義は、`%LOCALAPPDATA%\Programs\KelpieSSH`へユーザー単位で配置し、`bin`をユーザーの`PATH`へ登録します。MSI版はWindowsの「インストールされているアプリ」で管理します。MSIを導入しても、ほかのWindowsアカウントからコマンドを呼び出せるようにはなりません。

## コマンドの解決先を確認する

インストール後は、ターミナルアプリケーションを閉じて開き直します。起動済みのターミナルやIDE内でタブだけを開き直すと、古い環境変数を引き継ぐ場合があります。コマンドを起動するGUI・AIクライアントも再起動します。

次の変数には、実際に選んだインストール先を設定します。

```powershell
$installRoot = Join-Path $env:LOCALAPPDATA 'Programs\KelpieSSH'
$binPath = Join-Path $installRoot 'bin'
Test-Path (Join-Path $binPath 'kelpie.exe')
Test-Path (Join-Path $binPath 'kelpiemcp.exe')
Test-Path (Join-Path $binPath 'mcp\KelpieMCPServer.exe')
```

3件とも`True`となることを確認します。配置先を変更した場合は、最初の代入を、たとえば`$installRoot = 'C:\Tools\KelpieSSH'`に置き換えます。

まず、選んだ実行ファイルを直接確認します。

```powershell
& (Join-Path $binPath 'kelpie.exe') version
& (Join-Path $binPath 'kelpiemcp.exe') version
```

次に、シェルがどのコマンドを選ぶか確認します。

```powershell
Get-Command kelpie, kelpiemcp -All | Select-Object CommandType, Name, Source, Path
where.exe kelpie
where.exe kelpiemcp
kelpie version
kelpiemcp version
```

両方の実行ファイルが、選んだ`bin`ディレクトリへ解決されることを確認します。両製品は個別にバージョンを管理するため、版番号が同じである必要はありません。`Get-Command -All`では、実行ファイルを隠すエイリアスや関数も確認できます。

保存されているユーザーの`PATH`は、変更せず次の方法で確認できます。

```powershell
[Environment]::GetEnvironmentVariable('Path', 'User') -split ';'
```

ターミナルを再起動する前に、意図した配置先を現在のPowerShellプロセスだけで選ぶ場合は、次のように先頭へ追加します。

```powershell
$env:Path = $binPath + [IO.Path]::PathSeparator + $env:Path
```

この操作は、ユーザーやシステムの環境変数を永続的に変更しません。その後、解決先の確認を繰り返します。

## Kelpie homeを初期化する

新規導入では、接続前にローカル設定を初期化して検証します。

```powershell
kelpie init
kelpie config check
```

Kelpie homeはインストールされたコマンドのディレクトリから決まるため、シェルの作業ディレクトリを変えても切り替わりません。標準構成では`bin`の親がhomeです。`config`、`profiles`、`keys`、`dat`、`logs`をその配下に置きます。ディレクトリの明示的な上書き指定は [CLI_OPTIONS.md](../../CLI_OPTIONS.md) を参照してください。

名前付きプロファイルの作成・検証は [PROFILE_GUIDE.ja.md](PROFILE_GUIDE.ja.md) を参照してください。`kelpie config check`はSSH接続を開かずにローカル設定を確認します。MCPクライアント設定とサーバー起動は [MCP_GUIDE.ja.md](MCP_GUIDE.ja.md) を参照してください。

## 更新または配置先を変更する

ZIP版は、配置先を使っているプロセスを停止し、別に展開した新しい配布物から、同じ`-InstallDirectory`を指定してインストーラーを再実行します。スクリプトは実行ファイル・文書をコピーし、既存の`config`、`profiles`、`keys`、`dat`、`logs`を削除しません。新しい配布物にない既存ファイルを自動で削除する処理もありません。

MCPサーバーをWindows Serviceとして動かしている場合は、更新前にそのサービス管理手順で停止し、更新後も同じ実行形態で起動します。ZIPインストーラー自身はサービスを停止・再設定しません。サービス操作は [COMMANDS.ja.md](COMMANDS.ja.md) を参照してください。

別のインストール先を選ぶと、別のhomeになります。利用者データの移行や、古い配置先の`PATH`登録削除は自動では行いません。必要な設定・プロファイル・鍵・永続データをバックアップして意図した場所へ移し、接続前にすべての実行ファイルの解決先を確認してください。異なる配置先のCLIとMCPサーバーを混在させないでください。

## インストールを削除する

MSI版はWindowsの「インストールされているアプリ」から削除します。生成されるMSIは、自ら登録したユーザーの`PATH`項目を管理し、アンインストール時にその項目を削除する定義になっています。

ZIP版は、対象のプロセスを停止して必要な利用者データを保存し、その配置先の`bin`と完全一致する項目だけをWindowsのユーザー`PATH`から削除します。不要になった実行ファイル・文書を削除してください。home全体を削除すると、その配下のプロファイル・鍵・永続データも消えます。ZIPインストーラーにアンインストール用スイッチはありません。

ターミナルやクライアントを開き直し、`Get-Command -All`と`where.exe`で、残したインストール先へ正しく解決されることを確認します。

## 問題の切り分け

| 症状 | 確認と対応 |
| :--- | :--- |
| 絶対パスの`version`は成功するが、コマンド名が見つからない | 保存されたユーザー`PATH`、選んだ`bin`、ターミナルやクライアントが引き継いだ環境を確認します。 |
| 古い版が起動する | `Get-Command -All`と`where.exe`の全結果を調べ、古い配置先の登録や、同名のエイリアス・関数を解決します。 |
| 実行ファイルが.NETの導入・更新を求める | `runtimeconfig.json`と`dotnet --list-runtimes`を照合し、現在の配布物に必要な.NET 8とASP.NET Core 8のフレームワークを用意します。 |
| インストーラーが実行中プロセスやファイル使用中を報告する | 必要に応じてWindows Serviceも含め、該当する配置先のプロセスを停止して、同じ配置先への導入をやり直します。 |
| ターミナルでは使えるがGUIクライアントでは使えない | クライアントを再起動するか、設定にインストール済み実行ファイルの絶対パスを指定します。 |
| 配置先を変えた後にプロファイルが見つからない | 初期化や接続の前に、選ばれた実行ファイルのディレクトリと、それから決まるKelpie homeを確認します。 |

配布物の構成と依存は [PACKAGES.ja.md](PACKAGES.ja.md) を参照してください。

# PACKAGES.ja.md Version
2026.06.18

# 変更履歴
- 2026.06.18
- 2026.06.17
- 2026.06.16
- 2026.06.14
- 2026.06.11

# KelpieSSH Packages

このファイルは、KelpieSSH の配布単位、内部 NuGet パッケージ、外部 NuGet 依存パッケージをまとめる公開ドキュメントです。

対象はリポジトリ内の直接参照です。NuGet が自動解決する推移的依存関係は、各パッケージのロックファイルまたは NuGet メタデータを正とします。

# パッケージ分類

| 分類 | 対象 | 用途 |
| :--- | :--- | :--- |
| 実行ファイル | `kelpie`, `kelpiemcp`, `KelpieMCPServer`, `kelpie-web-permission-helper` | 利用者が直接実行する CLI / MCP サーバー / SSH先 sudo helper |
| 内部 NuGet パッケージ | `Akatsukisoft.Kelpie.*` | KelpieSSH OSS と商用側から参照する共有ライブラリ |
| 外部 NuGet パッケージ | `Microsoft.Extensions.*`, `ModelContextProtocol`, `SSH.NET` など | 設定、MCP、SSH、テスト基盤 |
| テスト専用パッケージ | `xunit`, `FluentAssertions`, `coverlet.collector` など | 自動テストとカバレッジ収集 |

# 配布実行ファイル

| 実行ファイル | プロジェクト | バージョン | 主な責務 | 配置例 |
| :--- | :--- | :--- | :--- | :--- |
| `kelpie` | `src/KelpieClientCommand/KelpieClientCommand.csproj` | `0.2.0.0` | VPS 操作 CLI。初期化、プロファイル確認、診断、ログ取得、GUI/CLI モード切替を担当します。 | `KelpieHome/bin/kelpie.exe` |
| `kelpiemcp` | `src/KelpieServerCommand/KelpieServerCommand.csproj` | `0.2.0.0` | MCP サーバー制御 CLI。`start` / `stop` / `status` / `service register` / `service unregister` / `password` / `forget` を担当します。 | `KelpieHome/bin/kelpiemcp.exe` |
| `KelpieMCPServer` | `src/KelpieMCPServer/KelpieMCPServer.csproj` | `0.2.0.0` | Streamable HTTP MCP サーバー本体。Codex などの MCP クライアントへ SSH 診断ツールを公開します。 | `KelpieHome/bin/mcp/KelpieMCPServer.exe` |
| `kelpie-web-permission-helper` | `src/KelpieWebPermissionHelper/KelpieWebPermissionHelper.csproj` | `0.1.0.4` | SSH先に配置する sudo helper。Web公開ルート配下に限定して権限指定付き atomic write と owner / mode 変更を行います。 | `/usr/local/libexec/kelpie/kelpie-web-permission-helper` |

## `kelpie`

`kelpie` はユーザー向けの VPS 操作 CLI です。

主な機能:

- `kelpie init` による `KelpieHome` 初期化。
- `kelpie profiles` / `kelpie profile show` / `kelpie status` によるプロファイル確認。
- `kelpie diag` による SSH 診断コマンドの一括実行。
- `kelpie logs` によるサービスログ取得。
- `kelpie gui` / `kelpie cli` による既定クライアントモード切替。

主な参照:

- `KelpieServerCommand`
- `KelpieSSH.Application`
- `KelpieSSH.Infrastructure`
- `Kelpie.Core`
- `Microsoft.Extensions.Configuration.Json`

## `kelpiemcp`

`kelpiemcp` は `KelpieMCPServer` の起動、停止、状態確認、一時パスワード登録を行う制御 CLI です。

主な機能:

- `kelpiemcp start` による MCP サーバープロセス起動。
- `kelpiemcp stop` による NamedPipe 経由の停止要求。
- `kelpiemcp status` による稼働状態確認。
- `kelpiemcp service register` / `kelpiemcp service unregister` による Windows Service 登録管理。
- `kelpiemcp password <profile>` による起動中サーバーへの一時パスワード登録。
- `kelpiemcp forget <profile>` による一時パスワード削除。

主な参照:

- `Kelpie.Core`
- `Microsoft.Extensions.Configuration.Json`

## `KelpieMCPServer`

`KelpieMCPServer` は MCP クライアントへ SSH 診断・保守ツールを公開するローカルサーバーです。

主な機能:

- Streamable HTTP MCP エンドポイント `/mcp` の公開。
- ヘルスチェックエンドポイント `/health` の公開。
- 診断系 MCP ツールの公開。
- SSH プロファイルを使った安全な SSH コマンド実行。
- `profile_reload` による SSH profile catalog のオンデマンド再読み込み。
- `ssh_connection_close` / `ssh_logout` による MCP session cleanup。
- `kelpiemcp` からの NamedPipe 制御。

主な参照:

- `Kelpie.Core`
- `KelpieSSH.Application`
- `KelpieSSH.Infrastructure`
- `Microsoft.Extensions.Hosting`
- `Microsoft.Extensions.Hosting.WindowsServices`
- `ModelContextProtocol`
- `ModelContextProtocol.AspNetCore`

## `kelpie-web-permission-helper`

`kelpie-web-permission-helper` は SSH先の Linux サーバーへ配置する専用 sudo helper です。

主な機能:

- `web_change_owner` / `web_change_owner_recursive` から呼ばれる owner / group 変更。
- `web_change_mode` / `web_change_mode_recursive` から呼ばれる mode 変更。
- `web_file_write` の owner または mode 指定付き書き込みで、一時ファイルへ内容と権限を設定してから同一ディレクトリ内で置き換える atomic write。
- `realpath` 解決後の Web 公開ルート配下チェック。
- `root` / `0` 指定、world-writable mode、パストラバーサルの拒否。

主な参照:

- なし。

外部依存:

- NuGet 依存なし。
- 実行時は Linux libc の `realpath`、`getpwnam`、`getgrnam`、`chown`、`chmod`、`stat` を使用します。

# 内部 NuGet パッケージ

| PackageId | プロジェクト | バージョン | ライセンス | Packable | 主な責務 |
| :--- | :--- | :--- | :--- | :---: | :--- |
| `Akatsukisoft.Kelpie.Core` | `src/Kelpie.Core/Kelpie.Core.csproj` | `0.2.0.0-alpha` | Apache-2.0 | yes | Kelpie 共通ランタイム、設定解決、サーバー制御オプションなどを提供します。 |
| `Akatsukisoft.KelpieSSH.Domain` | `src/KelpieSSH.Domain/KelpieSSH.Domain.csproj` | `0.1.0.0-alpha` | Apache-2.0 | yes | SSH 実行結果、値オブジェクト、ドメイン表現を提供します。 |
| `Akatsukisoft.KelpieSSH.Application` | `src/KelpieSSH.Application/KelpieSSH.Application.csproj` | `0.2.0.0-alpha` | Apache-2.0 | yes | ユースケース、ポリシー、コマンド許可ロジック、SSH 抽象を提供します。 |
| `Akatsukisoft.KelpieSSH.Infrastructure` | `src/KelpieSSH.Infrastructure/KelpieSSH.Infrastructure.csproj` | `0.1.0.0-alpha` | Apache-2.0 | yes | SSH.NET を使った SSH 接続、コマンド実行、ShellStream 連携などのインフラ実装を提供します。 |

## `Akatsukisoft.Kelpie.Core`

Kelpie 製品群で共通利用する基盤ライブラリです。

含める内容:

- コマンド配置パス、`KelpieHome`、設定ファイルパスの解決。
- `kelpiemcp` と `KelpieMCPServer` が共有するサーバー設定。
- NamedPipe 制御やローカル実行に関係する共通モデル。

外部依存:

- `Microsoft.Extensions.Configuration.Abstractions`

## `Akatsukisoft.KelpieSSH.Domain`

KelpieSSH のドメイン表現を持つライブラリです。

含める内容:

- SSH 実行結果モデル。
- コマンド出力、終了コード、標準出力、標準エラーなどのドメイン表現。
- Application / Infrastructure が共有する値オブジェクト。

外部依存:

- なし。

## `Akatsukisoft.KelpieSSH.Application`

KelpieSSH のユースケースとポリシー判断を持つライブラリです。

含める内容:

- SSH コマンド抽象。
- 診断コマンド、パッケージ操作候補、ログ取得などのアプリケーションサービス。
- `Mode` / `Capabilities` / `AllowedRoots` / `SpecialPaths` による安全性評価。
- OS family と package manager に応じたコマンドプロバイダ選択。

内部依存:

- `Akatsukisoft.KelpieSSH.Domain`

外部依存:

- なし。

## `Akatsukisoft.KelpieSSH.Infrastructure`

KelpieSSH の外部接続実装を持つライブラリです。

含める内容:

- SSH.NET を使った SSH 接続。
- 秘密鍵認証、パスワード認証の接続処理。
- SSH コマンド実行。
- 対話シェルや PTY 連携に必要な ShellStream 処理。

内部依存:

- `Akatsukisoft.KelpieSSH.Application`
- `Akatsukisoft.Kelpie.Core`

外部依存:

- `SSH.NET`

# 外部 NuGet 依存

| パッケージ | バージョン | ライセンス | 利用プロジェクト | 用途 |
| :--- | :--- | :--- | :--- | :--- |
| `Microsoft.Extensions.Configuration.Abstractions` | `10.0.8` | MIT | `Kelpie.Core` | 設定読み取りの抽象 API。設定値をコードへ直書きせず、JSON や外部設定から扱うために使います。 |
| `Microsoft.Extensions.Configuration.Json` | `10.0.8` | MIT | `KelpieClientCommand`, `KelpieServerCommand` | `kelpie.json` / `kelpiemcp.json` などの JSON 設定ファイル読み取りに使います。 |
| `Microsoft.Extensions.Hosting` | `10.0.8` | MIT | `KelpieMCPServer` | MCP サーバーのホスト、DI、ライフサイクル管理に使います。 |
| `Microsoft.Extensions.Hosting.WindowsServices` | `10.0.8` | MIT | `KelpieMCPServer` | Windows Service として MCP サーバーをホストするために使います。 |
| `ModelContextProtocol` | `1.3.0` | Apache-2.0 | `KelpieMCPServer` | MCP ツール定義、MCP プロトコル連携に使います。 |
| `ModelContextProtocol.AspNetCore` | `1.3.0` | Apache-2.0 | `KelpieMCPServer` | ASP.NET Core 上で Streamable HTTP MCP エンドポイントを公開するために使います。 |
| `SSH.NET` | `2025.1.0` | MIT | `KelpieSSH.Infrastructure` | SSH 接続、コマンド実行、秘密鍵認証、ShellStream による対話シェル処理に使います。 |

推移的なランタイム依存を含む第三者ライセンス一覧は [THIRD_PARTY_NOTICES.ja.md](THIRD_PARTY_NOTICES.ja.md) を正本とします。
現時点のランタイム依存では、GPL / AGPL / LGPL / SSPL / Commons Clause など ClosedSource 有償製品に影響し得る copyleft / source-available 系ライセンスは検出していません。

## `Microsoft.Extensions.Configuration.Abstractions`

設定値を扱うための抽象 API です。

KelpieSSH では、実行環境で変わる値をコードへ直書きせず、設定ファイルや将来の設定プロバイダから取得するために使います。

## `Microsoft.Extensions.Configuration.Json`

JSON 設定ファイルを読み取るためのプロバイダです。

KelpieSSH では、`KelpieHome/config/kelpie.json` と `KelpieHome/config/kelpiemcp.json` の読み込みに使います。

## `Microsoft.Extensions.Hosting`

.NET の汎用ホスト基盤です。

KelpieSSH では、`KelpieMCPServer` の起動、停止、DI、ログ、ライフサイクル制御を整理するために使います。

## `Microsoft.Extensions.Hosting.WindowsServices`

.NET Generic Host を Windows Service として実行するための Microsoft 公式パッケージです。

KelpieSSH では、`KelpieMCPServer` を Windows Service として登録、起動できるようにするために使います。

## `ModelContextProtocol`

MCP の .NET 実装パッケージです。

KelpieSSH では、AI クライアントへ公開するツール定義、リクエスト処理、レスポンス生成の基盤として使います。

## `ModelContextProtocol.AspNetCore`

ASP.NET Core 経由で MCP サーバーを公開するための拡張パッケージです。

KelpieSSH では、ローカル HTTP サーバー上に Streamable HTTP MCP エンドポイントを公開するために使います。

## `SSH.NET`

.NET 向け SSH クライアントライブラリです。

KelpieSSH では、VPS へ SSH 接続し、安全性評価済みのコマンドを実行するために使います。秘密鍵認証、パスワード認証、コマンド実行、ShellStream を使った対話シェル処理の基盤です。

# テスト専用パッケージ

| パッケージ | バージョン | 利用プロジェクト | 用途 |
| :--- | :--- | :--- | :--- |
| `coverlet.collector` | `6.0.0` | `KelpieSSH.Application.Tests` | `dotnet test` 実行時のカバレッジ収集に使います。 |
| `FluentAssertions` | `8.10.0` | `KelpieSSH.Application.Tests` | テストの期待値検証を読みやすく書くために使います。 |
| `Microsoft.NET.Test.Sdk` | `17.8.0` | `KelpieSSH.Application.Tests` | .NET テスト実行基盤です。 |
| `xunit` | `2.5.3` | `KelpieSSH.Application.Tests` | ユニットテストフレームワークです。 |
| `xunit.runner.visualstudio` | `2.5.3` | `KelpieSSH.Application.Tests` | Visual Studio / `dotnet test` から xUnit テストを検出・実行するために使います。 |

# プロジェクト間依存

| プロジェクト | 参照先 |
| :--- | :--- |
| `KelpieClientCommand` | `KelpieServerCommand`, `KelpieSSH.Application`, `KelpieSSH.Infrastructure`, `Kelpie.Core` |
| `KelpieServerCommand` | `Kelpie.Core` |
| `KelpieMCPServer` | `Kelpie.Core`, `KelpieSSH.Application`, `KelpieSSH.Infrastructure` |
| `KelpieWebPermissionHelper` | なし |
| `KelpieSSH.Infrastructure` | `KelpieSSH.Application`, `Kelpie.Core` |
| `KelpieSSH.Application` | `KelpieSSH.Domain` |
| `KelpieSSH.Domain` | なし |
| `Kelpie.Core` | なし |
| `KelpieSSH.Application.Tests` | `KelpieSSH.Application`, `KelpieMCPServer`, `Kelpie.Core`, `KelpieServerCommand`, `KelpieWebPermissionHelper` |

# パッケージング方針

- 製品バージョンは Kelpie 全体で統一せず、製品またはライブラリごとの `.csproj` で管理します。
- NuGet 化するライブラリは `IsPackable=true` とし、`README.md`、Apache License 2.0、シンボルパッケージを含めます。
- 実行ファイルは NuGet ライブラリとは別に、MSI または zip 形式のバイナリ配布物として扱います。
- `KelpieMCPServer` は `kelpie` / `kelpiemcp` と DLL 競合しないように、手動配置では `bin/mcp` 配下へ分離して発行します。
- `kelpie-web-permission-helper` は SSH先の Linux サーバーへ self-contained 単一ファイルとして配置し、sudoers ではこの実行ファイルだけを NOPASSWD 許可します。
- テスト専用パッケージは公開ランタイム配布物へ含めません。

# 確認コマンド

パッケージと依存関係を確認する代表的なコマンドです。

```powershell
dotnet list package
dotnet build
dotnet test
dotnet publish src\KelpieWebPermissionHelper\KelpieWebPermissionHelper.csproj -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true
dotnet pack src\Kelpie.Core\Kelpie.Core.csproj -c Release
dotnet pack src\KelpieSSH.Domain\KelpieSSH.Domain.csproj -c Release
dotnet pack src\KelpieSSH.Application\KelpieSSH.Application.csproj -c Release
dotnet pack src\KelpieSSH.Infrastructure\KelpieSSH.Infrastructure.csproj -c Release
```

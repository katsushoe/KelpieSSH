# OSS公開範囲と配布方針

[英語版](../OSS_SCOPE.md)も参照してください。

## 公開範囲

KelpieSSHは[Apache License 2.0](../../LICENSE)で、kelpie CLI、kelpiemcp制御CLI、KelpieMCPServer、Kelpie.Core、KelpieSSH.Domain、KelpieSSH.Application、KelpieSSH.Infrastructureを公開します。任意導入のLinux向けKelpieWebPermissionHelper、それらのソース・テスト・ビルド/導入スクリプト・公開文書・機密値を除いた設定サンプルもOSSです。既存の制約付き保守操作はOSSに含み、診断専用に限定しません。OSと操作ごとの実対応は[provider一覧](PROVIDERS.ja.md)を参照してください。

KelpieDesktop、高度な商用UX、チーム管理、レポート、サポートは別のKelpieProで管理します。このリポジトリにある基本の安全制御・確認・認証・ログはOSSに残します。機能名だけを理由に既存実装を商用側へ移しません。全Linuxディストリビューション、runitサービス、任意root操作、MCPの無制限shell実行まで保証しません。

## 配布

| 成果物 | 方針 |
| :--- | :--- |
| ソース | 公開ソースリポジトリとリリース用ソースアーカイブ。ライセンスと依存表示を付けます。 |
| Windows x64 MSI | WiX Toolsetで作る標準バイナリ成果物。導入・更新・削除・版表示・ハッシュ照合を公開条件とします。 |
| Windows x64 ZIP | Install-Kelpie.ps1付きの補助配布物。標準MSIを置き換えません。 |
| ライブラリNuGet | KelpieProが基盤を参照する境界。公開レジストリへの配布は初期リリース範囲へ含めません。 |
| Linux特権helper | 別途ビルド・導入する任意構成。Windows CLI/MCPの導入だけで接続先のpolicyやsudoを設定しません。 |

製品版は独立管理し、リリース一式はCLI版で識別して収録製品の版をリリースノートへ記載します。実際の成果物、SHA-256、署名状態、前提条件、既知制限を明示します。ビルド可能なことだけで公開済み・検証済みとは扱いません。この方針は未署名公開を許可せず、特定リリースが入手可能だとも主張しません。

依存runtimeには必要な[第三者表示](THIRD_PARTY_NOTICES.ja.md)を付けます。非公開フィードや資格情報、実プロファイル、鍵、パスワード、利用者データ、ログ、内部管理文書、生成した試験資産は配布しません。導入・更新で既存の設定とデータを保持し、接続先の特権設定は管理者が明示的に行います。[導入手順](INSTALLATION.ja.md)と[パッケージ仕様](PACKAGES.ja.md)を参照してください。

## 貢献受付と公開操作

[貢献手順](CONTRIBUTING.ja.md)に従いIssue・PRを受け付けます。脆弱性は[セキュリティ方針](SECURITY.ja.md)の非公開窓口へ報告します。大きな変更は範囲相談とレビュー後に採否を判断します。

管理者がローカルで成果物を作成・検証してから公開します。tag作成、リリース公開、成果物アップロード、レジストリ配布は、製品・版・公開先を示した別途の公開承諾が必要です。文書更新、commit、push、ローカルビルドだけではリリース公開になりません。

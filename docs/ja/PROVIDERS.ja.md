# KelpieSSH provider一覧

[英語版](../../PROVIDERS.md)を対応する一覧として参照してください。providerは設定から選ぶ限定コマンドのモジュールであり、任意シェルを開放しません。

## 実装済みの種類

| provider | 対象 | 主な役割 |
| :--- | :--- | :--- |
| CommonDiagnosticCommandProvider | 非空OS family | Linux中心の共通診断・保守。systemctl/journalctl依存機能は別途前提が必要です。 |
| AlpineApkCommandProvider | alpine/apk | パッケージ8操作。 |
| ArchPacmanCommandProvider | arch・archlinux/pacman | パッケージ8操作。従来の未実装記載を訂正。 |
| DebianAptCommandProvider | Debian系/apt | パッケージ8操作。 |
| RhelDnfCommandProvider | RHEL系/dnf | パッケージ8操作。 |
| SuseZypperCommandProvider | SUSE系/zypper | パッケージ8操作。従来の未実装記載を訂正。 |
| VoidXbpsCommandProvider | void・voidlinux/xbps | パッケージ8操作を追加、隔離受入済み。[契約](VOID_XBPS.ja.md)。runit対応は含みません。 |
| DebianNginxCommandProvider・RhelNginxCommandProvider | 各OS系 | systemctlによる一般サービス操作とローカルHTTP確認。 |
| NginxServiceConfigCommandProvider・NginxConfigPathsProvider | nginx | 許可した設定・検査・変更履歴・ログ読取。 |
| WebPublicFileCommandProvider・WebPublicFileProvider | 設定したWebルート | 読取/一覧/検索/変更/権限。特権変更は専用helper境界を使います。 |
| SshConnectionProfileCatalog・ReloadingSshConnectionProfileCatalog | ローカル/信頼済みプロファイル | 設定読込・再読込・MCP信頼判定。 |
| NullSshPasswordProvider・InMemorySshPasswordSessionStore | プロセス内の認証状態 | パスワードなし/一時パスワード。平文を永続化しません。 |

## 未実装・限定範囲

legacy yum、Apache・PHP-FPM・MySQL/MariaDB・PostgreSQL設定provider、非systemdのサービス操作は今後の対象です。Nginx機密値の警告は完全なマスクを保証しません。パッケージprovider追加だけでOS全機能対応とは扱いません。

## 登録と追加

コマンドproviderはCommandProcessingProviderCatalog.CreateDefault、サービス設定providerはServiceConfigPathsProviderCatalog.CreateDefaultへ登録します。IAllowedCommandProvider/ICommandProcessingProviderと明示引数制約を実装し、CLI/MCP仕様・設定・シナリオ・試験結果を揃えます。Webルートと認証/プロファイルは既存の各専用providerで管理します。

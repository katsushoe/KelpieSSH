# DOCUMENTS.ja.md Version
2026.10.25

英語版は [DOCUMENTS.md](../../DOCUMENTS.md) を参照してください。

このファイルは、KelpieSSHでGit管理する公開文書の正本配置を示す日本語版です。

## 配置方針

- Git管理する公開文書は、リポジトリルート、`docs/`、または関連する機能のディレクトリに置きます。
- 日本語版は`docs/ja/`へ置きます。ただし`README.ja.md`は`README.md`と同じリポジトリルートに置き、同時に更新します。
- 非公開の文書やGit管理外の内部記録は、この公開一覧に含めません。
- 公開文書から非公開の文書、内部運用情報、秘密情報へリンクしません。
- 生成物は、Git管理する公開成果物である場合だけ、この一覧の対象にします。

## ディレクトリ

| パス | Git管理 | 用途 |
| :--- | :--- | :--- |
| `.` | はい | リポジトリルート。概要、解決ファイル、ライセンス、プロジェクト全体の設定。 |
| `docs/` | はい | 公開する補足文書。 |
| `docs/adr/` | はい | 公開するアーキテクチャ決定記録。 |
| `docs/ja/` | はい | 公開文書の日本語版。 |
| `config_samples/` | はい | 秘密情報を含まない設定例。 |
| `scripts/` | はい | ビルド、パッケージ作成、インストール、保守のスクリプト。 |
| `servers/` | はい | サーバー側の補助ファイルと例。 |
| `src/` | はい | 製品ソース。 |
| `tests/` | はい | 自動テスト。 |

## 文書一覧

| 文書 | 正本パス | Git管理 | 用途 |
| :--- | :--- | :--- | :--- |
| `README.md` | `README.md` | はい | セットアップ、使い方、安全性、ライセンスの概要。 |
| `README.ja.md` | `README.ja.md` | はい | 公開概要の日本語版。 |
| `DOCUMENTS.md` | `DOCUMENTS.md` | はい | ディレクトリと文書の正本配置。 |
| `DOCUMENTS.ja.md` | `docs/ja/DOCUMENTS.ja.md` | はい | 文書配置一覧の日本語版。 |
| `CLI_OPTIONS.md` | `CLI_OPTIONS.md` | はい | 非対話実行、変更予行、実行ディレクトリ指定、プロファイル変更処理のオプション。 |
| `INSTALLATION.md` | `docs/INSTALLATION.md` | はい | Windows導入、ランタイムの前提、ユーザーPATH、コマンド解決、更新、削除。 |
| `INSTALLATION.ja.md` | `docs/ja/INSTALLATION.ja.md` | はい | Windows導入とコマンド解決の日本語版。 |
| `COMMANDS.md` | `COMMANDS.md` | はい | 端末CLIコマンドのリファレンス。日本語版は`docs/ja/COMMANDS.ja.md`。 |
| `MCP_COMMANDS.md` | `MCP_COMMANDS.md` | はい | MCPツールのリファレンス。日本語版は`docs/ja/MCP_COMMANDS.ja.md`。 |
| `MCP_GUIDE.md` | `MCP_GUIDE.md` | はい | MCPサーバーの設定、配置、起動、利用方法。日本語版は`docs/ja/MCP_GUIDE.ja.md`。 |
| `CONFIG.md` | `CONFIG.md` | はい | 設定リファレンス。日本語版は`docs/ja/CONFIG.ja.md`。 |
| `PROFILE_GUIDE.md` | `PROFILE_GUIDE.md` | はい | SSHプロファイル設定の手引き。日本語版は`docs/ja/PROFILE_GUIDE.ja.md`。 |
| `PROVIDERS.md` | `PROVIDERS.md` | はい | 処理プロバイダーの対応状況と実装状態。 |
| `PACKAGES.md` | `PACKAGES.md` | はい | パッケージ、依存、取得元、更新方針。日本語版は`docs/ja/PACKAGES.ja.md`。 |
| `SECURITY.md` | `SECURITY.md` | はい | 安全性の方針と脆弱性報告。日本語版は`docs/ja/SECURITY.ja.md`。 |
| `THIRD_PARTY_NOTICES.md` | `THIRD_PARTY_NOTICES.md` | はい | 第三者依存のライセンス・通知。日本語版は`docs/ja/THIRD_PARTY_NOTICES.ja.md`。 |
| `LICENSE` | `LICENSE` | はい | Apache License 2.0の本文。 |
| `ADR-0001` | `docs/adr/ADR-0001-REMOTE-WEB-POLICY-MANAGEMENT.md` | はい | リモートのWebポリシー管理と安全境界。 |
| `ADR-0002` | `docs/adr/ADR-0002-PRIVILEGED-HELPER-UPDATE.md` | はい | 人間専用のヘルパー更新と特権コマンド境界。 |
| `ADR-0003` | `docs/adr/ADR-0003-STAGED-SERVER-DEPLOYMENT.md` | はい | 段階配備の状態、信頼境界、再試行、MCP固有のProvider契約。 |
| `ADR-0004` | `docs/adr/ADR-0004-TRUST-KEY-ACL-VALIDATION.md` | はい | Windowsの信頼鍵ACL検証と実行ユーザーの明示的な読み取り権限。 |
| `HOST_REBOOT.md` | `HOST_REBOOT.md` | はい | 通常再起動の設定、承認、永続状態、復旧。日本語版は`docs/ja/HOST_REBOOT.ja.md`。 |
| `ADR-0005` | `docs/adr/ADR-0005-HOST-REBOOT-VERIFICATION.md` | はい | 専用再起動操作、1回だけの送信、期限付き確認。 |

## 非公開文書の扱い

非公開の文書とGit管理外の内部文書は、この一覧の対象外です。非公開文書名、内部パス、実環境のメモ、未公開の運用情報、秘密情報、AI向け引き継ぎ情報を、この公開一覧へ追加しません。

## 更新規則

Git管理する公開文書を追加・削除・改名・移動した場合は、同じ変更で英語版と日本語版の一覧を更新します。`README.md`を更新した場合は、`README.ja.md`も同じ変更で更新し、セクションの対象範囲と公開向けのセットアップ情報を揃えます。

# COMMAND_SCENARIO.ja.md Version
2026.06.11

# 変更履歴
- 2026.06.11

# KelpieSSH コマンドテストシナリオ

このファイルは KelpieSSH のコマンドテスト限定シナリオを管理する。

対象は `kelpie`、`kelpiemcp`、および公開済みコマンドの実行確認です。ユニットテスト、内部APIテスト、MCPプロトコルの直接検証、GUI操作テスト、実機SSH詳細検証はこのファイルの対象外です。

# 前提条件

| 項目 | 前提 |
| :--- | :--- |
| 作業ブランチ | `develop` |
| 実行環境 | Windows PowerShell |
| 配置方式 | MSI または手動配置の `KelpieHome/bin` |
| コマンド配置 | `kelpie` と `kelpiemcp` が `PATH` から実行できること |
| 設定初期化 | `kelpie init` 済み、または空の一時 `KelpieHome` を使うこと |
| サンプルプロファイル | 実ホストを含まない `sample` または `vps01` を使うこと |
| 実SSHプロファイル | 実SSH接続テスト時だけ使用し、実ホスト名・実ユーザー名・秘密情報は結果表に書かないこと |

# 対象外

- `dotnet test` で実行するユニットテスト。
- MCPクライアントからのツール呼び出し検証。
- `KelpieDesktop` の画面操作。
- リモートOSの詳細な状態確認。
- 実パスワード、秘密鍵、実ホスト名、実ユーザー名の記録。

# シナリオ一覧

| ID | 分類 | コマンド | 目的 | 実SSH |
| :--- | :--- | :--- | :--- | :---: |
| `CT-001` | 基本 | `kelpie version` | バージョン表示を確認する。 | no |
| `CT-002` | 基本 | `kelpie help` | ヘルプ表示を確認する。 | no |
| `CT-003` | 初期化 | `kelpie init` | 既定の `KelpieHome` 構成を作成できることを確認する。 | no |
| `CT-004` | 初期化 | `kelpie init vps01` | 名前付きプロファイルを作成できることを確認する。 | no |
| `CT-005` | プロファイル | `kelpie profiles` | プロファイル一覧を表示できることを確認する。 | no |
| `CT-006` | プロファイル | `kelpie profile show vps01` | プロファイル概要を秘密情報なしで表示できることを確認する。 | no |
| `CT-007` | 状態 | `kelpie status vps01` | プロファイル概要と MCP サーバー状態を表示できることを確認する。 | no |
| `CT-008` | モード | `kelpie cli` | 既定モードを CLI に切り替えられることを確認する。 | no |
| `CT-009` | モード | `kelpie gui` | GUI モード切替コマンドの起動結果を確認する。 | no |
| `CT-010` | open/login | `kelpie open vps01` | open profile を保存できることを確認する。 | no |
| `CT-011` | open/login | `kelpie login` | open 済みプロファイルでログイン動作に進むことを確認する。 | yes |
| `CT-012` | open/login | `kelpie login --console` | console 起動オプションの結果を確認する。 | yes |
| `CT-013` | open/login | `kelpie login --desktop` | desktop 起動オプションの結果を確認する。 | no |
| `CT-014` | セッション | `kelpie sessions` | MCP サーバーの一時セッション一覧を表示できることを確認する。 | no |
| `CT-015` | セッション | `kelpie kill ssh-missing` | 存在しないセッションのエラーを確認する。 | no |
| `CT-016` | 診断 | `kelpie diag vps01` | 診断系SSHコマンドを一括実行できることを確認する。 | yes |
| `CT-017` | ログ | `kelpie logs vps01 <service>` | サービスログ取得を確認する。 | yes |
| `CT-018` | ログ | `kelpie logs vps01 bad;service` | 危険なサービス名を拒否することを確認する。 | no |
| `CT-019` | MCP制御 | `kelpiemcp status` | MCP サーバー状態を表示できることを確認する。 | no |
| `CT-020` | MCP制御 | `kelpiemcp start` | MCP サーバー起動要求を確認する。 | no |
| `CT-021` | MCP制御 | `kelpiemcp stop` | MCP サーバー停止要求を確認する。 | no |
| `CT-022` | MCP制御 | `kelpiemcp password vps01` | パスワード一時登録コマンドの動作を確認する。 | no |
| `CT-023` | MCP制御 | `kelpiemcp forget vps01` | パスワード一時削除コマンドの動作を確認する。 | no |
| `CT-024` | 互換 | `kelpiemcp login vps01` | 互換コマンドが `password` 相当で動くことを確認する。 | no |
| `CT-025` | 互換 | `kelpiemcp logout vps01` | 互換コマンドが `forget` 相当で動くことを確認する。 | no |

# 手順

## CT-001: `kelpie version`

1. `kelpie version` を実行する。
2. `kelpie x.y.z.w` 形式のバージョンが表示されることを確認する。

## CT-002: `kelpie help`

1. `kelpie help` を実行する。
2. 主要コマンドと `--version` / `--help` が表示されることを確認する。

## CT-003: `kelpie init`

1. 空の一時 `KelpieHome` 相当の配置で `kelpie init` を実行する。
2. `config`、`profiles`、`keys` が作成されることを確認する。
3. `kelpie.json`、`kelpiemcp.json`、`sample.json` が作成されることを確認する。

## CT-004: `kelpie init vps01`

1. `kelpie init vps01` を実行する。
2. `profiles/vps01.json` が作成されることを確認する。
3. 既存ファイルが上書きされないことを確認する。

## CT-005: `kelpie profiles`

1. `kelpie profiles` を実行する。
2. 設定済みプロファイル名が表示されることを確認する。

## CT-006: `kelpie profile show vps01`

1. `kelpie profile show vps01` を実行する。
2. host、port、user、mode、authentication が表示されることを確認する。
3. 秘密鍵パス、パスワード、秘密情報の実値が表示されないことを確認する。

## CT-007: `kelpie status vps01`

1. `kelpie status vps01` を実行する。
2. プロファイル概要と `KelpieMCPServer` の状態が表示されることを確認する。

## CT-008: `kelpie cli`

1. `kelpie cli` を実行する。
2. `Kelpie mode: cli` が表示されることを確認する。

## CT-009: `kelpie gui`

1. `kelpie gui` を実行する。
2. GUI 起動結果と `Kelpie mode: gui` が表示されることを確認する。

## CT-010: `kelpie open vps01`

1. `kelpie open vps01` を実行する。
2. `Opened profile: vps01` が表示されることを確認する。

## CT-011: `kelpie login`

1. `kelpie open vps01` を実行する。
2. `kelpie cli` を実行する。
3. `kelpie login` を実行する。
4. SSH 対話セッションへ接続されることを確認する。
5. `exit` で終了できることを確認する。

## CT-012: `kelpie login --console`

1. `kelpie open vps01` を実行する。
2. `kelpie login --console` を実行する。
3. console 起動結果が表示されることを確認する。

## CT-013: `kelpie login --desktop`

1. `kelpie open vps01` を実行する。
2. `kelpie login --desktop` を実行する。
3. desktop 起動結果が表示されることを確認する。

## CT-014: `kelpie sessions`

1. `kelpie sessions` を実行する。
2. セッション一覧、空表示、または MCP サーバー未起動表示が仕様どおりであることを確認する。

## CT-015: `kelpie kill ssh-missing`

1. `kelpie kill ssh-missing` を実行する。
2. 存在しないセッションのエラー、または MCP サーバー未起動表示が仕様どおりであることを確認する。

## CT-016: `kelpie diag vps01`

1. 実SSH接続可能な `vps01` プロファイルを用意する。
2. `kelpie diag vps01` を実行する。
3. `get_system_info`、`get_disk_usage`、`get_memory_usage`、`get_listening_ports`、`get_failed_services` の結果が表示されることを確認する。

## CT-017: `kelpie logs vps01 <service>`

1. 実SSH接続可能な `vps01` プロファイルを用意する。
2. `kelpie logs vps01 <service>` を実行する。
3. `tail_log` の結果が表示されることを確認する。

## CT-018: `kelpie logs vps01 bad;service`

1. `kelpie logs vps01 bad;service` を実行する。
2. 危険な引数として拒否されることを確認する。

## CT-019: `kelpiemcp status`

1. `kelpiemcp status` を実行する。
2. stopped または running の状態表示が仕様どおりであることを確認する。

## CT-020: `kelpiemcp start`

1. `kelpiemcp start` を実行する。
2. 起動要求または起動済み表示が仕様どおりであることを確認する。
3. `kelpiemcp status` で running になることを確認する。

## CT-021: `kelpiemcp stop`

1. `kelpiemcp stop` を実行する。
2. 停止要求または未起動表示が仕様どおりであることを確認する。
3. `kelpiemcp status` で stopped になることを確認する。

## CT-022: `kelpiemcp password vps01`

1. `kelpiemcp start` を実行する。
2. `kelpiemcp password vps01` を実行する。
3. パスワード入力後、一時保存完了が表示されることを確認する。
4. 入力したパスワードが画面やログに平文表示されないことを確認する。

## CT-023: `kelpiemcp forget vps01`

1. `kelpiemcp forget vps01` を実行する。
2. 一時パスワード削除完了、または未登録時の安全な結果が表示されることを確認する。

## CT-024: `kelpiemcp login vps01`

1. `kelpiemcp login vps01` を実行する。
2. `kelpiemcp password vps01` と同等の結果になることを確認する。

## CT-025: `kelpiemcp logout vps01`

1. `kelpiemcp logout vps01` を実行する。
2. `kelpiemcp forget vps01` と同等の結果になることを確認する。

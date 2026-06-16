# COMMAND_TEST.ja.md Version
2026.06.11

# 変更履歴
- 2026.06.11

# KelpieSSH コマンドテスト結果

このファイルは KelpieSSH のコマンドテスト結果を管理する。

コマンドテストシナリオは `COMMAND_SCENARIO.ja.md` を正とします。このファイルには結果サマリと実施結果だけを記録します。

# 結果記号

| 記号 | 意味 |
| :--- | :--- |
| `OK` | 期待結果どおり。 |
| `NG` | 期待結果と異なる。 |
| `SKIP` | 前提不足または今回は対象外。 |
| `PENDING` | 未実施。 |

# 結果サマリ

| 実施日 | 対象バージョン | 実施者 | 環境 | OK | NG | SKIP | PENDING | メモ |
| :--- | :--- | :--- | :--- | ---: | ---: | ---: | ---: | :--- |
| 2026.06.11 | `kelpie 0.1.3.3`, `kelpiemcp 0.1.1.2`, `KelpieMCPServer 0.1.4.2` | Codex | Windows PowerShell / `C:\Tmp\KelpieCommandTest` | 15 | 0 | 10 | 0 | NG修正後に自動実行可能な範囲を再実施。`dotnet build` 警告0・エラー0、`dotnet test` 139件成功。 |

# 実施結果

| ID | コマンド | 結果 | 実施日 | 対象バージョン | 実行環境 | メモ |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `CT-001` | `kelpie version` | `OK` | 2026.06.11 | `kelpie 0.1.3.3` | `C:\Tmp\KelpieCommandTest` | `kelpie 0.1.3.3` を確認。 |
| `CT-002` | `kelpie help` | `OK` | 2026.06.11 | `kelpie 0.1.3.3` | `C:\Tmp\KelpieCommandTest` | 主要コマンドとオプション表示を確認。 |
| `CT-003` | `kelpie init` | `OK` | 2026.06.11 | `kelpie 0.1.3.3` | `C:\Tmp\KelpieCommandTest` | `config`、`profiles`、`keys`、`dat` と初期ファイル生成を確認。 |
| `CT-004` | `kelpie init vps01` | `OK` | 2026.06.11 | `kelpie 0.1.3.3` | `C:\Tmp\KelpieCommandTest` | `profiles/vps01.json` 生成、既存設定ファイル非上書きを確認。 |
| `CT-005` | `kelpie profiles` | `OK` | 2026.06.11 | `kelpie 0.1.3.3` | `C:\Tmp\KelpieCommandTest` | `sample` と `vps01` の表示を確認。 |
| `CT-006` | `kelpie profile show vps01` | `OK` | 2026.06.11 | `kelpie 0.1.3.3` | `C:\Tmp\KelpieCommandTest` | プロファイル概要を表示し、秘密情報の実値が出ないことを確認。 |
| `CT-007` | `kelpie status vps01` | `OK` | 2026.06.11 | `kelpie 0.1.3.3` | `C:\Tmp\KelpieCommandTest` | `kelpiemcp.json` の `Server` 設定を参照し、プロファイル概要と stopped 状態を表示することを確認。 |
| `CT-008` | `kelpie cli` | `OK` | 2026.06.11 | `kelpie 0.1.3.3` | `C:\Tmp\KelpieCommandTest` | `Kelpie mode: cli` を確認。 |
| `CT-009` | `kelpie gui` | `SKIP` | 2026.06.11 | `kelpie 0.1.3.3` | `C:\Tmp\KelpieCommandTest` | GUI起動を伴うため自動実行対象外。 |
| `CT-010` | `kelpie open vps01` | `OK` | 2026.06.11 | `kelpie 0.1.3.3` | `C:\Tmp\KelpieCommandTest` | `Opened profile: vps01` を確認。 |
| `CT-011` | `kelpie login` | `SKIP` | 2026.06.11 | `kelpie 0.1.3.3` | `C:\Tmp\KelpieCommandTest` | 実SSH接続と対話セッションが必要。 |
| `CT-012` | `kelpie login --console` | `SKIP` | 2026.06.11 | `kelpie 0.1.3.3` | `C:\Tmp\KelpieCommandTest` | 実SSH接続と別コンソール起動が必要。 |
| `CT-013` | `kelpie login --desktop` | `SKIP` | 2026.06.11 | `kelpie 0.1.3.3` | `C:\Tmp\KelpieCommandTest` | GUI起動を伴うため自動実行対象外。 |
| `CT-014` | `kelpie sessions` | `OK` | 2026.06.11 | `kelpie 0.1.3.3` | `C:\Tmp\KelpieCommandTest` | MCPサーバー停止中表示を確認。 |
| `CT-015` | `kelpie kill ssh-missing` | `OK` | 2026.06.11 | `kelpie 0.1.3.3` | `C:\Tmp\KelpieCommandTest` | MCPサーバー停止中表示を確認。 |
| `CT-016` | `kelpie diag vps01` | `SKIP` | 2026.06.11 | `kelpie 0.1.3.3` | `C:\Tmp\KelpieCommandTest` | 実SSH接続が必要。 |
| `CT-017` | `kelpie logs vps01 <service>` | `SKIP` | 2026.06.11 | `kelpie 0.1.3.3` | `C:\Tmp\KelpieCommandTest` | 実SSH接続と実サービス名が必要。 |
| `CT-018` | `kelpie logs vps01 bad;service` | `OK` | 2026.06.11 | `kelpie 0.1.3.3` | `C:\Tmp\KelpieCommandTest` | 危険引数を拒否し、スタックトレースを出さずにエラーメッセージだけ表示することを確認。 |
| `CT-019` | `kelpiemcp status` | `OK` | 2026.06.11 | `kelpiemcp 0.1.1.2` | `C:\Tmp\KelpieCommandTest` | running と stopped の状態表示を確認。 |
| `CT-020` | `kelpiemcp start` | `OK` | 2026.06.11 | `kelpiemcp 0.1.1.2`, `KelpieMCPServer 0.1.4.2` | `C:\Tmp\KelpieCommandTest` | 起動済み状態で `KelpieMCPServer is already running.` を確認。 |
| `CT-021` | `kelpiemcp stop` | `OK` | 2026.06.11 | `kelpiemcp 0.1.1.2`, `KelpieMCPServer 0.1.4.2` | `C:\Tmp\KelpieCommandTest` | `KelpieMCPServer stop requested.` 後、`status` で stopped を確認。 |
| `CT-022` | `kelpiemcp password vps01` | `SKIP` | 2026.06.11 | `kelpiemcp 0.1.1.2` | `C:\Tmp\KelpieCommandTest` | パスワード入力を伴うため自動実行対象外。 |
| `CT-023` | `kelpiemcp forget vps01` | `SKIP` | 2026.06.11 | `kelpiemcp 0.1.1.2` | `C:\Tmp\KelpieCommandTest` | `vps01` が秘密鍵プロファイルで `PasswordSecretName` 未設定。 |
| `CT-024` | `kelpiemcp login vps01` | `SKIP` | 2026.06.11 | `kelpiemcp 0.1.1.2` | `C:\Tmp\KelpieCommandTest` | 互換コマンドだがパスワード入力を伴うため自動実行対象外。 |
| `CT-025` | `kelpiemcp logout vps01` | `SKIP` | 2026.06.11 | `kelpiemcp 0.1.1.2` | `C:\Tmp\KelpieCommandTest` | `vps01` が秘密鍵プロファイルで `PasswordSecretName` 未設定。 |

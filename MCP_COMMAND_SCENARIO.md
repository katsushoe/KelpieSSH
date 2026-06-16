# MCP_COMMAND_SCENARIO.md Version
2026.06.16

# 変更履歴
- 2026.06.14
- 2026.06.15
- 2026.06.16

# KelpieSSH MCP callable tool テストシナリオ

このファイルは KelpieSSH の MCP callable tool テスト限定シナリオを管理する。

MCP callable tool の仕様は `MCP_COMMANDS.md` を正とします。このファイルは、MCPクライアント経由で `KelpieMCPServer` に公開された tool を呼び出し、引数、確認文字列、戻り値、安全制御、実機SSH連携が仕様どおりに動くことを確認するためのシナリオです。

通常のターミナルで実行する `kelpie` / `kelpiemcp` CLI コマンドのテストシナリオは `COMMAND_SCENARIO.md` を正とします。

# 前提条件

| 項目 | 前提 |
| :--- | :--- |
| 作業ブランチ | `develop` |
| MCPサーバー | `KelpieMCPServer` が起動済みで、MCPクライアントから callable tool を呼び出せること |
| MCP疎通 | `kelpie_ping` が成功すること |
| 配置方式 | MSI または手動配置の `KelpieHome/bin/mcp` |
| プロファイル | `KelpieHome/profiles/<profile>.json` に検証用プロファイルがあること |
| サンプルプロファイル | 実ホストを含まない確認では `sample` または安全な一時プロファイルを使うこと |
| 実SSHプロファイル | 実SSH接続テスト時だけ `vps01` などの実機プロファイルを使用し、実ホスト名・実ユーザー名・秘密情報は結果表に書かないこと |
| 変更系tool | 空または不一致の `confirmation` で非破壊確認してから、必要最小限の確認済み変更だけを行うこと |
| Service config変更 | provider が許可した安全な設定ファイルだけを対象にし、write 後は必ず `service_config_file_commit` または `service_config_file_rollback` で閉じること |
| Web file変更 | 既存 site root や本番ファイルを直接上書きせず、provider が許可した安全なテスト用パスがある場合のみ実変更すること |

# 対象外

- `dotnet test` で実行するユニットテスト。
- 通常のターミナルから実行する `kelpie` / `kelpiemcp` CLI コマンド検証。
- `KelpieDesktop` の画面操作。
- MCPプロトコルそのものの低レベル互換性検証。
- raw shell による準備、後始末、または確認。
- 実パスワード、秘密鍵、実ホスト名、実ユーザー名の記録。
- 本番ファイル、既存 site root、重要設定ファイルへの破壊的変更。

# 結果判定

| 判定 | 意味 |
| :--- | :--- |
| OK | 期待結果どおり。 |
| NG | 期待結果と異なる。 |
| SKIP | 前提不足、安全な対象なし、または今回は対象外。 |
| PENDING | 未実施。 |

# シナリオ一覧

| ID | 分類 | MCP tool | 目的 | 実SSH | 実変更 |
| :--- | :--- | :--- | :--- | :---: | :---: |
| `MT-001` | Server health | `kelpie_ping` | MCPサーバー疎通を確認する。 | no | no |
| `MT-002` | Local diagnostics | `get_system_info` | MCPサーバー実行ホストのシステム情報を取得できることを確認する。 | no | no |
| `MT-003` | Local diagnostics | `get_disk_usage` | MCPサーバー実行ホストのディスク使用量を取得できることを確認する。 | no | no |
| `MT-004` | Local diagnostics | `get_memory_usage` | MCPサーバープロセスのメモリ使用量を取得できることを確認する。 | no | no |
| `MT-005` | Local diagnostics | `get_listening_ports` | MCPサーバー実行ホストの listening port を取得できることを確認する。 | no | no |
| `MT-075` | Capability discovery | `ssh_get_capabilities` | profile ごとの command / tool 可否を取得できることを確認する。 | yes | no |
| `MT-120` | Capability discovery | `get_target_inventory` | profile ごとの OS / helper / software inventory を一括取得できることを確認する。 | yes | no |
| `MT-006` | SSH diagnostics | `ssh_get_system_info` | 実SSH先のシステム情報を取得できることを確認する。 | yes | no |
| `MT-059` | SSH diagnostics | `ssh_get_os_release` | 実SSH先の `/etc/os-release` を取得できることを確認する。 | yes | no |
| `MT-060` | SSH diagnostics | `ssh_get_uptime` | 実SSH先の uptime を取得できることを確認する。 | yes | no |
| `MT-007` | SSH diagnostics | `ssh_get_disk_usage` | 実SSH先のディスク使用量を取得できることを確認する。 | yes | no |
| `MT-008` | SSH diagnostics | `ssh_get_memory_usage` | 実SSH先のメモリ使用量を取得できることを確認する。 | yes | no |
| `MT-063` | SSH diagnostics | `ssh_get_process_summary` | 実SSH先の CPU またはメモリ使用量上位プロセスを取得できることを確認する。 | yes | no |
| `MT-064` | SSH diagnostics | `ssh_get_inode_usage` | 実SSH先の inode 使用量を取得できることを確認する。 | yes | no |
| `MT-065` | SSH diagnostics | `ssh_get_mounts` | 実SSH先の mount 状態を取得できることを確認する。 | yes | no |
| `MT-067` | SSH diagnostics | `ssh_get_network_addresses` | 実SSH先の network interface/address 情報を取得できることを確認する。 | yes | no |
| `MT-068` | SSH diagnostics | `ssh_get_routes` | 実SSH先の route 情報を取得できることを確認する。 | yes | no |
| `MT-071` | SSH diagnostics | `ssh_get_dns_config` | 実SSH先の DNS resolver 設定を取得できることを確認する。 | yes | no |
| `MT-083` | SSH diagnostics | `ssh_cron_list` | system cron と現在 user の crontab を bounded list で確認できることを確認する。 | yes | no |
| `MT-084` | SSH diagnostics | `ssh_cron_validate` | cron 式、実行 user、command、log path を実変更なしで検証できることを確認する。 | yes | no |
| `MT-085` | SSH diagnostics | `ssh_cert_inspect` | 証明書 file の issuer、subject、有効期限、SAN を確認できることを確認する。 | yes | no |
| `MT-086` | SSH diagnostics | `ssh_cert_expiry_check` | 証明書の期限接近を指定日数で確認できることを確認する。 | yes | no |
| `MT-087` | SSH diagnostics | `ssh_user_list` | ローカル user 一覧を bounded list で確認できることを確認する。 | yes | no |
| `MT-088` | SSH diagnostics | `ssh_user_info` | 1 user の UID、GID、group、home、shell を確認できることを確認する。 | yes | no |
| `MT-089` | SSH diagnostics | `ssh_group_list` | ローカル group 一覧を bounded list で確認できることを確認する。 | yes | no |
| `MT-090` | SSH diagnostics | `ssh_group_info` | 1 group の GID と member を確認できることを確認する。 | yes | no |
| `MT-091` | SSH diagnostics | `ssh_sudoers_check` | user / group の sudoers evidence を本文なしで要約できることを確認する。 | yes | no |
| `MT-092` | SSH diagnostics | `ssh_user_usage_check` | user / group が service、cron owner、主要 path owner として使われているか要約できることを確認する。 | yes | no |
| `MT-093` | SSH diagnostics | `ssh_user_file_ownership_check` | 許可 root 配下で user / group owner の bounded scan ができることを確認する。 | yes | no |
| `MT-094` | SSH diagnostics | `ssh_user_service_usage_check` | systemd service の User / Group / SupplementaryGroups 参照を確認できることを確認する。 | yes | no |
| `MT-095` | SSH diagnostics | `ssh_service_residual_config_check` | service 関連の unit / config / log / data / runtime path の残存有無を確認できることを確認する。 | yes | no |
| `MT-096` | SSH diagnostics | `ssh_support_report_collect` | 秘密情報を除外した bounded support report を収集できることを確認する。 | yes | no |
| `MT-103` | SSH diagnostics | `ssh_cron_check_write` | cron 変更前チェックが実変更なしで確認文字列と rollback 可否を返すことを確認する。 | yes | no |
| `MT-109` | SSH diagnostics | `ssh_cron_write` | 空または不一致 confirmation で cron write が実行されないことを確認する。 | yes | no |
| `MT-110` | SSH diagnostics | `ssh_cron_rollback` | 安全な検証対象がある場合のみ、確認済み cron rollback を実行できることを確認する。 | yes | yes |
| `MT-104` | SSH diagnostics | `ssh_user_check_group_change` | user group 変更前チェックが実変更なしで差分と確認文字列を返すことを確認する。 | yes | no |
| `MT-111` | SSH diagnostics | `ssh_user_apply_group_change` | 空または不一致 confirmation で user group 変更が実行されないことを確認する。 | yes | no |
| `MT-112` | SSH diagnostics | `ssh_user_rollback_group_change` | 安全な検証対象がある場合のみ、確認済み user group rollback を実行できることを確認する。 | yes | yes |
| `MT-105` | SSH diagnostics | `ssh_user_check_permission_change` | user 権限変更前チェックが実変更なしで現在値、候補、確認文字列を返すことを確認する。 | yes | no |
| `MT-113` | SSH diagnostics | `ssh_user_apply_permission_change` | 空または不一致 confirmation で user 権限変更が実行されないことを確認する。 | yes | no |
| `MT-114` | SSH diagnostics | `ssh_user_rollback_permission_change` | 安全な検証対象がある場合のみ、確認済み user 権限 rollback を実行できることを確認する。 | yes | yes |
| `MT-106` | SSH diagnostics | `ssh_firewall_status` | firewall 状態を rule 本文なしで要約できることを確認する。 | yes | no |
| `MT-115` | SSH diagnostics | `ssh_firewall_check_rule` | firewall rule 変更前チェックが実変更なしで確認文字列を返すことを確認する。 | yes | no |
| `MT-116` | SSH diagnostics | `ssh_firewall_apply_rule` | 空または不一致 confirmation で firewall rule 変更が実行されないことを確認する。 | yes | no |
| `MT-107` | SSH diagnostics | `ssh_backup_plan_check` | backup 対象範囲を実 backup なしで bounded scan できることを確認する。 | yes | no |
| `MT-117` | SSH diagnostics | `ssh_backup_run` | 空または不一致 confirmation で backup 作成が実行されないことを確認する。 | yes | no |
| `MT-108` | SSH diagnostics | `ssh_backup_verify` | backup archive を entry 本文なしで検証できることを確認する。 | yes | no |
| `MT-118` | SSH diagnostics | `ssh_audit_verify` | audit log の hash chain を本文なしで検証できることを確認する。 | yes | no |
| `MT-119` | SSH diagnostics | `ssh_audit_export` | audit log から秘密情報を除外した summary を export できることを確認する。 | yes | no |
| `MT-072` | SSH diagnostics | `ssh_check_http_local` | 実SSH先の localhost HTTP 応答を port 制限付きで確認できることを確認する。 | yes | no |
| `MT-073` | SSH diagnostics | `ssh_check_tcp_connect_local` | 実SSH先の localhost TCP connect を port 制限付きで確認できることを確認する。 | yes | no |
| `MT-009` | SSH diagnostics | `ssh_get_listening_ports` | 実SSH先の listening port を取得できることを確認する。 | yes | no |
| `MT-010` | SSH diagnostics | `ssh_get_failed_services` | 実SSH先の failed services を取得できることを確認する。 | yes | no |
| `MT-066` | SSH diagnostics | `ssh_get_journal_recent` | 実SSH先の直近 journal を行数制限付きで取得できることを確認する。 | yes | no |
| `MT-011` | SSH diagnostics | `ssh_tail_log` | 許可された systemd service のログを取得できることを確認する。 | yes | no |
| `MT-012` | SSH diagnostics | `ssh_run_allowed_command` | 許可済み commandName だけを実行できることを確認する。 | yes | no |
| `MT-013` | SSH diagnostics | `ssh_run_allowed_command` | 未許可または危険な commandName が拒否されることを確認する。 | yes | no |
| `MT-014` | SSH terminal | `ssh_terminal_open` | PTY付き対話ターミナルを開けることを確認する。 | yes | no |
| `MT-015` | SSH terminal | `ssh_terminal_send` | 開いた対話ターミナルへ入力を送れることを確認する。 | yes | no |
| `MT-016` | SSH terminal | `ssh_terminal_snapshot` | 対話ターミナルの現在画面を取得できることを確認する。 | yes | no |
| `MT-017` | SSH terminal | `ssh_terminal_close` | 対話ターミナルを閉じられることを確認する。 | yes | no |
| `MT-018` | Package operations | `ssh_pkg_check_updates` | package update 候補を確認できることを確認する。 | yes | no |
| `MT-074` | Package operations | `ssh_pkg_info` | package の installed 状態、候補 version、repository 情報を確認できることを確認する。 | yes | no |
| `MT-076` | Package operations | `ssh_pkg_search` | package 候補を検索できることを確認する。 | yes | no |
| `MT-077` | Package operations | `ssh_pkg_list_installed` | installed package を filter / limit 付きで確認できることを確認する。 | yes | no |
| `MT-019` | Package operations | `ssh_pkg_simulate_install` | install dry-run を実行できることを確認する。 | yes | no |
| `MT-020` | Package operations | `ssh_pkg_install` | install の確認要求だけが返ることを確認する。 | yes | no |
| `MT-021` | Package operations | `ssh_pkg_install_confirmed` | 空または不一致 confirmation で install が実行されないことを確認する。 | yes | no |
| `MT-022` | Package operations | `ssh_pkg_install_confirmed` | 安全な検証対象がある場合のみ、確認済み install を実行できることを確認する。 | yes | yes |
| `MT-023` | Package operations | `ssh_pkg_simulate_remove` | remove dry-run を実行できることを確認する。 | yes | no |
| `MT-024` | Package operations | `ssh_pkg_remove` | remove の確認要求だけが返ることを確認する。 | yes | no |
| `MT-025` | Service operations | `ssh_service_enable_now` | 空または不一致 confirmation で service enable が実行されないことを確認する。 | yes | no |
| `MT-061` | Service operations | `ssh_service_status` | systemd service status を取得できることを確認する。 | yes | no |
| `MT-078` | Service operations | `ssh_service_is_active` | systemd service の active 状態を簡易確認できることを確認する。 | yes | no |
| `MT-079` | Service operations | `ssh_service_is_enabled` | systemd service の enable 状態を簡易確認できることを確認する。 | yes | no |
| `MT-062` | Service operations | `ssh_list_services` | systemd service 一覧を state filter と limit 付きで取得できることを確認する。 | yes | no |
| `MT-026` | Service operations | `ssh_service_enable_now` | 安全な検証対象がある場合のみ、確認済み service enable を実行できることを確認する。 | yes | yes |
| `MT-027` | Service operations | `ssh_service_reload` | 空または不一致 confirmation で service reload が実行されないことを確認する。 | yes | no |
| `MT-028` | Service operations | `ssh_service_reload` | 安全な検証対象がある場合のみ、確認済み service reload を実行できることを確認する。 | yes | yes |
| `MT-097` | Service operations | `ssh_service_restart` | 空または不一致 confirmation で service restart が実行されないことを確認する。 | yes | no |
| `MT-098` | Service operations | `ssh_service_restart` | 安全な検証対象がある場合のみ、確認済み service restart を実行できることを確認する。 | yes | yes |
| `MT-099` | Service operations | `ssh_service_stop` | 空または不一致 confirmation で service stop が実行されないことを確認する。 | yes | no |
| `MT-100` | Service operations | `ssh_service_stop` | 安全な検証対象がある場合のみ、確認済み service stop を実行できることを確認する。 | yes | yes |
| `MT-101` | Service operations | `ssh_service_disable` | 空または不一致 confirmation で service disable が実行されないことを確認する。 | yes | no |
| `MT-102` | Service operations | `ssh_service_disable` | 安全な検証対象がある場合のみ、確認済み service disable を実行できることを確認する。 | yes | yes |
| `MT-029` | Service config/logs | `service_config_paths` | provider が管理する設定ファイルパスを取得できることを確認する。 | yes | no |
| `MT-030` | Service config/logs | `service_config_file_read` | provider が許可した設定ファイルを読み取れることを確認する。 | yes | no |
| `MT-031` | Service config/logs | `service_config_file_write` | 通常 target の空 confirmation で確認要求が返ることを確認する。 | yes | no |
| `MT-032` | Service config/logs | `service_config_file_write` | indexed target の空 confirmation で確認要求が返ることを確認する。 | yes | no |
| `MT-033` | Service config/logs | `service_config_file_write` | Nginx の indexed `replace` が安全な対象で動くことを確認する。 | yes | yes |
| `MT-034` | Service config/logs | `service_config_file_write` | Nginx の indexed `delete` が安全な対象で動くことを確認する。 | yes | yes |
| `MT-035` | Service config/logs | `service_config_file_write` | Nginx の `insert` が安全な対象で動くことを確認する。 | yes | yes |
| `MT-036` | Service config/logs | `service_config_test` | 空 confirmation で確認要求が返ることを確認する。 | yes | no |
| `MT-037` | Service config/logs | `service_config_test` | 確認済み設定テストが成功または妥当な失敗を返すことを確認する。 | yes | no |
| `MT-038` | Service config/logs | `service_config_file_rollback` | 空 confirmation で確認要求が返ることを確認する。 | yes | no |
| `MT-039` | Service config/logs | `service_config_file_rollback` | write 後に backup から復元し、backup が削除されることを確認する。 | yes | yes |
| `MT-040` | Service config/logs | `service_config_file_commit` | 空 confirmation で確認要求が返ることを確認する。 | yes | no |
| `MT-041` | Service config/logs | `service_config_file_commit` | write 後に backup だけを削除できることを確認する。 | yes | yes |
| `MT-042` | Service config/logs | `service_logfile_read` | provider が許可したログを読み取れることを確認する。 | yes | no |
| `MT-043` | Web files | `web_file_read` | provider が許可した Web ファイルを読み取れることを確認する。 | yes | no |
| `MT-081` | Web files | `web_file_head` | provider が許可した Web ファイルの先頭だけを bounded read できることを確認する。 | yes | no |
| `MT-082` | Web files | `web_file_tail` | provider が許可した Web ファイルの末尾だけを bounded read できることを確認する。 | yes | no |
| `MT-044` | Web files | `web_file_write` | 空 confirmation で確認要求が返ることを確認する。 | yes | no |
| `MT-045` | Web files | `web_file_write` | 安全なテスト用パスへ確認済み書き込みできることを確認する。 | yes | yes |
| `MT-046` | Web files | `web_change_owner` | 空 confirmation で確認要求が返ることを確認する。 | yes | no |
| `MT-047` | Web files | `web_change_owner` | 安全なテスト用パスで owner/group を変更できることを確認する。 | yes | yes |
| `MT-048` | Web files | `web_change_owner_recursive` | 空 confirmation で確認要求が返ることを確認する。 | yes | no |
| `MT-049` | Web files | `web_change_owner_recursive` | 安全なテスト用ディレクトリで recursive owner/group 変更できることを確認する。 | yes | yes |
| `MT-050` | Web files | `web_change_mode` | 空 confirmation で確認要求が返ることを確認する。 | yes | no |
| `MT-051` | Web files | `web_change_mode` | 安全なテスト用パスで mode を変更できることを確認する。 | yes | yes |
| `MT-052` | Web files | `web_change_mode_recursive` | 空 confirmation で確認要求が返ることを確認する。 | yes | no |
| `MT-053` | Web files | `web_change_mode_recursive` | 安全なテスト用ディレクトリで recursive mode 変更できることを確認する。 | yes | yes |
| `MT-054` | Service config/logs | `service_config_file_check_read` | provider が許可した設定ファイルの読み取り可否を本文なしで確認する。 | yes | no |
| `MT-055` | Service config/logs | `service_config_file_check_write` | provider が許可した設定ファイルへの限定編集可否を実変更なしで確認する。 | yes | no |
| `MT-056` | Web files | `web_file_list` | provider が許可した Web ディレクトリ一覧を取得できることを確認する。 | yes | no |
| `MT-069` | Web files | `web_file_search_name` | provider が許可した Web ディレクトリ範囲を file name glob で検索できることを確認する。 | yes | no |
| `MT-080` | Web files | `web_file_search_text` | provider が許可した Web テキストファイルを bounded scan で検索できることを確認する。 | yes | no |
| `MT-057` | Web files | `web_file_stat` | provider が許可した Web path のメタデータを取得できることを確認する。 | yes | no |
| `MT-058` | Web files | `web_file_check_write` | Web ファイル書き込み可否を実変更なしで確認できることを確認する。 | yes | no |
| `MT-070` | Web files | `web_file_check_permissions` | Web 権限変更前に対象 path と confirmation を実変更なしで確認できることを確認する。 | yes | no |

# 手順

## MT-001: `kelpie_ping`

1. MCPクライアントから `kelpie_ping` を引数なしで呼び出す。
2. `KelpieSSH MCP server is running.` が返ることを確認する。

## MT-002: `get_system_info`

1. `get_system_info` を引数なしで呼び出す。
2. `MachineName`、`OSDescription`、`FrameworkDescription`、`ProcessId`、`BaseDirectory` が返ることを確認する。
3. 例外やスタックトレースが返らないことを確認する。

## MT-003: `get_disk_usage`

1. `get_disk_usage` を引数なしで呼び出す。
2. `Drives` に ready drive の情報が返ることを確認する。

## MT-004: `get_memory_usage`

1. `get_memory_usage` を引数なしで呼び出す。
2. `WorkingSetBytes`、`PrivateMemoryBytes`、`ManagedTotalBytes` が返ることを確認する。

## MT-005: `get_listening_ports`

1. `get_listening_ports` を引数なしで呼び出す。
2. `ExitCode` と `Ports` が返ることを確認する。

## MT-075: `ssh_get_capabilities`

1. 実SSH接続可能な `profileName` を用意する。
2. `ssh_get_capabilities` を呼び出す。
3. `ProbeCommandName: get_os_release`、`ProbeExitCode: 0` または妥当な失敗が返ることを確認する。
4. `Commands` と `Tools` に profile ごとの command / MCP tool 可否が返ることを確認する。
5. 実ホスト名、実ユーザー名、IP、秘密情報を結果に記録しない。

## MT-120: `get_target_inventory`

1. 実SSH接続可能な `profileName` を用意する。
2. `get_target_inventory` を呼び出す。
3. `Os` に family、name、version、packageManager が返ることを確認する。
4. `Helpers` に Python、PHP、kelpie-web-permission-helper が返ることを確認する。
5. `Software` に Node.js、npm、Composer、Git、curl、wget、OpenSSL、systemctl、nginx、firewall-cmd が返ることを確認する。
6. 個別 command の失敗は tool 全体の失敗ではなく、該当 item の `Status: Not Available`、`ExitCode`、`Detail` として返ることを確認する。
7. file 本文、秘密鍵、パスワード、raw log body は返らないことを確認する。

## MT-006: `ssh_get_system_info`

1. 実SSH接続可能な `profileName` を用意する。
2. `ssh_get_system_info` を呼び出す。
3. SSH先のシステム情報が `SshToolResult` として返ることを確認する。

## MT-059: `ssh_get_os_release`

1. 実SSH接続可能な `profileName` を用意する。
2. `ssh_get_os_release` を呼び出す。
3. `/etc/os-release` の内容が `SshToolResult` として返ることを確認する。

## MT-060: `ssh_get_uptime`

1. 実SSH接続可能な `profileName` を用意する。
2. `ssh_get_uptime` を呼び出す。
3. uptime と load average が `SshToolResult` として返ることを確認する。

## MT-007: `ssh_get_disk_usage`

1. `ssh_get_disk_usage` を呼び出す。
2. SSH先のディスク使用量が返ることを確認する。

## MT-008: `ssh_get_memory_usage`

1. `ssh_get_memory_usage` を呼び出す。
2. SSH先のメモリ使用量が返ることを確認する。

## MT-063: `ssh_get_process_summary`

1. `sortBy` に `cpu` または `memory`、`limit` に小さめの数値を指定して `ssh_get_process_summary` を呼び出す。
2. `CommandName: get_process_summary`、`ExitCode: 0`、プロセス概要が返ることを確認する。
3. 不正な `sortBy` または `limit` が拒否されることを確認する。

## MT-064: `ssh_get_inode_usage`

1. `ssh_get_inode_usage` を呼び出す。
2. `df -ih` の結果が `SshToolResult` として返ることを確認する。

## MT-065: `ssh_get_mounts`

1. `ssh_get_mounts` を呼び出す。
2. `findmnt` の結果が `SshToolResult` として返ることを確認する。

## MT-067: `ssh_get_network_addresses`

1. `ssh_get_network_addresses` を呼び出す。
2. `commandName: get_network_addresses`、`commandText: ip addr show`、`ExitCode: 0` または対象OSの妥当な失敗が返ることを確認する。
3. 実IPなどの環境情報をテスト結果ファイルへ転記しない。

## MT-068: `ssh_get_routes`

1. `ssh_get_routes` を呼び出す。
2. `commandName: get_routes`、`commandText: ip route show`、`ExitCode: 0` または対象OSの妥当な失敗が返ることを確認する。
3. gateway / private address などの環境情報をテスト結果ファイルへ転記しない。

## MT-071: `ssh_get_dns_config`

1. `ssh_get_dns_config` を呼び出す。
2. `commandName: get_dns_config`、`commandText: cat /etc/resolv.conf`、`ExitCode: 0` または対象OSの妥当な失敗が返ることを確認する。
3. DNS server、search domain などの環境情報をテスト結果ファイルへ転記しない。

## MT-083: `ssh_cron_list`

1. `limit` に小さめの数値を指定して `ssh_cron_list` を呼び出す。
2. `commandName: cron_list`、`ExitCode: 0` または cron 未設定時の妥当な空結果が返ることを確認する。
3. 出力行数が `limit` を超えないことを確認する。
4. 不正な `limit` が SSH 実行前に拒否されることを確認する。

## MT-084: `ssh_cron_validate`

1. 安全な `cronExpression`、`runUser`、`command`、`logPath` を指定して `ssh_cron_validate` を呼び出す。
2. `commandName: cron_validate`、妥当な入力で `valid=true` と `ExitCode: 0` が返ることを確認する。
3. 危険文字を含む `command`、5 field ではない cron 式、`/var/log/` 外の `logPath` が SSH 実行前または wrapper 内で拒否されることを確認する。
4. cron file が変更されないことを確認する。

## MT-085: `ssh_cert_inspect`

1. provider が許可する証明書 path を指定して `ssh_cert_inspect` を呼び出す。
2. `commandName: cert_inspect`、`commandText` に `openssl x509`、issuer、subject、有効期限、SAN または証明書未存在時の妥当な失敗が返ることを確認する。
3. 許可外 path が SSH 実行前に拒否されることを確認する。
4. 秘密鍵本文や証明書本文そのものを結果に記録しない。

## MT-086: `ssh_cert_expiry_check`

1. provider が許可する証明書 path と `days` を指定して `ssh_cert_expiry_check` を呼び出す。
2. `commandName: cert_expiry_check`、`commandText` に `openssl x509 -checkend` 相当、openssl 仕様どおりの `ExitCode` が返ることを確認する。
3. 許可外 path と範囲外 `days` が SSH 実行前に拒否されることを確認する。

## MT-087: `ssh_user_list`

1. `limit` に小さめの数値を指定して `ssh_user_list` を呼び出す。
2. `commandName: user_list`、`ExitCode: 0`、UID / GID / home / shell を含む user 一覧が返ることを確認する。
3. 出力行数が `limit` を超えないことを確認する。
4. password hash や shadow 情報が返らないことを確認する。

## MT-088: `ssh_user_info`

1. 安全な既存 user 名を指定して `ssh_user_info` を呼び出す。
2. `commandName: user_info`、`ExitCode: 0`、UID / GID / primary group / supplementary groups / home / shell が返ることを確認する。
3. password hash や shadow 情報が返らないことを確認する。
4. 危険文字を含む user 名が SSH 実行前に拒否されることを確認する。

## MT-089: `ssh_group_list`

1. `limit` に小さめの数値を指定して `ssh_group_list` を呼び出す。
2. `commandName: group_list`、`ExitCode: 0`、GID と member 名を含む group 一覧が返ることを確認する。
3. 出力行数が `limit` を超えないことを確認する。
4. 範囲外 `limit` が SSH 実行前に拒否されることを確認する。

## MT-090: `ssh_group_info`

1. 安全な既存 group 名を指定して `ssh_group_info` を呼び出す。
2. `commandName: group_info`、`ExitCode: 0`、GID と member 名が返ることを確認する。
3. 危険文字を含む group 名が SSH 実行前に拒否されることを確認する。

## MT-091: `ssh_sudoers_check`

1. `targetType` に `user` または `group`、`name` に安全な既存 user / group 名を指定して `ssh_sudoers_check` を呼び出す。
2. `commandName: sudoers_check`、`ExitCode: 0`、存在有無、admin group 該当、sudoers match 件数、match source path が返ることを確認する。
3. sudoers file 本文や rule 内容が返らないことを確認する。
4. 不正な `targetType` や危険文字を含む `name` が SSH 実行前に拒否されることを確認する。

## MT-092: `ssh_user_usage_check`

1. `targetType` に `user` または `group`、`name` に安全な既存 user / group 名、`limit` に小さめの数値を指定して `ssh_user_usage_check` を呼び出す。
2. `commandName: user_usage_check`、`ExitCode: 0`、存在有無、service match 件数、cron owner match 件数、file ownership match 件数が返ることを確認する。
3. service unit 本文、cron 本文、file 本文が返らないことを確認する。
4. 不正な `targetType`、危険文字入り `name`、範囲外 `limit` が拒否されることを確認する。

## MT-093: `ssh_user_file_ownership_check`

1. `targetType` に `user` または `group`、`name` に安全な既存 user / group 名、`scanRoot` に許可 root、`depth` と `limit` に小さめの数値を指定して `ssh_user_file_ownership_check` を呼び出す。
2. `commandName: user_file_ownership_check`、`ExitCode: 0`、scan root、scan 件数、match 件数、owner/group summary が返ることを確認する。
3. file 本文が返らず、symlink を追跡しないことを確認する。
4. 許可外 `scanRoot`、不正な `depth`、範囲外 `limit`、危険文字入り `name` が拒否されることを確認する。

## MT-094: `ssh_user_service_usage_check`

1. `targetType` に `user` または `group`、`name` に安全な既存 user / group 名、`limit` に小さめの数値を指定して `ssh_user_service_usage_check` を呼び出す。
2. `commandName: user_service_usage_check`、`ExitCode: 0`、確認 service 数、User / Group / SupplementaryGroups の match 件数が返ることを確認する。
3. unit file 本文が返らないことを確認する。
4. 不正な `targetType`、危険文字入り `name`、範囲外 `limit` が拒否されることを確認する。

## MT-095: `ssh_service_residual_config_check`

1. 安全な service 名を指定して `ssh_service_residual_config_check` を呼び出す。
2. `commandName: service_residual_config_check`、`ExitCode: 0`、unit / config / log / data / runtime path の存在有無と種別が返ることを確認する。
3. 設定 file 本文や log 本文が返らないことを確認する。
4. 危険文字入り `service`、範囲外 `limit` が拒否されることを確認する。

## MT-096: `ssh_support_report_collect`

1. `limit` に小さめの数値を指定して `ssh_support_report_collect` を呼び出す。
2. `commandName: support_report_collect`、`ExitCode: 0`、kernel、OS release、uptime、memory、disk、failed service summary が返ることを確認する。
3. host 名、IP address、SSH user 名、DNS server、file 本文、設定本文、log 本文が返らないことを確認する。
4. 範囲外 `limit` が拒否されることを確認する。

## MT-103: `ssh_cron_check_write`

1. 安全な `targetType`、`runUser`、cron 式、command、`/var/log/` 配下 log path を指定して呼び出す。
2. `commandName: cron_check_write`、`requiresConfirmation=true`、`confirmation=cron_write:<targetType>:<runUser>`、`rollbackSupported=true` が返ることを確認する。
3. cron file や user crontab が変更されないことを確認する。
4. 不正な cron 式、危険文字入り command、`/var/log/` 外 log path が SSH 実行前に拒否されることを確認する。

## MT-109: `ssh_cron_write` 空 confirmation

1. 安全な cron 入力で `confirmation` を空または不一致にして `ssh_cron_write` を実行する。
2. `Confirmation is required: cron_write:<targetType>:<runUser>` 相当の確認要求が返ることを確認する。
3. cron file / user crontab が変更されていないことを確認する。

## MT-110: `ssh_cron_rollback` 確認済み実行

1. 安全な検証対象がある場合のみ、`ssh_cron_check_write` で確認文字列を取得する。
2. `ssh_cron_write` を確認済みで実行し、backup が作成されることを確認する。
3. `ssh_cron_rollback` を `cron_rollback:<targetType>:<runUser>` で実行し、`restored=true` が返ることを確認する。
4. cron 本文、既存 crontab 本文、実ユーザー名は結果に記録しない。

## MT-104: `ssh_user_check_group_change`

1. 既存 user と safe な group list、`append` または `replace` を指定して呼び出す。
2. `commandName: user_check_group_change`、group 差分、`confirmation=user_apply_group_change:<user>:<mode>:<groups>` が返ることを確認する。
3. user group が変更されないことを確認する。
4. 危険文字入り user/group、許可外 group list 形式、不正 mode が SSH 実行前に拒否されることを確認する。

## MT-111: `ssh_user_apply_group_change` 空 confirmation

1. 安全な既存 user / group を指定し、`confirmation` を空または不一致にして `ssh_user_apply_group_change` を実行する。
2. `Confirmation is required: user_apply_group_change:<user>:<mode>:<groups>` 相当の確認要求が返ることを確認する。
3. 実 user group が変更されていないことを確認する。

## MT-112: `ssh_user_rollback_group_change` 確認済み実行

1. 安全な検証対象がある場合のみ、`ssh_user_check_group_change` で確認文字列を取得する。
2. `ssh_user_apply_group_change` を確認済みで実行し、backup が作成されることを確認する。
3. `ssh_user_rollback_group_change` を `user_rollback_group_change:<user>` で実行し、`restored=true` が返ることを確認する。
4. 実 user 名、group 名、backup 本文は結果に記録しない。

## MT-105: `ssh_user_check_permission_change`

1. 既存 user、許可 shell path、`login`、`sudo` を指定して呼び出す。
2. `commandName: user_check_permission_change`、現在 shell、変更候補、sudoers evidence 件数、confirmation が返ることを確認する。
3. shell、login、sudoers が変更されないことを確認する。
4. sudoers 本文や rule 内容が返らないことを確認する。

## MT-113: `ssh_user_apply_permission_change` 空 confirmation

1. 安全な既存 user、許可 shell path、`login`、`sudo` を指定し、`confirmation` を空または不一致にして `ssh_user_apply_permission_change` を実行する。
2. `Confirmation is required: user_apply_permission_change:<user>:<shell>:<login>:<sudo>` 相当の確認要求が返ることを確認する。
3. `exitCode: -1` で、shell、login、sudoers が変更されないことを確認する。
4. sudoers 本文、backup 本文、実 user 名は結果に記録しない。

## MT-114: `ssh_user_rollback_permission_change`

1. 安全な検証対象がある場合のみ、`ssh_user_check_permission_change` で確認文字列を取得する。
2. `ssh_user_apply_permission_change` を確認済みで実行し、backup が作成されることを確認する。
3. `ssh_user_rollback_permission_change` を `user_rollback_permission_change:<user>` で実行し、`restored=true` が返ることを確認する。
4. backup なしの場合は `backupExists=false` の妥当な失敗になることを確認する。
5. 実 user 名、sudoers 本文、backup 本文は結果に記録しない。

## MT-106: `ssh_firewall_status`

1. 実SSH接続可能な profile を指定して呼び出す。
2. `commandName: firewall_status`、firewalld / ufw の有無と状態要約が返ることを確認する。
3. firewall rule 本文、送信元 IP、port rule 詳細が返らないことを確認する。

## MT-115: `ssh_firewall_check_rule`

1. 安全な `action`, `target`, `value`, `zone`, `permanent` を指定して呼び出す。
2. `commandName: firewall_check_rule`、firewalld の有無、対象 rule の存在状態、`confirmation=firewall_apply_rule:<action>:<target>:<value>:<zone>:<permanent>` が返ることを確認する。
3. firewall rule が変更されないことを確認する。
4. 不正な `action`, `target`, `value`, `zone`, `permanent` が SSH 実行前または wrapper 内で拒否されることを確認する。

## MT-116: `ssh_firewall_apply_rule`

1. 安全な検証対象がある場合のみ、`ssh_firewall_check_rule` で確認文字列を取得する。
2. `confirmation` を空または不一致にして `ssh_firewall_apply_rule` を実行し、`ExitCode: -1` で拒否されることを確認する。
3. 確認済み実変更は専用の一時 rule だけで行い、実施後に逆操作で戻す。
4. firewalld がない環境では、確認済み実変更は SKIP とし、非破壊確認だけを OK とする。

## MT-107: `ssh_backup_plan_check`

1. 許可 root、`depth`、`limit` を指定して呼び出す。
2. `commandName: backup_plan_check`、存在有無、scan 件数、推定 byte 数、`confirmation=backup_run:<scanRoot>` が返ることを確認する。
3. backup archive が作成されないこと、file 本文や file 名一覧が返らないことを確認する。
4. 許可外 root、不正 `depth`、範囲外 `limit` が SSH 実行前に拒否されることを確認する。

## MT-117: `ssh_backup_run`

1. 安全な許可 root、`depth`、`limit` を指定し、`confirmation` を空または不一致にして呼び出す。
2. `Confirmation is required: backup_run:<scanRoot>` 相当の確認要求が返り、backup archive が作成されないことを確認する。
3. 安全な検証対象がある場合のみ確認済みで実行し、`backupPath`, `entriesAdded`, `archiveReadable=true` が返ることを確認する。
4. file 本文、file 名一覧、archive entry 名は結果に記録しない。

## MT-108: `ssh_backup_verify`

1. `/var/backups/kelpie` 配下の許可 archive path を指定して呼び出す。
2. `commandName: backup_verify`、存在有無、size、archive readable 判定が返ることを確認する。
3. archive entry 名や file 本文が返らないことを確認する。
4. 許可外 path が SSH 実行前に拒否されることを確認する。

## MT-118: `ssh_audit_verify`

1. 許可された `/var/log/kelpie/*.log` path と `limit` を指定して呼び出す。
2. `commandName: audit_verify`、存在有無、確認 record 数、hash field 欠落数、chain break 数が返ることを確認する。
3. log 本文、実ホスト名、IP、実ユーザー名、秘密情報が返らないことを確認する。
4. 許可外 path と範囲外 `limit` が SSH 実行前に拒否されることを確認する。

## MT-119: `ssh_audit_export`

1. 許可された `/var/log/kelpie/*.log` path と `limit` を指定して呼び出す。
2. `commandName: audit_export`、`exportVersion`, sanitized `record`, `records` が返ることを確認する。
3. allowlist 外の raw log body、host 名、IP、SSH user 名、秘密情報が返らないことを確認する。
4. 許可外 path と範囲外 `limit` が SSH 実行前に拒否されることを確認する。

## MT-072: `ssh_check_http_local`

1. `port` に安全な localhost HTTP port を指定して `ssh_check_http_local` を呼び出す。
2. `commandName: check_http_local`、`ExitCode: 0` または service 未起動時の妥当な失敗が返ることを確認する。
3. 不正な `port` が拒否されることを確認する。
4. 任意 host / 任意 URL を指定できないことを確認する。

## MT-073: `ssh_check_tcp_connect_local`

1. `port` に安全な localhost TCP port を指定して `ssh_check_tcp_connect_local` を呼び出す。
2. `commandName: check_tcp_connect_local`、`ExitCode: 0` または port 未待受時の妥当な失敗が返ることを確認する。
3. 不正な `port` が拒否されることを確認する。
4. 任意 host を指定できないことを確認する。

## MT-009: `ssh_get_listening_ports`

1. `ssh_get_listening_ports` を呼び出す。
2. SSH先の listening port 情報が返ることを確認する。

## MT-010: `ssh_get_failed_services`

1. `ssh_get_failed_services` を呼び出す。
2. failed services の一覧、または0件を示す結果が返ることを確認する。

## MT-066: `ssh_get_journal_recent`

1. `lines` に小さめの数値を指定して `ssh_get_journal_recent` を呼び出す。
2. `CommandName: get_journal_recent`、`ExitCode: 0` または権限・journal 状態に応じた妥当な失敗が返ることを確認する。
3. 不正な `lines` が拒否されることを確認する。

## MT-011: `ssh_tail_log`

1. `service` に安全な systemd service 名を指定して `ssh_tail_log` を呼び出す。
2. 指定行数以内のログ、または権限・サービス状態に応じた妥当なエラーが返ることを確認する。
3. `service` に危険文字を含む値を指定した場合、拒否されることを確認する。

## MT-012: `ssh_run_allowed_command`

1. `commandName` に許可済み診断コマンドを指定して `ssh_run_allowed_command` を呼び出す。
2. 許可済みコマンドの結果が返ることを確認する。

## MT-013: `ssh_run_allowed_command` 拒否確認

1. `commandName` に未許可または危険なコマンド名を指定する。
2. 実行されず、拒否エラーが返ることを確認する。

## MT-014: `ssh_terminal_open`

1. `ssh_terminal_open` を呼び出す。
2. `Handle`、`ProfileName`、`Text`、`Connected` が返ることを確認する。
3. 以後の terminal 系シナリオで `Handle` を使う。

## MT-015: `ssh_terminal_send`

1. `ssh_terminal_open` で取得した `Handle` を指定する。
2. 読み取り専用の安全な入力を送る。
3. 更新後の画面スナップショットが返ることを確認する。

## MT-016: `ssh_terminal_snapshot`

1. `ssh_terminal_open` で取得した `Handle` を指定する。
2. 現在画面の `Text` が返ることを確認する。

## MT-017: `ssh_terminal_close`

1. `ssh_terminal_open` で取得した `Handle` を指定する。
2. `Closed` 相当の成功結果が返ることを確認する。
3. close 後に同じ handle を使った操作が妥当な失敗になることを確認する。

## MT-018: `ssh_pkg_check_updates`

1. `ssh_pkg_check_updates` を呼び出す。
2. update 候補、または0件を示す結果が返ることを確認する。
3. 実 install や update が行われないことを確認する。

## MT-074: `ssh_pkg_info`

1. 安全な package 名を指定して `ssh_pkg_info` を呼び出す。
2. `commandName: pkg_info`、対象 package の installed 状態、候補 version、repository 情報、または未検出時の妥当な失敗が返ることを確認する。
3. 実 install、update、remove が行われないことを確認する。
4. 不正な package 名が拒否されることを確認する。

## MT-076: `ssh_pkg_search`

1. 安全な query と小さめの `limit` を指定して `ssh_pkg_search` を呼び出す。
2. `commandName: pkg_search`、`ExitCode: 0` または package manager の妥当な失敗が返ることを確認する。
3. 出力行数が `limit` を超えないことを確認する。
4. 実 install、update、remove が行われないことを確認する。
5. 不正な query または limit が拒否されることを確認する。

## MT-077: `ssh_pkg_list_installed`

1. 安全な filter と小さめの `limit` を指定して `ssh_pkg_list_installed` を呼び出す。
2. `commandName: pkg_list_installed`、`ExitCode: 0` または package manager の妥当な失敗が返ることを確認する。
3. 出力行数が `limit` を超えないことを確認する。
4. 実 install、update、remove が行われないことを確認する。
5. 不正な filter または limit が拒否されることを確認する。

## MT-019: `ssh_pkg_simulate_install`

1. 安全な package 名を指定して `ssh_pkg_simulate_install` を呼び出す。
2. dry-run 結果が返ることを確認する。
3. package 状態が変更されないことを確認する。

## MT-020: `ssh_pkg_install`

1. 安全な package 名を指定して `ssh_pkg_install` を呼び出す。
2. 実 install ではなく確認要求だけが返ることを確認する。

## MT-021: `ssh_pkg_install_confirmed` 空 confirmation

1. `confirmation` を空または不一致にして `ssh_pkg_install_confirmed` を呼び出す。
2. `Confirmation is required: pkg_install:<package>` が返ることを確認する。
3. install が実行されないことを確認する。

## MT-022: `ssh_pkg_install_confirmed` 確認済み実行

1. 実 install してよい安全な package がある場合のみ実施する。
2. `confirmation` に `pkg_install:<package>` を指定して呼び出す。
3. install 結果が返ることを確認する。
4. 安全な後始末手順がある場合だけ後始末する。

## MT-023: `ssh_pkg_simulate_remove`

1. 安全な package 名を指定して `ssh_pkg_simulate_remove` を呼び出す。
2. dry-run 結果が返ることを確認する。
3. package 状態が変更されないことを確認する。

## MT-024: `ssh_pkg_remove`

1. 安全な package 名を指定して `ssh_pkg_remove` を呼び出す。
2. 実 remove ではなく確認要求だけが返ることを確認する。

## MT-025: `ssh_service_enable_now` 空 confirmation

1. 安全な service 名を指定し、`confirmation` を空または不一致にして呼び出す。
2. `Confirmation is required: service_enable_now:<service>` 相当の確認要求が返ることを確認する。
3. service 状態が変更されないことを確認する。

## MT-061: `ssh_service_status`

1. 安全な service 名を指定して `ssh_service_status` を呼び出す。
2. `service_status` の `SshToolResult` が返ることを確認する。
3. service 名に危険文字を含む値を指定した場合、拒否されることを確認する。

## MT-078: `ssh_service_is_active`

1. 安全な service 名を指定して `ssh_service_is_active` を呼び出す。
2. `service_is_active` の `SshToolResult` が返ることを確認する。
3. active の場合は `ExitCode: 0`、inactive / failed / unknown の場合は systemctl の仕様に沿った非0終了コードが返ることを確認する。
4. service 名に危険文字を含む値を指定した場合、拒否されることを確認する。

## MT-062: `ssh_list_services`

1. `state` と `limit` を指定して `ssh_list_services` を呼び出す。
2. `list_services` の `SshToolResult` が返ることを確認する。
3. `limit` を超える行が返らないことを確認する。
4. `state` または `limit` に不正値を指定した場合、拒否されることを確認する。

## MT-026: `ssh_service_enable_now` 確認済み実行

1. enable してよい安全な service がある場合のみ実施する。
2. 正しい `confirmation` を指定して呼び出す。
3. enable now の結果が返ることを確認する。

## MT-027: `ssh_service_reload` 空 confirmation

1. 安全な service 名を指定し、`confirmation` を空または不一致にして呼び出す。
2. `Confirmation is required: service_reload:<service>` 相当の確認要求が返ることを確認する。
3. reload が実行されないことを確認する。

## MT-028: `ssh_service_reload` 確認済み実行

1. reload してよい安全な service がある場合のみ実施する。
2. 正しい `confirmation` を指定して呼び出す。
3. reload 結果が返ることを確認する。

## MT-097: `ssh_service_restart` 空 confirmation

1. 安全な service 名を指定し、`confirmation` を空または不一致にして呼び出す。
2. `Confirmation is required: service_restart:<service>` 相当の確認要求が返ることを確認する。
3. restart が実行されないことを確認する。

## MT-098: `ssh_service_restart` 確認済み実行

1. restart してよい安全な service がある場合のみ実施する。
2. 正しい `confirmation` を指定して呼び出す。
3. restart 結果が返ることを確認する。
4. 実施前後の service 状態を確認し、必要な後始末がある場合だけ後始末する。

## MT-099: `ssh_service_stop` 空 confirmation

1. 安全な service 名を指定し、`confirmation` を空または不一致にして呼び出す。
2. `Confirmation is required: service_stop:<service>` 相当の確認要求が返ることを確認する。
3. stop が実行されないことを確認する。

## MT-100: `ssh_service_stop` 確認済み実行

1. stop してよい安全な service がある場合のみ実施する。
2. 正しい `confirmation` を指定して呼び出す。
3. stop 結果が返ることを確認する。
4. 実施前後の service 状態を確認し、必要な後始末がある場合だけ後始末する。

## MT-101: `ssh_service_disable` 空 confirmation

1. 安全な service 名を指定し、`confirmation` を空または不一致にして呼び出す。
2. `Confirmation is required: service_disable:<service>` 相当の確認要求が返ることを確認する。
3. disable が実行されないことを確認する。

## MT-102: `ssh_service_disable` 確認済み実行

1. disable してよい安全な service がある場合のみ実施する。
2. 正しい `confirmation` を指定して呼び出す。
3. disable 結果が返ることを確認する。
4. 実施前後の service enable 状態を確認し、必要な後始末がある場合だけ後始末する。

## MT-029: `service_config_paths`

1. `serviceKey` に provider 対応サービスを指定して `service_config_paths` を呼び出す。
2. `mainConfig`、`configFiles`、`includePatterns` が返ることを確認する。

## MT-030: `service_config_file_read`

1. `service_config_paths` で得た provider 許可パスを指定する。
2. 設定ファイル本文、encoding、truncated が返ることを確認する。
3. provider 許可外または通常ファイルではない path が安全に拒否されることを確認する。

## MT-031: `service_config_file_write` 通常 target 空 confirmation

1. `targetKey` に通常 target を指定し、`confirmation` を空にして呼び出す。
2. `Confirmation is required: service_config_file_write:<serviceKey>:<path>:<method>:<targetKey>` が返ることを確認する。
3. 設定ファイルが変更されないことを確認する。

## MT-032: `service_config_file_write` indexed target 空 confirmation

1. `targetKey` に `server.server_name[0]` などの indexed target を指定し、`confirmation` を空にして呼び出す。
2. index 指定を含む確認文字列が返ることを確認する。
3. 設定ファイルが変更されないことを確認する。

## MT-033: `service_config_file_write` Nginx indexed replace

1. provider が許可した安全な Nginx 設定ファイルを選ぶ。
2. `targetKey` に `server.server_name[0]` などの indexed target を指定する。
3. 可能なら現在値と同じ値への `replace` で確認済み write を実行する。
4. `bytesWritten` が返ることを確認する。
5. `service_config_test` を実行する。
6. `service_config_file_commit` または `service_config_file_rollback` で backup を閉じる。

## MT-034: `service_config_file_write` Nginx indexed delete

1. 削除してもよい安全なテスト用設定ファイルがある場合のみ実施する。
2. `targetKey` に indexed target を指定して `delete` を実行する。
3. 対象行だけが削除されることを確認する。
4. `service_config_test` を実行する。
5. 原則として `service_config_file_rollback` で戻す。

## MT-035: `service_config_file_write` insert

1. 挿入してもよい安全なテスト用設定ファイルがある場合のみ実施する。
2. `targetKey` に `line:<number>` または `<path>:<number>` を指定して `insert` を実行する。
3. 指定行にだけ挿入されることを確認する。
4. `service_config_test` を実行する。
5. 原則として `service_config_file_rollback` で戻す。

## MT-036: `service_config_test` 空 confirmation

1. `confirmation` を空にして `service_config_test` を呼び出す。
2. `Confirmation is required: service_config_test:<serviceKey>` が返ることを確認する。

## MT-037: `service_config_test` 確認済み実行

1. `confirmation` に `service_config_test:<serviceKey>` を指定して呼び出す。
2. provider 管理のテストコマンド結果が返ることを確認する。

## MT-038: `service_config_file_rollback` 空 confirmation

1. `confirmation` を空にして `service_config_file_rollback` を呼び出す。
2. `Confirmation is required: service_config_file_rollback:<serviceKey>:<path>` が返ることを確認する。
3. 設定ファイルと backup が変更されないことを確認する。

## MT-039: `service_config_file_rollback` 確認済み実行

1. `service_config_file_write` で backup を作成する。
2. `confirmation` に `service_config_file_rollback:<serviceKey>:<path>` を指定して呼び出す。
3. `changed: true` が返ることを確認する。
4. 設定ファイルが backup から復元され、backup が削除されることを確認する。

## MT-040: `service_config_file_commit` 空 confirmation

1. `confirmation` を空にして `service_config_file_commit` を呼び出す。
2. `Confirmation is required: service_config_file_commit:<serviceKey>:<path>` が返ることを確認する。
3. 設定ファイルと backup が変更されないことを確認する。

## MT-041: `service_config_file_commit` 確認済み実行

1. `service_config_file_write` で backup を作成する。
2. `confirmation` に `service_config_file_commit:<serviceKey>:<path>` を指定して呼び出す。
3. `changed: true` が返ることを確認する。
4. 設定ファイル本体は変更せず、backup だけが削除されることを確認する。

## MT-042: `service_logfile_read`

1. `serviceKey` と provider 定義の `logKey` を指定して `service_logfile_read` を呼び出す。
2. 許可されたログ本文、encoding、truncated が返ることを確認する。

## MT-043: `web_file_read`

1. provider が許可した `siteKey` と site-relative absolute path を指定する。
2. Web ファイル本文または存在状態が返ることを確認する。
3. root 外へ出る path が拒否されることを確認する。

## MT-044: `web_file_write` 空 confirmation

1. 安全なテスト用 path と `contentBase64` を指定し、`confirmation` を空にして呼び出す。
2. `Confirmation is required: web_file_write:<siteKey>:<path>` 相当が返ることを確認する。
3. ファイルが変更されないことを確認する。

## MT-045: `web_file_write` 確認済み実行

1. provider が許可した安全なテスト用 path がある場合のみ実施する。
2. 正しい `confirmation` を指定して呼び出す。
3. `written: true`、`created`、`overwritten`、`resolvedPath` が返ることを確認する。

## MT-046: `web_change_owner` 空 confirmation

1. 安全なテスト用 path、owner、group を指定し、`confirmation` を空にして呼び出す。
2. 確認要求が返ることを確認する。
3. owner/group が変更されないことを確認する。

## MT-047: `web_change_owner` 確認済み実行

1. provider が許可した安全なテスト用 path がある場合のみ実施する。
2. 正しい `confirmation` を指定して呼び出す。
3. owner/group 変更結果が返ることを確認する。

## MT-048: `web_change_owner_recursive` 空 confirmation

1. 安全なテスト用ディレクトリを指定し、`confirmation` を空にして呼び出す。
2. 確認要求が返ることを確認する。
3. owner/group が変更されないことを確認する。

## MT-049: `web_change_owner_recursive` 確認済み実行

1. provider が許可した安全なテスト用ディレクトリがある場合のみ実施する。
2. 正しい `confirmation` を指定して呼び出す。
3. recursive owner/group 変更結果が返ることを確認する。
4. symlink が追跡または変更されないことを確認する。

## MT-050: `web_change_mode` 空 confirmation

1. 安全なテスト用 path と mode を指定し、`confirmation` を空にして呼び出す。
2. 確認要求が返ることを確認する。
3. mode が変更されないことを確認する。

## MT-051: `web_change_mode` 確認済み実行

1. provider が許可した安全なテスト用 path がある場合のみ実施する。
2. 正しい `confirmation` を指定して呼び出す。
3. mode 変更結果が返ることを確認する。
4. world-writable mode が拒否されることを確認する。

## MT-052: `web_change_mode_recursive` 空 confirmation

1. 安全なテスト用ディレクトリと mode を指定し、`confirmation` を空にして呼び出す。
2. 確認要求が返ることを確認する。
3. mode が変更されないことを確認する。

## MT-053: `web_change_mode_recursive` 確認済み実行

1. provider が許可した安全なテスト用ディレクトリがある場合のみ実施する。
2. 正しい `confirmation` を指定して呼び出す。
3. recursive mode 変更結果が返ることを確認する。
4. symlink が追跡または変更されないことを確認する。

## MT-054: `service_config_file_check_read`

1. `serviceKey` と provider 許可済み設定ファイル path を指定して `service_config_file_check_read` を呼び出す。
2. `canRead: true`、`canWrite: false` が返ることを確認する。
3. 設定ファイル本文が戻り値に含まれないことを確認する。
4. provider 許可外または通常ファイルではない path では `canRead: false` と理由が返ることを確認する。

## MT-055: `service_config_file_check_write`

1. `serviceKey`、provider 許可済み設定ファイル path、`method`、`targetKey`、必要に応じて `targetValue` を指定して `service_config_file_check_write` を呼び出す。
2. 書き込み可能な場合は `canRead: true`、`canWrite: true`、`requiresConfirmation: true`、`confirmation` が返ることを確認する。
3. 実ファイル本文、backup ファイル、設定状態が変更されないことを確認する。
4. targetKey が一致しない場合は `canWrite: false` と provider matcher の理由が返ることを確認する。
5. 実際の `service_config_file_write` は改めて confirmation を指定した場合だけ実行されることを確認する。

## MT-056: `web_file_list`

1. provider が許可した `siteKey` と site-relative absolute directory path を指定する。
2. `entries`、`resolvedPath`、`truncated` が返ることを確認する。
3. `limit` と `maxDepth` が反映されることを確認する。

## MT-069: `web_file_search_name`

1. provider が許可した `siteKey`、site-relative absolute directory path、file name glob を指定する。
2. `entries` が `pattern` に一致する file / directory name に絞られることを確認する。
3. `/` や `..` を含む path pattern が拒否されることを確認する。

## MT-057: `web_file_stat`

1. provider が許可した `siteKey` と site-relative absolute path を指定する。
2. `exists`、`type`、`size`、`mode`、`owner`、`group` が返ることを確認する。
3. root 外へ出る path が拒否されることを確認する。

## MT-058: `web_file_check_write`

1. provider が許可した安全なテスト用 path を指定する。
2. 実ファイル本文が変更されず、`canWrite`、`requiresConfirmation`、`confirmation` が返ることを確認する。
3. provider 許可外または書き込み不可の path では `canWrite: false` と理由が返ることを確認する。

## MT-070: `web_file_check_permissions`

1. provider が許可した安全な path、owner/group/mode 候補を指定する。
2. 実変更なしで現在の owner/group/mode と `canChangeOwner` / `canChangeMode` が返ることを確認する。
3. 可能な場合は `web_change_owner*` / `web_change_mode*` 用 confirmation が返ることを確認する。
4. `root` / `0`、world-writable mode、root 外 path が拒否または変更不可になることを確認する。

# 実機変更時の後始末

- `service_config_file_write` を実行した場合は、同じ `path` に対して必ず `service_config_file_commit` または `service_config_file_rollback` を実行する。
- `service_config_file_commit` は設定ファイル本体を変更せず、`<path>.kelpiebakup` だけを削除することを確認する。
- `service_config_file_rollback` は `<path>.kelpiebakup` を `<path>` に戻し、成功後に backup を削除することを確認する。
- Web file / permission 系の実変更は、テスト用 path だけで行う。既存 site root や本番ファイルへ直接 recursive 操作を行わない。
- 実機で得た実ホスト名、実ユーザー名、秘密情報、公開前の設定値は `MCP_COMMAND_TEST.md` に記録しない。

# KelpieSSH MCP コマンド

最終更新: 2026-06-28

このファイルは、KelpieSSH が MCP callable tool として公開するコマンドの正本です。
通常のターミナルで実行する `kelpie` / `kelpiemcp` CLI コマンドは `COMMANDS.ja.md` を正本とします。

## MCP tool の呼び出し方式

KelpieSSH の MCP tool は REST resource ではありません。Streamable HTTP MCP transport 上で送受信される MCP JSON-RPC method です。

通常の AI クライアント利用では、利用者が HTTP request を直接組み立てる必要はありません。Codex、Claude、その他の MCP client が local Streamable HTTP endpoint に接続し、tool 一覧の取得と tool 呼び出しを代行します。

既定 endpoint は [MCP_GUIDE.ja.md](MCP_GUIDE.ja.md) を参照してください。典型的な endpoint は次の形式です。

```text
http://127.0.0.1:45432/mcp
```

通常の流れは次の通りです。

1. MCP client が `initialize` を送信し、protocol capability を確立します。
2. MCP client が `tools/list` を送信し、利用可能な tool 名と JSON schema を取得します。
3. MCP client が `tools/call` に tool 名と引数を入れて送信します。
4. `KelpieMCPServer` が request を検証し、必要に応じて保存済み profile または `SshRemoteOperation` を解決し、policy check 後に許可済み operation を実行して結果を返します。

HTTP request body は REST 形式ではなく JSON-RPC です。たとえば診断 tool を直接呼ぶ場合は次の形になります。

```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "get_target_inventory",
    "arguments": {
      "profileName": "vps01"
    }
  }
}
```

この文書では、`tools/call` の中で指定する `name` と `arguments` を説明します。各 tool の呼び出しサンプルは、JSON-RPC `tools/call` request の `params` に入る形を示します。AI 利用者は通常、内部の JSON-RPC 呼び出し方式までは意識せず、各 tool の動作と安全上の注意を確認すれば十分です。

## コマンド分類

| 分類 | MCP tool | 内容 |
| :--- | :--- | :--- |
| サーバー疎通 | `kelpie_ping` | `KelpieMCPServer` の疎通確認。 |
| profile 管理 | `profile_reload`, `ssh_profile_capabilities` | 保存済み SSH profiles の再読み込みと、接続中 terminal の profile 操作可否確認を行う。 |
| ローカル診断 | `get_system_info`, `get_disk_usage`, `get_memory_usage`, `get_listening_ports` | `KelpieMCPServer` 実行ホストの診断。 |
| 機能可否確認 | `ssh_get_capabilities`, `get_target_inventory` | SSH 接続先 profile ごとの OS / command / tool 可否、helper / software inventory を確認する。 |
| SSH 診断 | `ssh_get_system_info`, `ssh_get_os_release`, `ssh_get_uptime`, `ssh_get_disk_usage`, `ssh_get_memory_usage`, `ssh_get_process_summary`, `ssh_get_inode_usage`, `ssh_get_mounts`, `ssh_get_network_addresses`, `ssh_get_routes`, `ssh_get_dns_config`, `ssh_cron_list`, `ssh_cron_validate`, `ssh_cron_check_write`, `ssh_cron_write`, `ssh_cron_rollback`, `ssh_cert_inspect`, `ssh_cert_expiry_check`, `ssh_user_list`, `ssh_user_info`, `ssh_group_list`, `ssh_group_info`, `ssh_sudoers_check`, `ssh_user_usage_check`, `ssh_user_check_group_change`, `ssh_user_apply_group_change`, `ssh_user_rollback_group_change`, `ssh_user_check_permission_change`, `ssh_user_apply_permission_change`, `ssh_user_rollback_permission_change`, `ssh_user_file_ownership_check`, `ssh_user_service_usage_check`, `ssh_service_residual_config_check`, `ssh_support_report_collect`, `ssh_firewall_status`, `ssh_firewall_check_rule`, `ssh_firewall_apply_rule`, `ssh_backup_plan_check`, `ssh_backup_run`, `ssh_backup_verify`, `ssh_audit_verify`, `ssh_audit_export`, `ssh_check_http_local`, `ssh_check_tcp_connect_local`, `ssh_get_listening_ports`, `ssh_get_failed_services`, `ssh_get_journal_recent`, `ssh_tail_log`, `ssh_run_allowed_command`, `ssh_run_remote_operation` | 許可済み SSH 診断コマンドの実行。 |
| 環境変数 | `get_environment_keys`, `peek_environment_value`, `set_environment_value`, `list_persistent_environment_keys`, `persist_environment_value`, `remove_persistent_environment_value` | profile policy に従って remote 環境変数の key 表示、値参照、一時設定、永続化を行う。 |
| SSH ターミナル / session cleanup | `ssh_terminal_open`, `ssh_terminal_send`, `ssh_terminal_snapshot`, `ssh_terminal_close`, `ssh_connection_close`, `ssh_logout` | PTY 付き対話ターミナルの操作と MCP password session の破棄。 |
| パッケージ操作 | `ssh_pkg_check_updates`, `ssh_pkg_info`, `ssh_pkg_search`, `ssh_pkg_list_installed`, `ssh_pkg_simulate_install`, `ssh_pkg_install`, `ssh_pkg_install_confirmed`, `ssh_pkg_simulate_remove`, `ssh_pkg_remove` | package の確認、検索、dry-run、確認付き変更。 |
| サービス操作 | `ssh_service_status`, `ssh_service_is_active`, `ssh_service_is_enabled`, `ssh_list_services`, `ssh_service_enable_now`, `ssh_service_reload`, `ssh_service_restart`, `ssh_service_stop`, `ssh_service_disable` | systemd service の状態確認と確認付き変更。 |
| サービス設定 / ログ | `service_config_paths`, `service_config_file_check_read`, `service_config_file_read`, `service_config_file_check_write`, `service_config_file_write`, `service_config_file_rollback`, `service_config_file_commit`, `service_config_test`, `ssh_service_config_nginx_enable_php`, `service_logfile_read` | provider が許可したサービス設定ファイルとログの操作。 |
| Web ファイル | `web_file_list`, `web_file_search_name`, `web_file_search_text`, `web_file_stat`, `web_file_check_write`, `web_file_check_permissions`, `web_file_read`, `web_file_head`, `web_file_tail`, `web_file_write`, `web_change_owner`, `web_change_owner_recursive`, `web_change_mode`, `web_change_mode_recursive` | provider が許可した Web ルート配下のファイル操作と権限変更。 |

## 共通オプション

SSH 先を操作する既存 tool は、互換性のため `profileName` に `KelpieHome/profiles/<profile>.json` の `<profile>` 部分を指定します。内部実行では host 側 loader が profile を `SshRemoteOperation` へ変換します。

`ssh_run_remote_operation` は profile を受け取らず、`endpoint` / `credential` / `policy` / `operation` / `options` を含む `SshRemoteOperation` を直接受け取ります。profile 登録数、edition、license、広告、サポート、表示順、顧客情報などの製品管理情報は含めません。

変更操作を伴う tool は `confirmation` を要求します。`confirmation` が空または不一致の場合、実変更せず `Confirmation is required: ...` を返します。

MCP tool の戻り値は JSON object または text です。SSH コマンド系の戻り値には、主に `Ok`, `Data`, `ErrorInfo`, `Meta`, `ProfileName`, `CommandName`, `CommandText`, `ExitCode`, `StandardOutput`, `StandardError`, `Stdout`, `Stderr`, `StdoutPlain`, `StderrPlain`, `StartedAt`, `CompletedAt`, `TimedOut`, `Error` が含まれます。

- `Ok`: SSH tool が `ExitCode: 0` かつ Kelpie 側エラーなしで完了した場合に `true`。
- `Data`: `Ok` が `true` の場合の構造化 command data。互換性のため既存の top-level fields も残します。
- `ErrorInfo`: `Ok` が `false` の場合の構造化エラー情報。`Code`, `Category`, `Message`, `Hint`, `Retryable` を含みます。
- `Meta`: `SchemaVersion`, `GeneratedAt`, `ProfileName`, `CommandName`, output line count, `Truncated` を含む metadata。
- `Error`: 互換性維持用の legacy error message。

入力不備や policy 拒否のような想定内の失敗は、MCP invocation exception ではなく `Ok: false` の `SshToolResult` として返ります。remote command の非0終了も `Ok: false` になり、legacy stdout / stderr fields は保持されます。

## コマンド詳細

### `kelpie_ping`

目的:

`KelpieMCPServer` が起動していて MCP tool 呼び出しに応答できることを確認します。

入力引数:

- なし。

引数サンプル:

```json
{}
```

確認文字列:

- なし。

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- text。

実行結果サンプル:

```text
KelpieSSH MCP server is running.
```

安全上の注意:

- 読み取り専用です。

### `profile_reload`

目的:

`KelpieMCPServer` を再起動せずに、Kelpie profiles directory の保存済み SSH profile JSON files を再読み込みします。

入力引数:

- なし。

呼び出しサンプル:

```json
{
  "name": "profile_reload",
  "arguments": {}
}
```

確認文字列:

- なし。

処理内容:

MCP server が `KelpieHome/profiles/*.json` を読み直し、reload が成功した場合だけ in-memory profile catalog を差し替えます。Profile JSON が不正、または読み取りに失敗した場合は、最後に正常読み込みした profile catalog を維持します。

戻り値:

- `ProfileReloadToolResult`

実行結果サンプル:

```json
{
  "Success": true,
  "ProfilesDirectory": "D:\\Kelpie\\profiles",
  "ProfileCount": 2,
  "ProfileNames": ["vps01", "vps02"],
  "ErrorMessage": null
}
```

安全上の注意:

- SSH 接続先には接続しません。
- SSH 接続先の file、process、settings は変更しません。
- この tool が変更するのは MCP server の in-memory profile catalog だけです。
- 既存の SSH terminal session は現在の接続を維持します。新しい tool call は再読み込み後の profile を使います。
- `kelpiemcp.json` は再読み込みしません。server configuration を変更した場合は MCP server を再起動してください。

### `ssh_profile_capabilities`

目的:

開いている SSH terminal connection について、profile 操作可否を返します。

入力引数:

- `handle`: `ssh_terminal_open` が返した SSH terminal handle。

呼び出しサンプル:

```json
{
  "name": "ssh_profile_capabilities",
  "arguments": {
    "handle": "term-a1b2c3d4e5f6"
  }
}
```

確認文字列:

- なし。

処理内容:

MCP server は terminal handle から接続中 profile を解決します。`kelpiemcp.json` の `ProfileOperations:Reload:MCP` を読み、MCP経由の reload capability が許可されているか返します。SSH target には接続せず、profile file 本文も返しません。

戻り値:

- `SshProfileCapabilitiesToolResult`
- `Handle`: 要求された terminal handle。
- `ProfileName`: handle に紐づく profile 名。handle が見つからない場合は空文字。
- `ReloadAllowed`: `ProfileOperations:Reload:MCP` が `Allow` なら `true`、それ以外は `false`。互換のため旧 `Allowed` と boolean `true` も許可として扱います。
- `Reason`: `allowed-by-config`、`disabled-by-config`、`session-not-found` などの理由。

実行結果サンプル:

```json
{
  "Handle": "term-a1b2c3d4e5f6",
  "ProfileName": "vps01",
  "ReloadAllowed": false,
  "Reason": "disabled-by-config"
}
```

安全上の注意:

- 読み取り専用です。
- terminal handle、profile 名、reload 可否だけを返します。

### `get_system_info`

目的:

`KelpieMCPServer` 実行ホストの OS、runtime、process 情報を取得します。

入力引数:

- なし。

引数サンプル:

```json
{}
```

確認文字列:

- なし。

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- `MachineName`
- `UserName`
- `OSDescription`
- `OSArchitecture`
- `ProcessArchitecture`
- `FrameworkDescription`
- `ProcessorCount`
- `ProcessId`
- `BaseDirectory`

実行結果サンプル:

```json
{
  "MachineName": "HOST",
  "UserName": "user",
  "OSDescription": "Microsoft Windows ...",
  "ProcessId": 1234,
  "BaseDirectory": "D:\\Kelpie\\bin\\mcp\\"
}
```

安全上の注意:

- 読み取り専用です。

### `get_disk_usage`

目的:

`KelpieMCPServer` 実行ホストの ready drive の disk usage を取得します。

入力引数:

- なし。

引数サンプル:

```json
{}
```

確認文字列:

- なし。

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- `Drives[]`

実行結果サンプル:

```json
{
  "Drives": [
    {
      "Name": "C:\\",
      "DriveType": "Fixed",
      "TotalBytes": 100000000000,
      "AvailableFreeBytes": 50000000000
    }
  ]
}
```

安全上の注意:

- 読み取り専用です。

### `get_memory_usage`

目的:

`KelpieMCPServer` process と managed runtime の memory usage を取得します。

入力引数:

- なし。

引数サンプル:

```json
{}
```

確認文字列:

- なし。

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- `WorkingSetBytes`
- `PrivateMemoryBytes`
- `VirtualMemoryBytes`
- `ManagedTotalBytes`
- `HeapSizeBytes`

実行結果サンプル:

```json
{
  "WorkingSetBytes": 123456789,
  "PrivateMemoryBytes": 123456789,
  "ManagedTotalBytes": 12345678
}
```

安全上の注意:

- 読み取り専用です。

### `get_listening_ports`

目的:

`KelpieMCPServer` 実行ホストの listening TCP/UDP port を取得します。

入力引数:

- なし。

引数サンプル:

```json
{}
```

確認文字列:

- なし。

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- `Command`
- `Arguments`
- `ExitCode`
- `StandardError`
- `Ports[]`

実行結果サンプル:

```json
{
  "Command": "netstat.exe",
  "Arguments": "-ano",
  "ExitCode": 0,
  "Ports": [
    {
      "Protocol": "TCP",
      "LocalAddress": "127.0.0.1:45432",
      "State": "LISTENING",
      "ProcessId": "1234"
    }
  ]
}
```

安全上の注意:

- 読み取り専用です。

### 環境変数 tool

対象 tool:

- `get_environment_keys`
- `peek_environment_value`
- `set_environment_value`
- `list_persistent_environment_keys`
- `persist_environment_value`
- `remove_persistent_environment_value`

目的:

profile の `Capabilities` と `EnvironmentValues` policy に従って、remote 環境変数の key 表示、値参照、1回だけの一時設定、Kelpie env file への永続化を行います。

入力引数:

- `profileName`: SSH プロファイル名。
- `key`: key 指定が必要な環境変数 tool で使う環境変数名。
- `value`: `set_environment_value` / `persist_environment_value` で使う環境変数値。
- `command`: `set_environment_value` で実行する command。

`get_environment_keys` 呼び出しサンプル:

```json
{
  "name": "get_environment_keys",
  "arguments": {
    "profileName": "vps01"
  }
}
```

`peek_environment_value` 呼び出しサンプル:

```json
{
  "name": "peek_environment_value",
  "arguments": {
    "profileName": "vps01",
    "key": "PATH"
  }
}
```

`set_environment_value` 呼び出しサンプル:

```json
{
  "name": "set_environment_value",
  "arguments": {
    "profileName": "vps01",
    "key": "APP_ENV",
    "value": "production",
    "command": "printenv APP_ENV"
  }
}
```

`list_persistent_environment_keys` 呼び出しサンプル:

```json
{
  "name": "list_persistent_environment_keys",
  "arguments": {
    "profileName": "vps01"
  }
}
```

`persist_environment_value` 呼び出しサンプル:

```json
{
  "name": "persist_environment_value",
  "arguments": {
    "profileName": "vps01",
    "key": "APP_ENV",
    "value": "production"
  }
}
```

`remove_persistent_environment_value` 呼び出しサンプル:

```json
{
  "name": "remove_persistent_environment_value",
  "arguments": {
    "profileName": "vps01",
    "key": "APP_ENV"
  }
}
```

確認文字列:

- なし。

処理内容:

`get_environment_keys` は `AllowPeekEnvironmentKeys` がある場合だけ実行できます。`EnvironmentValues` で `Hidden` にした key は出力から除外します。
`peek_environment_value` は `AllowPeekEnvironmentValues` と、対象 key の `PeekCommon` / `PeekSecret` / `Masked` rule が必要です。
`set_environment_value` は `AllowSetEnvironmentValues` と、対象 key の `SetCommon` / `SetSecret` rule が必要です。`~/.kelpie/.env` が存在する場合は source してから、指定 command の1回の実行にだけ値を付与します。
`list_persistent_environment_keys` は `~/.kelpie/.env` から key 名だけを読み取ります。
`persist_environment_value` は `~/.kelpie/.env` に1つの key/value を保存します。書き込み前に timestamp 付き `.kelpie` backup を作成します。
`remove_persistent_environment_value` は `~/.kelpie/.env` から1つの key を削除します。書き込み前に timestamp 付き `.kelpie` backup を作成します。

戻り値:

- `SshToolResult`

実行結果サンプル:

```json
{
  "ProfileName": "vps01",
  "CommandName": "set_environment_value",
  "CommandText": "env APP_ENV=(hidden) printenv APP_ENV",
  "ExitCode": 0,
  "StandardOutput": "production\n"
}
```

安全上の注意:

- 環境変数値は secret の可能性があります。raw value を公開文書、公開 issue、公開ログに貼り付けないでください。
- `set_environment_value` は戻り値の `CommandText` で value を `(hidden)` としてマスクします。
- `persist_environment_value` は戻り値の `CommandText` で value を `(hidden)` としてマスクします。
- `Hidden` key は存在しないものとして扱います。`Masked` key は masked output と長さだけを返します。
- `EnvironmentValues` に未定義の key は、key 一覧に表示されることはありますが、値の参照と設定はできません。
- 永続化した値は、shell、cron、service、Kelpie command が次回 `~/.kelpie/.env` を source した時点で反映されます。既存プロセスには自動反映されません。

### `ssh_run_allowed_command`

目的:

指定プロファイルに対し、許可リストに存在する読み取り中心の SSH コマンドを実行します。

入力引数:

- `profileName`: SSH プロファイル名。
- `commandName`: 許可済みコマンド名。例: `get_system_info`。
- `arguments`: コマンド引数。省略可。

引数サンプル:

```json
{
  "profileName": "vps01",
  "commandName": "get_system_info",
  "arguments": {}
}
```

確認文字列:

- なし。

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- `SshToolResult`

実行結果サンプル:

```json
{
  "ProfileName": "vps01",
  "CommandName": "get_system_info",
  "ExitCode": 0,
  "StandardOutput": "Linux example ...\n"
}
```

未許可 `commandName` の場合:

```json
{
  "ProfileName": "vps01",
  "CommandName": "not_allowed",
  "ExitCode": -1,
  "StandardError": "SSH command is not allowed: not_allowed",
  "Stderr": [
    "SSH command is not allowed: not_allowed"
  ],
  "StderrPlain": [
    "SSH command is not allowed: not_allowed"
  ],
  "Error": "SSH command is not allowed: not_allowed"
}
```

### `ssh_run_remote_operation`

目的:

保存済み profile 名を使わず、1回の SSH 操作入力である `SshRemoteOperation` を直接実行します。

入力引数:

- `operation`: `endpoint` / `credential` / `policy` / `operation` / `options` / 任意の `target` を持つ SSH remote operation。

引数サンプル:

```json
{
  "operation": {
    "endpoint": {
      "host": "203.0.113.10",
      "port": 22
    },
    "credential": {
      "user_name": "deploy",
      "kind": "private_key",
      "private_key_path": "id_ed25519"
    },
    "policy": {
      "mode": "maintenance",
      "roles": ["web_admin"],
      "allowed_roots": [
        {
          "path": "/var/www/example",
          "access": ["read", "list", "write", "cd"]
        }
      ],
      "special_paths": [
        {
          "pattern": "**/.env",
          "action": "deny"
        }
      ]
    },
    "operation": {
      "kind": "managed",
      "name": "service_status",
      "arguments": {
        "service": "nginx"
      }
    },
    "options": {
      "timeout_seconds": 30,
      "correlation_id": "op-example"
    },
    "target": {
      "os_family": "debian",
      "package_manager": "apt"
    }
  }
}
```

確認文字列:

- operation が呼び出す managed command の risk level に従います。

戻り値:

- `SshRemoteOperationToolResult`

安全上の注意:

- `operation.kind` が `managed` の場合も、既存の許可コマンド catalog と policy 評価を通過したコマンドだけを実行します。
- `operation.kind` が `raw` の場合は raw shell policy を通過した command text だけを実行します。

安全上の注意:

- `service_config_*`, `service_logfile_*`, `web_file_*`, `web_change_*` は専用 tool から呼び出します。
- `support_report_collect`, `audit_*`, 確認必須の maintenance command は専用 tool から呼び出します。
- 実行可能な `commandName` はプロファイルと provider の許可リストに制限されます。

### `ssh_get_capabilities`

目的:

指定プロファイルへ接続した後、対象 profile の OS family、許可済み command、Kelpie MCP tool の実行可否を読み取り専用で確認します。

入力引数:

- `profileName`: SSH プロファイル名。

引数サンプル:

```json
{
  "profileName": "vps01"
}
```

確認文字列:

- なし。

処理内容:

`get_os_release` を固定 probe として実行し、profile 設定と provider の許可リストから、利用可能な command と MCP tool を返します。

戻り値:

- `SshCapabilityResult`

実行結果サンプル:

```json
{
  "ProfileName": "vps01",
  "OsFamily": "alma",
  "PackageManager": "dnf",
  "ProbeSucceeded": true,
  "ProbeCommandName": "get_os_release",
  "ProbeCommandText": "cat /etc/os-release",
  "Commands": [
    { "CommandName": "pkg_search", "RiskLevel": "ReadOnly", "RequiresConfirmation": false }
  ],
  "Tools": [
    { "ToolName": "ssh_pkg_search", "CommandName": "pkg_search", "Available": true }
  ]
}
```

安全上の注意:

- 読み取り専用です。package install、service 変更、sudo、raw shell fallback は行いません。
- MCP の `tools/list` は静的な製品機能一覧であり、この tool は profile ごとの動的可否確認として使います。
- capability は実行時点の診断結果であり、実行 tool 側でも同じ前提を再検証します。
- `available: false` の tool を呼び出した場合は、KelpieMCPServer が構造化エラーまたは失敗結果を返します。MCP クライアントは勝手に代替コマンドや install を実行せず、ユーザーへ理由と選択肢を説明します。

### `get_target_inventory`

目的:

指定プロファイルの OS 基本情報、helper、software の availability と version を読み取り専用で一括取得します。
`python3`、`php`、`node`、`systemctl`、`journalctl`、`findmnt`、`ss`、`ip` などの optional helper / OS 標準 command の有無も確認します。

入力引数:

- `profileName`: SSH プロファイル名。

引数サンプル:

```json
{
  "profileName": "vps02"
}
```

確認文字列:

- なし。

処理内容:

対象 SSH profile で `target_inventory` を実行し、`/etc/os-release` と固定コマンドの version 出力を読み取ります。各 helper / software command は約8秒で打ち切り、コマンド単位の失敗は `Not Available` として返します。検出結果は実行時点の結果として扱い、SSH profile file へ書き戻しません。SSH 接続または OS probe に失敗した場合のみ tool 全体を失敗扱いにします。

戻り値:

- `TargetInventoryResult`

実行結果サンプル:

```json
{
  "Profile": "vps02",
  "Os": {
    "Family": "alma",
    "Name": "AlmaLinux",
    "Version": "9.6",
    "PackageManager": "dnf"
  },
  "Helpers": [
    {
      "Name": "Python",
      "Executable": "python3",
      "Status": "Available",
      "Version": "3.9.21",
      "Detail": "Python 3.9.21",
      "ExitCode": 0
    },
    {
      "Name": "PHP",
      "Executable": "php",
      "Status": "Not Available",
      "Version": "",
      "Detail": "command not found",
      "ExitCode": 127
    }
  ],
  "Software": [
    {
      "Name": "nginx",
      "Executable": "nginx",
      "Status": "Available",
      "Version": "1.24.0",
      "Detail": "nginx version: nginx/1.24.0",
      "ExitCode": 0
    }
  ]
}
```

安全上の注意:

- 読み取り専用です。package install、service 変更、sudo、raw shell fallback は行いません。
- `Detail` は stdout / stderr の最初の有効行だけです。file 本文、秘密鍵、パスワード、raw log body は返しません。

### `ssh_get_system_info`

目的:

指定プロファイルで `get_system_info` を実行します。

入力引数:

- `profileName`: SSH プロファイル名。

引数サンプル:

```json
{
  "profileName": "vps01"
}
```

確認文字列:

- なし。

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- `SshToolResult`

実行結果サンプル:

```json
{
  "ProfileName": "vps01",
  "CommandName": "get_system_info",
  "ExitCode": 0,
  "StandardOutput": "Linux example ...\n"
}
```

安全上の注意:

- 読み取り専用の許可済み SSH 診断です。

### `ssh_get_os_release`

目的:

指定プロファイルで `get_os_release` を実行し、`/etc/os-release` を取得します。

入力引数:

- `profileName`: SSH プロファイル名。

引数サンプル:

```json
{
  "profileName": "vps01"
}
```

確認文字列:

- なし。

処理内容:

`cat /etc/os-release` を許可済み SSH 診断として実行します。

戻り値:

- `SshToolResult`

実行結果サンプル:

```json
{
  "ProfileName": "vps01",
  "CommandName": "get_os_release",
  "ExitCode": 0,
  "StandardOutput": "NAME=\"AlmaLinux\"\\nVERSION_ID=\"9.6\"\\n..."
}
```

安全上の注意:

- 読み取り専用の許可済み SSH 診断です。

### `ssh_get_uptime`

目的:

指定プロファイルで `get_uptime` を実行し、稼働時間と load average を取得します。

入力引数:

- `profileName`: SSH プロファイル名。

引数サンプル:

```json
{
  "profileName": "vps01"
}
```

確認文字列:

- なし。

処理内容:

`uptime` を許可済み SSH 診断として実行します。

戻り値:

- `SshToolResult`

実行結果サンプル:

```json
{
  "ProfileName": "vps01",
  "CommandName": "get_uptime",
  "ExitCode": 0,
  "StandardOutput": " 12:34:56 up 10 days,  1 user,  load average: 0.00, 0.01, 0.05\\n"
}
```

安全上の注意:

- 読み取り専用の許可済み SSH 診断です。

### `ssh_get_disk_usage`

目的:

指定プロファイルで `get_disk_usage` を実行します。

入力引数:

- `profileName`: SSH プロファイル名。

引数サンプル:

```json
{
  "profileName": "vps01"
}
```

確認文字列:

- なし。

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- `SshToolResult`

実行結果サンプル:

```json
{
  "ProfileName": "vps01",
  "CommandName": "get_disk_usage",
  "ExitCode": 0,
  "StandardOutput": "Filesystem      Size  Used Avail Use% Mounted on\n..."
}
```

安全上の注意:

- 読み取り専用の許可済み SSH 診断です。

### `ssh_get_memory_usage`

目的:

指定プロファイルで `get_memory_usage` を実行します。

入力引数:

- `profileName`: SSH プロファイル名。

引数サンプル:

```json
{
  "profileName": "vps01"
}
```

確認文字列:

- なし。

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- `SshToolResult`

実行結果サンプル:

```json
{
  "ProfileName": "vps01",
  "CommandName": "get_memory_usage",
  "ExitCode": 0,
  "StandardOutput": "               total        used        free\nMem:            1780         420         190\n"
}
```

安全上の注意:

- 読み取り専用の許可済み SSH 診断です。

### `ssh_get_process_summary`

目的:

指定プロファイルで `get_process_summary` を実行し、CPU またはメモリ使用量順のプロセス概要を取得します。

入力引数:

- `profileName`: SSH プロファイル名。
- `sortBy`: 並び順。`cpu` または `memory`。省略時は `cpu`。
- `limit`: 最大取得プロセス行数。省略時は `10`、最大3桁。

引数サンプル:

```json
{
  "profileName": "vps01",
  "sortBy": "cpu",
  "limit": "10"
}
```

確認文字列:

- なし。

処理内容:

固定の shell wrapper から `ps -eo pid,ppid,user,comm,%cpu,%mem --sort=<sort>` を実行し、ヘッダーと先頭 `limit` 件を返します。`python3` は必要ありません。

戻り値:

- `SshToolResult`

実行結果サンプル:

```json
{
  "ProfileName": "vps01",
  "CommandName": "get_process_summary",
  "ExitCode": 0,
  "StandardOutput": "PID PPID USER COMMAND %CPU %MEM\\n..."
}
```

安全上の注意:

- 読み取り専用です。
- `sortBy` と `limit` は provider の引数検証を通過する必要があります。
- 任意の `ps` オプションは受け付けません。

### `ssh_get_inode_usage`

目的:

指定プロファイルで `get_inode_usage` を実行し、inode 使用量を取得します。

入力引数:

- `profileName`: SSH プロファイル名。

引数サンプル:

```json
{
  "profileName": "vps01"
}
```

確認文字列:

- なし。

処理内容:

`df -ih` を許可済み SSH 診断として実行します。

戻り値:

- `SshToolResult`

安全上の注意:

- 読み取り専用の許可済み SSH 診断です。

### `ssh_get_mounts`

目的:

指定プロファイルで `get_mounts` を実行し、mount 状態を取得します。

入力引数:

- `profileName`: SSH プロファイル名。

引数サンプル:

```json
{
  "profileName": "vps01"
}
```

確認文字列:

- なし。

処理内容:

`findmnt -rno TARGET,SOURCE,FSTYPE,OPTIONS` を許可済み SSH 診断として実行します。

戻り値:

- `SshToolResult`

安全上の注意:

- 読み取り専用の許可済み SSH 診断です。

### `ssh_get_network_addresses`

目的:

指定プロファイルで `get_network_addresses` を実行し、network interface と address 情報を取得します。

入力引数:

- `profileName`: SSH プロファイル名。

引数サンプル:

```json
{
  "profileName": "vps01"
}
```

確認文字列:

- なし。

処理内容:

`ip addr show` を許可済み SSH 診断として実行します。

戻り値:

- `SshToolResult`

安全上の注意:

- 読み取り専用の許可済み SSH 診断です。
- IP address や interface 名には環境情報が含まれるため、公開ログへ転記する場合は注意します。

### `ssh_get_routes`

目的:

指定プロファイルで `get_routes` を実行し、routing table を取得します。

入力引数:

- `profileName`: SSH プロファイル名。

引数サンプル:

```json
{
  "profileName": "vps01"
}
```

確認文字列:

- なし。

処理内容:

`ip route show` を許可済み SSH 診断として実行します。

戻り値:

- `SshToolResult`

安全上の注意:

- 読み取り専用の許可済み SSH 診断です。
- gateway や private address などの環境情報を含む可能性があります。

### `ssh_get_listening_ports`

目的:

指定プロファイルで `get_listening_ports` を実行します。

入力引数:

- `profileName`: SSH プロファイル名。

引数サンプル:

```json
{
  "profileName": "vps01"
}
```

確認文字列:

- なし。

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- `SshToolResult`

実行結果サンプル:

```json
{
  "ProfileName": "vps01",
  "CommandName": "get_listening_ports",
  "ExitCode": 0,
  "StandardOutput": "Netid State  Recv-Q Send-Q Local Address:Port Peer Address:Port\n..."
}
```

安全上の注意:

- 読み取り専用の許可済み SSH 診断です。

### `ssh_get_dns_config`

目的:

指定プロファイルで `get_dns_config` を実行し、DNS resolver 設定を取得します。

入力引数:

- `profileName`: SSH プロファイル名。

引数サンプル:

```json
{
  "profileName": "vps01"
}
```

確認文字列:

- なし。

処理内容:

`cat /etc/resolv.conf` を許可済み SSH 診断として実行します。

戻り値:

- `SshToolResult`

安全上の注意:

- 読み取り専用の許可済み SSH 診断です。
- DNS server や search domain などの環境情報を含む可能性があります。

### `ssh_cron_list`

目的:

指定プロファイルで `cron_list` を実行し、system cron と現在の SSH user の crontab を読み取り専用で一覧化します。

入力引数:

- `profileName`: SSH プロファイル名。
- `limit`: 最大取得行数。省略時は `100`、最大 `200`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "limit": "50"
}
```

確認文字列:

- なし。

処理内容:

`/etc/crontab`、`/etc/cron.d/*` の通常ファイル、現在 user の `crontab -l` を固定 Python wrapper で読み取り、コメント行と空行を除いて `limit` 件まで返します。

戻り値:

- `SshToolResult`

安全上の注意:

- 読み取り専用です。
- 任意 path や任意 user の crontab は指定できません。

### `ssh_cron_validate`

目的:

指定プロファイルで `cron_validate` を実行し、cron 式、実行 user、command、log path の妥当性を実変更なしで検証します。

入力引数:

- `profileName`: SSH プロファイル名。
- `cronExpression`: 5 field cron expression。
- `runUser`: 実行 user 名。
- `command`: 検証対象 command text。
- `logPath`: `/var/log/` 配下の log path。

引数サンプル:

```json
{
  "profileName": "vps01",
  "cronExpression": "*/5 * * * *",
  "runUser": "deploy",
  "command": "/usr/local/bin/job --once",
  "logPath": "/var/log/kelpie/job.log"
}
```

確認文字列:

- なし。

処理内容:

固定 Python wrapper で引数形式を検証し、`valid=true` または `valid=false` を返します。cron file への書き込みは行いません。

戻り値:

- `SshToolResult`

安全上の注意:

- 読み取り専用です。
- `command` は検証対象文字列として扱い、実行しません。
- 危険文字を含む引数は SSH 実行前に拒否します。

### `ssh_cron_check_write`

目的:

cron 変更前に、対象、実行 user、cron 式、command、log path、確認文字列、rollback 可否を実変更なしで確認します。

入力引数:

- `profileName`: SSH プロファイル名。
- `targetType`: `user` または `system`。
- `runUser`: cron 実行 user。
- `cronExpression`: 5 field cron 式。
- `command`: 実行 command。危険文字は拒否。
- `logPath`: `/var/log/` 配下の log path。

確認文字列:

- なし。この tool は実変更しません。

戻り値:

- `SshToolResult`
- stdout に `targetType`, `target`, `userExists`, `requiresConfirmation`, `confirmation`, `rollbackSupported` を含みます。

安全上の注意:

- cron file や user crontab は変更しません。
- cron 本文や既存 crontab 本文は返しません。

### `ssh_cron_write`

目的:

確認済みの cron 変更を適用し、rollback 用 backup を作成します。

入力引数:

- `profileName`: SSH プロファイル名。
- `targetType`: `user` または `system`。
- `runUser`: cron 実行 user。
- `cronExpression`: 5 field cron 式。
- `command`: 実行 command。危険文字は拒否。
- `logPath`: `/var/log/` 配下の log path。
- `confirmation`: `cron_write:<targetType>:<runUser>`。

確認文字列:

- `cron_write:<targetType>:<runUser>`

戻り値:

- `SshToolResult`
- stdout に `targetType`, `target`, `runUser`, `changed`, `backupPath`, `rollbackConfirmation`, `standardErrorSummary` を含みます。

安全上の注意:

- `sudo -n` で実行します。sudo 権限がない場合は失敗します。
- 既存 crontab / cron file 本文は返しません。
- `ssh_run_allowed_command` 経由では実行できません。

### `ssh_cron_rollback`

目的:

`ssh_cron_write` が作成した最新 backup から cron 変更を rollback します。

入力引数:

- `profileName`: SSH プロファイル名。
- `targetType`: `user` または `system`。
- `runUser`: cron 実行 user。
- `confirmation`: `cron_rollback:<targetType>:<runUser>`。

確認文字列:

- `cron_rollback:<targetType>:<runUser>`

戻り値:

- `SshToolResult`
- stdout に `targetType`, `target`, `runUser`, `backupExists`, `restored`, `standardErrorSummary` を含みます。

安全上の注意:

- `sudo -n` で実行します。sudo 権限がない場合は失敗します。
- backup 本文や cron 本文は返しません。
- `ssh_run_allowed_command` 経由では実行できません。

### `ssh_cert_inspect`

目的:

指定プロファイルで `cert_inspect` を実行し、証明書の issuer、subject、有効期限、SAN を確認します。

入力引数:

- `profileName`: SSH プロファイル名。
- `path`: 証明書 file path。`/etc/letsencrypt/live/`、`/etc/letsencrypt/archive/`、`/etc/ssl/`、`/etc/pki/` 配下の `.pem` / `.crt` / `.cer` のみ。

引数サンプル:

```json
{
  "profileName": "vps01",
  "path": "/etc/letsencrypt/live/example.invalid/fullchain.pem"
}
```

確認文字列:

- なし。

処理内容:

`openssl x509 -noout` で公開証明書情報だけを出力します。

戻り値:

- `SshToolResult`

安全上の注意:

- 読み取り専用です。
- 任意 path は受け付けません。
- file 本文をそのまま `cat` しません。

### `ssh_cert_expiry_check`

目的:

指定プロファイルで `cert_expiry_check` を実行し、証明書が指定日数後も有効か確認します。

入力引数:

- `profileName`: SSH プロファイル名。
- `path`: 証明書 file path。`ssh_cert_inspect` と同じ制限。
- `days`: 確認する残存日数。省略時は `30`、最大 `3650`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "path": "/etc/pki/tls/certs/example.crt",
  "days": "30"
}
```

確認文字列:

- なし。

処理内容:

`openssl x509 -checkend` を固定 Python wrapper から実行し、openssl の仕様どおり有効なら `ExitCode: 0`、期限不足なら非0を返します。

戻り値:

- `SshToolResult`

安全上の注意:

- 読み取り専用です。
- 任意 path は受け付けません。

### `ssh_user_list`

目的:

指定プロファイルで `user_list` を実行し、ローカル user の UID、GID、home directory、login shell を一覧化します。

入力引数:

- `profileName`: SSH プロファイル名。
- `limit`: 最大取得 user 数。省略時は `100`、最大 `200`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "limit": "50"
}
```

確認文字列:

- なし。

処理内容:

Python の `pwd.getpwall()` でローカル user 情報を取得し、`limit` 件まで返します。

戻り値:

- `SshToolResult`

安全上の注意:

- 読み取り専用です。
- パスワード hash や shadow 情報は読み取りません。

### `ssh_user_info`

目的:

指定プロファイルで `user_info` を実行し、1 user の UID、GID、primary group、supplementary groups、home directory、login shell を取得します。

入力引数:

- `profileName`: SSH プロファイル名。
- `user`: user 名。

引数サンプル:

```json
{
  "profileName": "vps01",
  "user": "deploy"
}
```

確認文字列:

- なし。

処理内容:

Python の `pwd.getpwall()` と `grp.getgrall()` を使い、指定 user の公開アカウント情報だけを返します。

戻り値:

- `SshToolResult`

安全上の注意:

- 読み取り専用です。
- パスワード hash や shadow 情報は読み取りません。
- user 名は provider の安全な名前 pattern に制限されます。

### `ssh_group_list`

目的:

指定プロファイルで `group_list` を実行し、ローカル group の GID と member 名を bounded list で取得します。

入力引数:

- `profileName`: SSH プロファイル名。
- `limit`: 最大取得 group 数。省略時は `100`、最大 `200`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "limit": "50"
}
```

確認文字列:

- なし。

処理内容:

Python の `grp.getgrall()` で group 情報を取得し、`limit` 件まで返します。

戻り値:

- `SshToolResult`

安全上の注意:

- 読み取り専用です。
- group 名、GID、member 名だけを返します。

### `ssh_group_info`

目的:

指定プロファイルで `group_info` を実行し、1 group の GID と member 名を取得します。

入力引数:

- `profileName`: SSH プロファイル名。
- `group`: group 名。

引数サンプル:

```json
{
  "profileName": "vps01",
  "group": "wheel"
}
```

確認文字列:

- なし。

処理内容:

Python の `grp.getgrall()` から指定 group を検索し、GID と member 名を返します。

戻り値:

- `SshToolResult`

安全上の注意:

- 読み取り専用です。
- group 名は provider の安全な名前 pattern に制限されます。

### `ssh_sudoers_check`

目的:

指定プロファイルで `sudoers_check` を実行し、1 user または group の sudoers 関連 evidence を本文なしで要約します。

入力引数:

- `profileName`: SSH プロファイル名。
- `targetType`: `user` または `group`。
- `name`: user 名または group 名。

引数サンプル:

```json
{
  "profileName": "vps01",
  "targetType": "user",
  "name": "deploy"
}
```

確認文字列:

- なし。

処理内容:

`pwd` / `grp` の公開情報、一般的な admin group、読み取り可能な `/etc/sudoers` と `/etc/sudoers.d/*` の非コメント行を固定 Python wrapper で確認し、存在有無、admin group 該当、sudoers match 件数、match source path だけを返します。

戻り値:

- `SshToolResult`

安全上の注意:

- 読み取り専用です。
- sudoers file の本文や rule 内容は返しません。
- sudoers file が現在 user から読めない場合、読める範囲だけで要約します。

### `ssh_user_usage_check`

目的:

指定 user または group が service、cron owner、主要 path の owner として使われている可能性を要約します。

入力引数:

- `profileName`: SSH プロファイル名。
- `targetType`: `user` または `group`。
- `name`: user 名または group 名。
- `limit`: 最大確認件数。省略時は `50`、最大 `200`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "targetType": "user",
  "name": "deploy",
  "limit": "50"
}
```

確認文字列:

- なし。

処理内容:

固定 Python wrapper で user / group の存在、systemd service の `User` / `Group` / `SupplementaryGroups`、system cron の実行 user、`/var/www` / `/var/log` / `/etc` 直下の owner/group 該当件数を bounded scan で確認します。

戻り値:

- `SshToolResult`

安全上の注意:

- 読み取り専用です。
- file 内容、cron 本文、service unit 本文は返しません。
- scan は固定 root と `limit` により制限され、symlink を追跡しません。

### `ssh_user_check_group_change`

目的:

既存 user の supplementary groups 変更前に、追加・削除される group、存在しない group、確認文字列、rollback 可否を実変更なしで確認します。

入力引数:

- `profileName`: SSH プロファイル名。
- `user`: 対象 user。
- `groups`: comma 区切りの group 名。
- `mode`: `append` または `replace`。省略時は `append`。

確認文字列:

- なし。この tool は実変更しません。

戻り値:

- `SshToolResult`
- stdout に `exists`, `requestedGroups`, `missingGroups`, `groupsToAdd`, `groupsToRemove`, `confirmation`, `rollbackSupported` を含みます。

安全上の注意:

- user group は変更しません。
- user 追加、削除、password 変更は対象外です。

### `ssh_user_apply_group_change`

目的:

既存 user の supplementary groups を確認付きで変更し、rollback 用 backup を作成します。

入力引数:

- `profileName`: SSH プロファイル名。
- `user`: 対象 user。
- `groups`: comma 区切りの group 名。
- `mode`: `append` または `replace`。
- `confirmation`: `user_apply_group_change:<user>:<mode>:<groups>`。

確認文字列:

- `user_apply_group_change:<user>:<mode>:<groups>`

戻り値:

- `SshToolResult`
- stdout に `user`, `mode`, `currentGroupCount`, `requestedGroupCount`, `missingGroups`, `changed`, `backupPath`, `rollbackConfirmation`, `standardErrorSummary` を含みます。

安全上の注意:

- `sudo -n usermod` 相当の変更を行います。
- group 名や既存 group 本文は返しません。
- `ssh_run_allowed_command` 経由では実行できません。

### `ssh_user_rollback_group_change`

目的:

`ssh_user_apply_group_change` が作成した最新 backup から user の supplementary groups を rollback します。

入力引数:

- `profileName`: SSH プロファイル名。
- `user`: 対象 user。
- `confirmation`: `user_rollback_group_change:<user>`。

確認文字列:

- `user_rollback_group_change:<user>`

戻り値:

- `SshToolResult`
- stdout に `user`, `backupExists`, `restoredGroupCount`, `restored`, `standardErrorSummary` を含みます。

安全上の注意:

- `sudo -n usermod` 相当の変更を行います。
- backup 本文や group 一覧本文は返しません。
- `ssh_run_allowed_command` 経由では実行できません。

### `ssh_user_check_permission_change`

目的:

既存 user の shell / login 可否 / sudo evidence 変更前に、現在 shell、変更候補、sudoers evidence 件数、確認文字列を実変更なしで確認します。

入力引数:

- `profileName`: SSH プロファイル名。
- `user`: 対象 user。
- `shell`: `/bin`, `/sbin`, `/usr/bin`, `/usr/sbin` 配下の shell path。
- `login`: `enabled`, `disabled`, `unchanged`。省略時は `unchanged`。
- `sudo`: `present`, `absent`, `unchanged`。省略時は `unchanged`。

確認文字列:

- なし。この tool は実変更しません。

戻り値:

- `SshToolResult`
- stdout に `exists`, `currentShell`, `requestedShell`, `shellExists`, `loginTarget`, `sudoTarget`, `sudoersMatches`, `confirmation`, `rollbackSupported` を含みます。

安全上の注意:

- shell、login、sudoers は変更しません。
- sudoers 本文や rule 内容は返しません。

### `ssh_user_apply_permission_change`

目的:

既存 user の login shell、login lock 状態、Kelpie 管理 sudoers entry を確認付きで変更し、rollback 用 backup を作成します。

入力引数:

- `profileName`: SSH プロファイル名。
- `user`: 対象 user。
- `shell`: `/bin`, `/sbin`, `/usr/bin`, `/usr/sbin` 配下の shell path。
- `login`: `enabled`, `disabled`, `unchanged`。
- `sudo`: `present`, `absent`, `unchanged`。
- `confirmation`: `user_apply_permission_change:<user>:<shell>:<login>:<sudo>`。

確認文字列:

- `user_apply_permission_change:<user>:<shell>:<login>:<sudo>`

戻り値:

- `SshToolResult`
- stdout に `user`, `shellChanged`, `loginChanged`, `sudoChanged`, `backupPath`, `rollbackConfirmation`, `standardErrorSummary` を含みます。

安全上の注意:

- `sudo -n usermod` と `/etc/sudoers.d/kelpie-<user>` への変更を行います。
- `sudo=present` は Kelpie 管理 sudoers entry を作成し、`visudo -cf` で検証してから反映します。
- sudoers 本文、backup 本文、既存 sudoers rule 内容は返しません。
- `ssh_run_allowed_command` 経由では実行できません。

### `ssh_user_rollback_permission_change`

目的:

`ssh_user_apply_permission_change` が作成した最新 backup から user の shell、login lock 状態、Kelpie 管理 sudoers entry を rollback します。

入力引数:

- `profileName`: SSH プロファイル名。
- `user`: 対象 user。
- `confirmation`: `user_rollback_permission_change:<user>`。

確認文字列:

- `user_rollback_permission_change:<user>`

戻り値:

- `SshToolResult`
- stdout に `user`, `backupExists`, `shellRestored`, `loginRestored`, `sudoRestored`, `restored`, `standardErrorSummary` を含みます。

安全上の注意:

- `sudo -n usermod` と `/etc/sudoers.d/kelpie-<user>` への変更を行います。
- backup 本文、sudoers 本文、既存 sudoers rule 内容は返しません。
- `ssh_run_allowed_command` 経由では実行できません。

### `ssh_user_file_ownership_check`

目的:

指定 user または group が、許可された root 配下の file / directory owner として使われているか bounded scan で確認します。

入力引数:

- `profileName`: SSH プロファイル名。
- `targetType`: `user` または `group`。
- `name`: user 名または group 名。
- `scanRoot`: scan root。`/etc`、`/home`、`/opt`、`/srv`、`/var`、`/var/log`、`/var/www` 配下の安全な path のみ。
- `depth`: 最大深さ。省略時は `2`、最大 `5`。
- `limit`: 最大一致件数。省略時は `50`、最大 `200`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "targetType": "group",
  "name": "www-data",
  "scanRoot": "/var/www",
  "depth": "2",
  "limit": "50"
}
```

確認文字列:

- なし。

処理内容:

固定 Python wrapper で `scanRoot` 配下を `depth` と `limit` の範囲で `lstat` し、owner / group が一致した path と owner/group 名を返します。

戻り値:

- `SshToolResult`

安全上の注意:

- 読み取り専用です。
- file 本文は読みません。
- symlink は追跡せず、任意 root や `/root` は受け付けません。

### `ssh_user_service_usage_check`

目的:

指定 user または group が systemd service の実行 user / group として参照されているか確認します。

入力引数:

- `profileName`: SSH プロファイル名。
- `targetType`: `user` または `group`。
- `name`: user 名または group 名。
- `limit`: 最大確認 service 数。省略時は `50`、最大 `200`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "targetType": "user",
  "name": "deploy",
  "limit": "50"
}
```

確認文字列:

- なし。

処理内容:

`systemctl list-units --type=service` で得た service を `limit` 件まで確認し、`systemctl show` の `User` / `Group` / `SupplementaryGroups` の一致だけを返します。

戻り値:

- `SshToolResult`

安全上の注意:

- 読み取り専用です。
- unit file 本文は返しません。
- service 数は `limit` で制限されます。

### `ssh_service_residual_config_check`

目的:

service uninstall 前後に、関連しやすい unit file、設定 path、log path、data directory、runtime directory の存在を確認します。

入力引数:

- `profileName`: SSH プロファイル名。
- `service`: systemd service 名。
- `limit`: 最大確認 path 数。省略時は `50`、最大 `200`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "service": "nginx.service",
  "limit": "50"
}
```

確認文字列:

- なし。

処理内容:

service 名から base name を作り、`/etc/systemd/system`、`/usr/lib/systemd/system`、`/lib/systemd/system`、`/etc/<base>`、`/etc/<base>.conf`、`/etc/<base>.d/*`、`/var/lib/<base>`、`/var/log/<base>`、`/run/<base>` の存在と種別だけを返します。

戻り値:

- `SshToolResult`

安全上の注意:

- 読み取り専用です。
- file 本文や設定本文は返しません。
- service 名は provider の安全な service name pattern に制限されます。

### `ssh_support_report_collect`

目的:

秘密情報を除外したサポート用状態レポートを読み取り専用で収集します。

入力引数:

- `profileName`: SSH プロファイル名。
- `limit`: 各 bounded section の最大行数。省略時は `20`、最大 `200`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "limit": "20"
}
```

確認文字列:

- なし。

処理内容:

kernel、OS release の公開フィールド、uptime、memory summary、disk summary、failed service 名の要約を収集します。

戻り値:

- `SshToolResult`

安全上の注意:

- 読み取り専用です。
- host 名、IP address、SSH user 名、DNS server、file 本文、設定本文、log 本文は収集対象にしません。
- disk summary は filesystem 種別、容量、使用量、mount target に限定し、source device 名は返しません。
- sanitized result を維持するため、`ssh_run_allowed_command` 経由では実行できません。

### `ssh_firewall_status`

目的:

firewalld / ufw の有無と有効状態を、rule 本文なしの要約として確認します。

入力引数:

- `profileName`: SSH プロファイル名。

確認文字列:

- なし。

戻り値:

- `SshToolResult`
- stdout に `firewalldAvailable`, `ufwAvailable`, `firewalldState`, `firewalldDefaultZone`, `firewalldServiceCount`, `ufwStatusLineCount` を含みます。

安全上の注意:

- 読み取り専用です。
- firewall rule 本文、送信元 IP、port rule の詳細は返しません。

### `ssh_firewall_check_rule`

目的:

firewalld rule 変更前に、対象 rule の存在状態と確認文字列を実変更なしで確認します。

入力引数:

- `profileName`: SSH プロファイル名。
- `action`: `add` または `remove`。
- `target`: `service` または `port`。
- `value`: service 名、または `443/tcp` のような port/protocol。
- `zone`: firewalld zone。省略時は `public`。
- `permanent`: `true` または `false`。省略時は `false`。

確認文字列:

- なし。この tool は実変更しません。

戻り値:

- `SshToolResult`
- stdout に `firewalldAvailable`, `firewalldState`, `rulePresent`, `confirmation` を含みます。

安全上の注意:

- firewalld rule は変更しません。
- `target` と `value` は固定形式に制限されます。

### `ssh_firewall_apply_rule`

目的:

`ssh_firewall_check_rule` で確認した firewalld rule 変更を、確認文字列一致時のみ適用します。

入力引数:

- `profileName`: SSH プロファイル名。
- `action`: `add` または `remove`。
- `target`: `service` または `port`。
- `value`: service 名、または `443/tcp` のような port/protocol。
- `zone`: firewalld zone。
- `permanent`: `true` または `false`。
- `confirmation`: `firewall_apply_rule:<action>:<target>:<value>:<zone>:<permanent>`。

確認文字列:

- `firewall_apply_rule:<action>:<target>:<value>:<zone>:<permanent>`

戻り値:

- `SshToolResult`
- stdout に `firewalldAvailable`, `applyExitCode`, `changed`, `standardErrorSummary` を含みます。

安全上の注意:

- `sudo -n firewall-cmd` 相当の変更を行います。
- firewalld がない場合は `ExitCode: 127` の妥当な失敗になります。
- `ssh_run_allowed_command` 経由では実行できません。

### `ssh_backup_plan_check`

目的:

provider-approved root の backup 対象範囲を bounded scan し、件数、推定 byte 数、確認文字列を実 backup なしで確認します。

入力引数:

- `profileName`: SSH プロファイル名。
- `scanRoot`: `/etc`, `/home`, `/opt`, `/srv`, `/var`, `/var/log`, `/var/www` 配下の許可 root。
- `depth`: scan depth。省略時は `2`、最大 `5`。
- `limit`: 最大確認 entry 数。省略時は `100`、最大 `200`。

確認文字列:

- なし。この tool は実 backup を作成しません。

戻り値:

- `SshToolResult`
- stdout に `scanRoot`, `exists`, `entriesScanned`, `files`, `directories`, `symlinks`, `estimatedBytes`, `confirmation` を含みます。

安全上の注意:

- file 本文と file 名一覧は返しません。
- symlink は追跡しません。

### `ssh_backup_run`

目的:

provider-approved root を bounded scan し、確認文字列一致時のみ `/var/backups/kelpie/run` 配下へ backup archive を作成します。

入力引数:

- `profileName`: SSH プロファイル名。
- `scanRoot`: `/etc`, `/home`, `/opt`, `/srv`, `/var`, `/var/log`, `/var/www` 配下の許可 root。
- `depth`: scan depth。最大 `5`。
- `limit`: 最大 archive 対象 file 数。最大 `200`。
- `confirmation`: `backup_run:<scanRoot>`。

確認文字列:

- `backup_run:<scanRoot>`

戻り値:

- `SshToolResult`
- stdout に `backupCreated`, `backupPath`, `entriesAdded`, `bytesAdded`, `archiveReadable` を含みます。

安全上の注意:

- `sudo -n` で実行し、archive path は `/var/backups/kelpie/run` に固定します。
- symlink は追跡しません。
- file 本文、file 名一覧、archive entry 名は返しません。
- `ssh_run_allowed_command` 経由では実行できません。

### `ssh_backup_verify`

目的:

`/var/backups/kelpie` 配下の許可 archive path について、存在、size、archive として読み取れるかを確認します。

入力引数:

- `profileName`: SSH プロファイル名。
- `backupPath`: `/var/backups/kelpie` 配下の `.tar`, `.tgz`, `.tar.gz` archive path。

確認文字列:

- なし。

戻り値:

- `SshToolResult`
- stdout に `exists`, `size`, `archiveReadable`, `verifyExitCode`, `standardErrorSummary` を含みます。

安全上の注意:

- archive entry 名や file 本文は返しません。
- backup archive は変更しません。

### `ssh_audit_verify`

目的:

Kelpie audit log の hash chain を、log 本文なしで検証します。

入力引数:

- `profileName`: SSH プロファイル名。
- `logPath`: `/var/log/kelpie` 配下の `.log` file。省略時は `/var/log/kelpie/audit.log`。
- `limit`: 最大確認 record 数。省略時は `100`、最大 `200`。

確認文字列:

- なし。

戻り値:

- `SshToolResult`
- stdout に `exists`, `linesScanned`, `jsonLines`, `missingHashFields`, `chainBreaks` を含みます。

安全上の注意:

- 読み取り専用です。
- log 本文、実ホスト名、IP、ユーザー名、秘密情報は返しません。
- `ssh_run_allowed_command` 経由では実行できません。

### `ssh_audit_export`

目的:

Kelpie audit log から support 向けの sanitized summary を出力します。

入力引数:

- `profileName`: SSH プロファイル名。
- `logPath`: `/var/log/kelpie` 配下の `.log` file。省略時は `/var/log/kelpie/audit.log`。
- `limit`: 最大 export record 数。省略時は `100`、最大 `200`。

確認文字列:

- なし。

戻り値:

- `SshToolResult`
- stdout に `exportVersion`, `exists`, `record`, `records` を含みます。

安全上の注意:

- 読み取り専用です。
- allowlist された `timestamp`, `eventType`, `toolName`, `commandName`, `exitCode`, `result`, `riskLevel` だけを出力します。
- raw log body、秘密情報、host 名、IP、SSH user 名は出力対象にしません。
- `ssh_run_allowed_command` 経由では実行できません。

### `ssh_check_http_local`

目的:

指定プロファイルで `check_http_local` を実行し、SSH 先自身の `127.0.0.1:<port>` の HTTP 応答を確認します。

入力引数:

- `profileName`: SSH プロファイル名。
- `port`: local TCP port。数字のみ、最大5桁。

引数サンプル:

```json
{
  "profileName": "vps01",
  "port": "80"
}
```

確認文字列:

- なし。

処理内容:

Python の `urllib.request` で `http://127.0.0.1:<port>/` だけを取得します。任意 host や任意 URL は受け付けません。

戻り値:

- `SshToolResult`

安全上の注意:

- 読み取り専用の local HTTP probe です。
- 応答本文は読み取らず、HTTP status と content type だけを返します。

### `ssh_check_tcp_connect_local`

目的:

指定プロファイルで `check_tcp_connect_local` を実行し、SSH 先自身の `127.0.0.1:<port>` へ TCP connect できるか確認します。

入力引数:

- `profileName`: SSH プロファイル名。
- `port`: local TCP port。数字のみ、最大5桁。

引数サンプル:

```json
{
  "profileName": "vps01",
  "port": "22"
}
```

確認文字列:

- なし。

処理内容:

Python の `socket.create_connection` で `127.0.0.1:<port>` だけへ接続し、接続後すぐ close します。任意 host は受け付けません。

戻り値:

- `SshToolResult`

安全上の注意:

- 読み取り専用の local TCP probe です。
- 外部 host への接続確認には使いません。

### `ssh_get_failed_services`

目的:

指定プロファイルで `get_failed_services` を実行します。

入力引数:

- `profileName`: SSH プロファイル名。

引数サンプル:

```json
{
  "profileName": "vps01"
}
```

確認文字列:

- なし。

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- `SshToolResult`

実行結果サンプル:

```json
{
  "ProfileName": "vps01",
  "CommandName": "get_failed_services",
  "ExitCode": 0,
  "StandardOutput": "0 loaded units listed.\n"
}
```

安全上の注意:

- 読み取り専用の許可済み SSH 診断です。

### `ssh_get_journal_recent`

目的:

指定プロファイルで `get_journal_recent` を実行し、service を限定しない直近 journal を取得します。

入力引数:

- `profileName`: SSH プロファイル名。
- `lines`: 取得行数。省略時は `50`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "lines": "50"
}
```

確認文字列:

- なし。

処理内容:

`journalctl -n {lines} --no-pager` を許可済み SSH 診断として実行します。

戻り値:

- `SshToolResult`

安全上の注意:

- 読み取り専用です。
- service を限定しない journal には環境情報が含まれる可能性があるため、既定行数は小さめにしています。
- `lines` は provider の引数検証を通過する必要があります。

### `ssh_service_status`

目的:

指定 systemd service の status を取得します。

入力引数:

- `profileName`: SSH プロファイル名。
- `service`: systemd service 名。例: `nginx.service`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "service": "nginx.service"
}
```

確認文字列:

- なし。

処理内容:

`systemctl status {service} --no-pager` を許可済み SSH command として実行します。

戻り値:

- `SshToolResult`

実行結果サンプル:

```json
{
  "ProfileName": "vps01",
  "CommandName": "service_status",
  "ExitCode": 0,
  "StandardOutput": "nginx.service - The nginx HTTP and reverse proxy server\\n..."
}
```

安全上の注意:

- 読み取り専用です。
- `service` は systemd service 名として安全な文字種に制限されます。

### `ssh_service_is_active`

目的:

指定 systemd service の active 状態を簡易確認します。

入力引数:

- `profileName`: SSH プロファイル名。
- `service`: systemd service 名。例: `nginx.service`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "service": "nginx.service"
}
```

確認文字列:

- なし。

処理内容:

`systemctl is-active {service}` を許可済み SSH command として実行します。

戻り値:

- `SshToolResult`

実行結果サンプル:

```json
{
  "ProfileName": "vps01",
  "CommandName": "service_is_active",
  "ExitCode": 0,
  "StandardOutput": "active\\n"
}
```

安全上の注意:

- 読み取り専用です。
- inactive / failed / unknown の場合、systemctl の仕様に従って非0終了コードが返ることがあります。
- `service` は systemd service 名として安全な文字種に制限されます。

### `ssh_service_is_enabled`

目的:

指定 systemd service の enable 状態を簡易確認します。

入力引数:

- `profileName`: SSH プロファイル名。
- `service`: systemd service 名。例: `nginx.service`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "service": "nginx.service"
}
```

確認文字列:

- なし。

処理内容:

`systemctl is-enabled {service}` を許可済み SSH command として実行します。

戻り値:

- `SshToolResult`

実行結果サンプル:

```json
{
  "ProfileName": "vps01",
  "CommandName": "service_is_enabled",
  "ExitCode": 0,
  "StandardOutput": "enabled\\n"
}
```

安全上の注意:

- 読み取り専用です。
- disabled / static / masked / unknown の場合、systemctl の仕様に従って非0終了コードが返ることがあります。
- `service` は systemd service 名として安全な文字種に制限されます。

### `ssh_list_services`

目的:

systemd service unit 一覧を取得します。

入力引数:

- `profileName`: SSH プロファイル名。
- `state`: systemd state filter。省略時は `running`。
- `limit`: 最大取得行数。省略時は `100`、最大3桁。

引数サンプル:

```json
{
  "profileName": "vps01",
  "state": "running",
  "limit": "100"
}
```

確認文字列:

- なし。

処理内容:

固定の shell wrapper から `systemctl list-units --type=service --state=<state> --no-pager --plain --all --no-legend` を実行し、先頭 `limit` 行だけを返します。`python3` は必要ありません。

戻り値:

- `SshToolResult`

実行結果サンプル:

```json
{
  "ProfileName": "vps01",
  "CommandName": "list_services",
  "ExitCode": 0,
  "StandardOutput": "nginx.service loaded active running The nginx HTTP and reverse proxy server\\n..."
}
```

安全上の注意:

- 読み取り専用です。
- `state` と `limit` は provider の引数検証を通過する必要があります。

### `ssh_tail_log`

目的:

指定プロファイルで systemd service の recent log を取得します。

入力引数:

- `profileName`: SSH プロファイル名。
- `service`: systemd service 名。例: `nginx.service`。
- `lines`: 取得行数。省略時は `100`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "service": "nginx.service",
  "lines": "100"
}
```

確認文字列:

- なし。

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- `SshToolResult`

実行結果サンプル:

```json
{
  "ProfileName": "vps01",
  "CommandName": "tail_log",
  "ExitCode": 0,
  "StandardOutput": "Jun 14 10:00:00 example nginx[123]: started\n"
}
```

安全上の注意:

- `service` と `lines` は provider の引数検証を通過する必要があります。

### `ssh_terminal_open`

目的:

指定プロファイルで PTY 付き SSH 対話ターミナルを開き、初期画面スナップショットを返します。

入力引数:

- `profileName`: SSH プロファイル名。
- `columns`: PTY の桁数。省略時は既定値。
- `rows`: PTY の行数。省略時は既定値。
- `pixelWidth`: PTY のピクセル幅。省略可。
- `pixelHeight`: PTY のピクセル高さ。省略可。

引数サンプル:

```json
{
  "profileName": "vps01",
  "columns": 120,
  "rows": 40,
  "pixelWidth": 1200,
  "pixelHeight": 800
}
```

確認文字列:

- なし。

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- terminal snapshot。

実行結果サンプル:

```json
{
  "Handle": "term-a1b2c3d4e5f6",
  "ProfileName": "vps01",
  "Columns": 120,
  "Rows": 40,
  "Text": "deploy@example:~$ ",
  "Connected": true
}
```

安全上の注意:

- 対話ターミナルは raw input を送れるため、利用者操作として扱います。

### `ssh_terminal_send`

目的:

既存の SSH 対話ターミナルへ raw input を送り、更新後の画面スナップショットを返します。

入力引数:

- `handle`: `ssh_terminal_open` が返した handle。
- `input`: ターミナルへ送信する raw input。

引数サンプル:

```json
{
  "handle": "term-a1b2c3d4e5f6",
  "input": "pwd\r"
}
```

確認文字列:

- なし。

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- terminal snapshot。

実行結果サンプル:

```json
{
  "Handle": "term-a1b2c3d4e5f6",
  "ProfileName": "vps01",
  "Text": "deploy@example:~$ pwd\r\n/home/deploy\r\ndeploy@example:~$ ",
  "Connected": true
}
```

安全上の注意:

- `input` は対話シェルへ直接送信されます。危険操作の扱いは対話セッションの安全設計に従います。

### `ssh_terminal_snapshot`

目的:

既存の SSH 対話ターミナルの現在画面スナップショットを返します。

入力引数:

- `handle`: `ssh_terminal_open` が返した handle。

引数サンプル:

```json
{
  "handle": "term-a1b2c3d4e5f6"
}
```

確認文字列:

- なし。

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- terminal snapshot。

実行結果サンプル:

```json
{
  "Handle": "term-a1b2c3d4e5f6",
  "ProfileName": "vps01",
  "Text": "deploy@example:~$ ",
  "Connected": true
}
```

安全上の注意:

- 読み取り専用です。

### `ssh_terminal_close`

目的:

既存の SSH 対話ターミナルを閉じます。

入力引数:

- `handle`: `ssh_terminal_open` が返した handle。

引数サンプル:

```json
{
  "handle": "term-a1b2c3d4e5f6"
}
```

確認文字列:

- なし。

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- close result。

実行結果サンプル:

```json
{
  "Handle": "term-a1b2c3d4e5f6",
  "Closed": true
}
```

安全上の注意:

- 対話セッションを終了します。

### `ssh_connection_close`

目的:

`ssh_terminal_open` で開いた永続 SSH terminal connection を handle 指定で閉じます。`ssh_terminal_close` と同じ接続を閉じますが、利用者の「コネクションを閉じる」という意図に対応する名前です。

入力引数:

- `handle`: `ssh_terminal_open` が返した handle。

呼び出しサンプル:

```json
{
  "name": "ssh_connection_close",
  "arguments": {
    "handle": "term-a1b2c3d4e5f6"
  }
}
```

確認文字列:

- なし。

処理内容:

指定された terminal connection を閉じ、サーバー内の terminal session 管理から削除します。通常の診断 MCP tool は呼び出しごとに SSH 接続を閉じるため、この tool の対象は主に `ssh_terminal_open` で作成した永続 terminal connection です。

戻り値:

- close result。

実行結果サンプル:

```json
{
  "Handle": "term-a1b2c3d4e5f6",
  "ProfileName": "vps01",
  "Closed": true,
  "Error": ""
}
```

安全上の注意:

- SSH 接続先の file、process、settings は変更しません。
- password session は削除しません。password session を削除する場合は `ssh_logout` を使います。

### `ssh_logout`

目的:

指定 profile の MCP server process 内 password session を削除します。`kelpiemcp forget <profile>` / `kelpiemcp logout <profile>` に相当します。

入力引数:

- `profileName`: SSH プロファイル名。

呼び出しサンプル:

```json
{
  "name": "ssh_logout",
  "arguments": {
    "profileName": "vps01"
  }
}
```

確認文字列:

- なし。

処理内容:

Profile の `PasswordSecretName` に対応する in-memory password session を削除します。秘密鍵認証 profile や password secret が未設定の profile では password session cleanup 対象がないため、失敗結果を返します。

戻り値:

- logout result。

実行結果サンプル:

```json
{
  "ProfileName": "vps01",
  "LoggedOut": true,
  "Error": ""
}
```

安全上の注意:

- SSH 接続先には接続しません。
- SSH 接続先の file、process、settings は変更しません。
- 既存の terminal connection は自動では閉じません。接続も閉じたい場合は `ssh_connection_close` を併用してください。

### `ssh_pkg_check_updates`

目的:

指定プロファイルで package update 候補を確認します。

入力引数:

- `profileName`: SSH プロファイル名。

引数サンプル:

```json
{
  "profileName": "vps01"
}
```

確認文字列:

- なし。

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- `SshToolResult`

実行結果サンプル:

```json
{
  "ProfileName": "vps01",
  "CommandName": "pkg_check_updates",
  "ExitCode": 0,
  "StandardOutput": "nginx/..."
}
```

安全上の注意:

- 読み取り専用です。

### `ssh_pkg_info`

目的:

指定 package の installed 状態、候補 version、repository 情報を確認します。

入力引数:

- `profileName`: SSH プロファイル名。
- `package`: package 名。

引数サンプル:

```json
{
  "profileName": "vps01",
  "package": "nginx"
}
```

確認文字列:

- なし。

処理内容:

Debian / Ubuntu 系では `apt-cache policy <package>`、RHEL 系では `dnf info <package>` を許可済み package 診断として実行します。

戻り値:

- `SshToolResult`

安全上の注意:

- 読み取り専用です。
- package 名は既存 package provider と同じ規則で検証します。

### `ssh_pkg_search`

目的:

指定プロファイルで package 候補を検索します。

入力引数:

- `profileName`: SSH プロファイル名。
- `query`: package 検索語。
- `limit`: 最大出力行数。省略時は `50`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "query": "nginx",
  "limit": "20"
}
```

確認文字列:

- なし。

処理内容:

Debian / Ubuntu 系では `apt-cache search`、RHEL 系では `dnf search` を固定 wrapper 経由で実行し、先頭 `limit` 行だけ返します。

戻り値:

- `SshToolResult`

安全上の注意:

- 読み取り専用です。
- 実 install、update、remove は行いません。
- query は文字種と長さを制限し、任意オプションや空白区切りの複合検索は受け付けません。

### `ssh_pkg_list_installed`

目的:

指定プロファイルで、filter に一致する installed package を確認します。

入力引数:

- `profileName`: SSH プロファイル名。
- `filter`: installed package 一覧の絞り込み文字列。
- `limit`: 最大出力行数。省略時は `50`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "filter": "nginx",
  "limit": "20"
}
```

確認文字列:

- なし。

処理内容:

Debian / Ubuntu 系では `apt list --installed`、RHEL 系では `dnf list installed` を固定 wrapper 経由で実行し、filter に一致する行だけ最大 `limit` 行返します。

戻り値:

- `SshToolResult`

安全上の注意:

- 読み取り専用です。
- 無制限の全件取得を避けるため、filter と limit を使います。
- 実 install、update、remove は行いません。

### `ssh_pkg_simulate_install`

目的:

指定 package の install dry-run を実行します。

入力引数:

- `profileName`: SSH プロファイル名。
- `package`: package 名。

引数サンプル:

```json
{
  "profileName": "vps01",
  "package": "nginx"
}
```

確認文字列:

- なし。

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- `SshToolResult`

実行結果サンプル:

```json
{
  "ProfileName": "vps01",
  "CommandName": "pkg_simulate_install",
  "ExitCode": 0,
  "StandardOutput": "Inst nginx ..."
}
```

安全上の注意:

- dry-run です。実 install は行いません。

### `ssh_pkg_install`

目的:

指定 package の install 確認要求だけを返します。SSH 先へ install は実行しません。

入力引数:

- `profileName`: SSH プロファイル名。
- `package`: package 名。

引数サンプル:

```json
{
  "profileName": "vps01",
  "package": "nginx"
}
```

確認文字列:

- なし。この tool は確認要求を返すだけです。

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- confirmation request。

実行結果サンプル:

```json
{
  "ProfileName": "vps01",
  "CommandName": "pkg_install",
  "RequiresConfirmation": true,
  "Message": "Command requires confirmation and has not been executed."
}
```

安全上の注意:

- 実 install には `ssh_pkg_install_confirmed` を使います。

### `ssh_pkg_install_confirmed`

目的:

指定 package を確認付きで install します。

入力引数:

- `profileName`: SSH プロファイル名。
- `package`: package 名。
- `confirmation`: `pkg_install:<package>`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "package": "nginx",
  "confirmation": "pkg_install:nginx"
}
```

確認文字列:

- `pkg_install:<package>`

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- `SshToolResult`

実行結果サンプル:

```json
{
  "ProfileName": "vps01",
  "CommandName": "pkg_install",
  "ExitCode": 0,
  "StandardOutput": "Setting up nginx ...\n"
}
```

確認文字列が不一致の場合:

```text
Confirmation is required: pkg_install:nginx
```

安全上の注意:

- SSH 先の package 状態を変更します。

### `ssh_pkg_simulate_remove`

目的:

指定 package の remove dry-run を実行します。

入力引数:

- `profileName`: SSH プロファイル名。
- `package`: package 名。

引数サンプル:

```json
{
  "profileName": "vps01",
  "package": "nginx"
}
```

確認文字列:

- なし。

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- `SshToolResult`

実行結果サンプル:

```json
{
  "ProfileName": "vps01",
  "CommandName": "pkg_simulate_remove",
  "ExitCode": 0,
  "StandardOutput": "Remv nginx ..."
}
```

安全上の注意:

- dry-run です。実 remove は行いません。

### `ssh_pkg_remove`

目的:

指定 package の remove 確認要求だけを返します。SSH 先へ remove は実行しません。

入力引数:

- `profileName`: SSH プロファイル名。
- `package`: package 名。

引数サンプル:

```json
{
  "profileName": "vps01",
  "package": "nginx"
}
```

確認文字列:

- なし。この tool は確認要求を返すだけです。

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- confirmation request。

実行結果サンプル:

```json
{
  "ProfileName": "vps01",
  "CommandName": "pkg_remove",
  "RequiresConfirmation": true,
  "Message": "Command requires confirmation and has not been executed."
}
```

安全上の注意:

- 現時点では確認要求のみです。確認済み remove tool を追加する場合は別途定義します。

### `ssh_service_enable_now`

目的:

指定 systemd service に対して `enable --now` 相当の操作を確認付きで実行します。

入力引数:

- `profileName`: SSH プロファイル名。
- `service`: systemd service 名。
- `confirmation`: `service_enable_now:<service>`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "service": "nginx.service",
  "confirmation": "service_enable_now:nginx.service"
}
```

確認文字列:

- `service_enable_now:<service>`

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- `SshToolResult`

実行結果サンプル:

```json
{
  "ProfileName": "vps01",
  "CommandName": "service_enable_now",
  "ExitCode": 0,
  "StandardOutput": ""
}
```

安全上の注意:

- SSH 先の service 状態を変更します。

### `ssh_service_reload`

目的:

指定 systemd service に対して `reload` 相当の操作を確認付きで実行します。

入力引数:

- `profileName`: SSH プロファイル名。
- `service`: systemd service 名。
- `confirmation`: `service_reload:<service>`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "service": "nginx.service",
  "confirmation": "service_reload:nginx.service"
}
```

確認文字列:

- `service_reload:<service>`

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- `SshToolResult`

実行結果サンプル:

```json
{
  "ProfileName": "vps01",
  "CommandName": "service_reload",
  "ExitCode": 0,
  "StandardOutput": ""
}
```

安全上の注意:

- SSH 先の service 状態を変更します。

### `ssh_service_restart`

目的:

指定 systemd service に対して `restart` 相当の操作を確認付きで実行します。

入力引数:

- `profileName`: SSH プロファイル名。
- `service`: systemd service 名。
- `confirmation`: `service_restart:<service>`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "service": "nginx.service",
  "confirmation": "service_restart:nginx.service"
}
```

確認文字列:

- `service_restart:<service>`

戻り値:

- `SshToolResult`

安全上の注意:

- SSH 先の service 状態を変更します。
- 空または不一致の `confirmation` では実行されません。

### `ssh_service_stop`

目的:

指定 systemd service に対して `stop` 相当の操作を確認付きで実行します。

入力引数:

- `profileName`: SSH プロファイル名。
- `service`: systemd service 名。
- `confirmation`: `service_stop:<service>`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "service": "nginx.service",
  "confirmation": "service_stop:nginx.service"
}
```

確認文字列:

- `service_stop:<service>`

戻り値:

- `SshToolResult`

安全上の注意:

- SSH 先の service 状態を変更します。
- 空または不一致の `confirmation` では実行されません。

### `ssh_service_disable`

目的:

指定 systemd service に対して `disable` 相当の操作を確認付きで実行します。

入力引数:

- `profileName`: SSH プロファイル名。
- `service`: systemd service 名。
- `confirmation`: `service_disable:<service>`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "service": "nginx.service",
  "confirmation": "service_disable:nginx.service"
}
```

確認文字列:

- `service_disable:<service>`

戻り値:

- `SshToolResult`

安全上の注意:

- SSH 先の service 自動起動状態を変更します。
- 空または不一致の `confirmation` では実行されません。

### `service_config_paths`

目的:

provider が対応するサービス設定ファイルの候補パスを取得します。

入力引数:

- `profileName`: SSH プロファイル名。
- `serviceKey`: 対応サービスキー。例: `nginx`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "serviceKey": "nginx"
}
```

確認文字列:

- なし。

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- config paths result。

実行結果サンプル:

```json
{
  "serviceKey": "nginx",
  "displayName": "nginx",
  "mainConfig": "/etc/nginx/nginx.conf",
  "configFiles": [
    "/etc/nginx/nginx.conf"
  ],
  "warnings": []
}
```

安全上の注意:

- 読み取り専用です。

### `service_config_file_read`

目的:

provider が許可したサービス設定ファイルを読み取ります。

入力引数:

- `profileName`: SSH プロファイル名。
- `serviceKey`: 対応サービスキー。例: `nginx`。
- `path`: 読み取る設定ファイルの full path。省略時は provider の main config を読みます。

引数サンプル:

```json
{
  "profileName": "vps01",
  "serviceKey": "nginx",
  "path": "/etc/nginx/nginx.conf"
}
```

確認文字列:

- なし。

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- config file read result。

実行結果サンプル:

```json
{
  "serviceKey": "nginx",
  "displayName": "nginx",
  "path": "/etc/nginx/nginx.conf",
  "content": "user www-data;\n...",
  "encoding": "utf-8",
  "truncated": false,
  "warnings": []
}
```

安全上の注意:

- provider が許可した設定ファイルだけを読み取ります。

### `service_config_file_check_read`

目的:

provider が許可したサービス設定ファイルを、本文を返さずに読み取り可能か確認します。

入力引数:

- `profileName`: SSH プロファイル名。
- `serviceKey`: 対応サービスキー。例: `nginx`。
- `path`: 読み取り可否を確認する設定ファイルの full path。省略時は provider の main config。

引数サンプル:

```json
{
  "profileName": "vps01",
  "serviceKey": "nginx",
  "path": "/etc/nginx/nginx.conf"
}
```

確認文字列:

- なし。

処理内容:

provider の許可範囲、path 検証、通常ファイル判定、読み取り可否を確認します。Nginx provider では既存の読み取り用内部コマンドを最小サイズで実行し、本文は戻り値に含めません。

戻り値:

- config file access check result。

実行結果サンプル:

```json
{
  "serviceKey": "nginx",
  "displayName": "Nginx",
  "path": "/etc/nginx/nginx.conf",
  "canRead": true,
  "canWrite": false,
  "requiresConfirmation": false,
  "confirmation": null,
  "method": null,
  "targetKey": null,
  "encoding": "utf-8",
  "warnings": [],
  "reason": null
}
```

安全上の注意:

- 読み取り可否だけを確認し、設定ファイル本文は返しません。
- provider が許可した設定ファイルだけを対象にします。

### `service_config_file_write`

目的:

provider が許可したサービス設定ファイルに対して、provider が認識できる限定編集だけを適用します。設定ファイル全体の Base64 本文は受け取りません。

入力引数:

- `profileName`: SSH プロファイル名。
- `serviceKey`: 対応サービスキー。例: `nginx`。
- `path`: 編集する設定ファイルの full path。
- `targetKey`: provider が解釈する編集対象。Nginx provider では `replace` / `delete` に Nginx directive path、または `server.server_name[2]` のような0始まりの match index 付き directive path を指定できます。`insert` では `line:<number>` または `<path>:<number>` を指定します。
- `targetValue`: 書き込む値または1行の設定行。`delete` では省略可。
- `method`: 編集方法。`replace`、`insert`、または `delete`。
- `confirmation`: `service_config_file_write:<serviceKey>:<path>:<method>:<targetKey>`。

引数サンプル:

`replace` の例です。`/etc/nginx/conf.d/default.conf` 内の `server` block にある `server_name` directive が1件だけ一致する場合、その値を `localhost` に置き換えます。

```json
{
  "profileName": "vps01",
  "serviceKey": "nginx",
  "path": "/etc/nginx/conf.d/default.conf",
  "targetKey": "server.server_name",
  "targetValue": "localhost",
  "method": "replace",
  "confirmation": "service_config_file_write:nginx:/etc/nginx/conf.d/default.conf:replace:server.server_name"
}
```

`insert` の例です。`/etc/nginx/nginx.conf` の110行目の前に `server_name localhost;` を挿入します。`targetValue` は `server_name localhost;` のような設定行、または `server_name:localhost` の短縮形を指定できます。

```json
{
  "profileName": "vps01",
  "serviceKey": "nginx",
  "path": "/etc/nginx/nginx.conf",
  "targetKey": "/etc/nginx/nginx.conf:110",
  "targetValue": "server_name:localhost",
  "method": "insert",
  "confirmation": "service_config_file_write:nginx:/etc/nginx/nginx.conf:insert:/etc/nginx/nginx.conf:110"
}
```

`replace` で index を指定する例です。`server.server_name[0]` は1個目の一致、`server.server_name[2]` は3個目の一致を表します。該当する一致がない場合はエラーになります。

```json
{
  "profileName": "vps01",
  "serviceKey": "nginx",
  "path": "/etc/nginx/conf.d/default.conf",
  "targetKey": "server.server_name[2]",
  "targetValue": "localhost",
  "method": "replace",
  "confirmation": "service_config_file_write:nginx:/etc/nginx/conf.d/default.conf:replace:server.server_name[2]"
}
```

`delete` の例です。`/etc/nginx/conf.d/default.conf` 内の `server` block にある `server_name` directive が1件だけ一致する場合、その行を削除します。`targetValue` は指定しません。

```json
{
  "profileName": "vps01",
  "serviceKey": "nginx",
  "path": "/etc/nginx/conf.d/default.conf",
  "targetKey": "server.server_name",
  "method": "delete",
  "confirmation": "service_config_file_write:nginx:/etc/nginx/conf.d/default.conf:delete:server.server_name"
}
```

確認文字列:

- `service_config_file_write:<serviceKey>:<path>:<method>:<targetKey>`

処理内容:

provider が許可した設定ファイルを読み取り、`method` と `targetKey` を provider 固有の matcher で解釈して一致箇所だけを編集してから、provider 内部の許可済み書き込み処理で反映します。書き込み直前に `<path>.kelpiebakup` が存在しない場合は、変更前の設定ファイル全体を `<path>.kelpiebakup` へコピーします。すでにバックアップがある場合は上書きせず、最初の変更前内容を保持します。Nginx provider では `replace` と `delete` は index 指定がない場合、一致箇所が0件または複数件なら失敗します。index 指定がある場合は指定された0始まりの一致だけを編集し、該当する一致がなければ失敗します。`insert` は指定行が範囲外の場合は失敗します。

戻り値:

- config file write result。

実行結果サンプル:

```json
{
  "serviceKey": "nginx",
  "displayName": "Nginx",
  "path": "/etc/nginx/conf.d/default.conf",
  "encoding": "utf-8",
  "bytesWritten": 128,
  "warnings": []
}
```

確認文字列がない場合:

```json
{
  "serviceKey": "nginx",
  "displayName": "",
  "path": "/etc/nginx/conf.d/default.conf",
  "encoding": "utf-8",
  "bytesWritten": 0,
  "warnings": [],
  "error": "Confirmation is required: service_config_file_write:nginx:/etc/nginx/conf.d/default.conf:replace:server.server_name"
}
```

安全上の注意:

- SSH 先の設定ファイルを変更します。
- provider が許可した設定ファイルだけを書き込みます。
- 設定ファイル全体の任意本文は受け取らず、provider が実装した限定編集だけを許可します。
- `targetKey` の matcher は MCP tool 側ではなく provider 側に実装します。アプリごとに設定ファイル形式が異なるため、`targetKey` の意味も provider ごとに異なります。
- Nginx provider では `replace` と `delete` で複数箇所に一致する編集は、`server.server_name[0]` のような index 指定がない限り拒否します。
- 変更前バックアップは `<path>.kelpiebakup` に保存します。編集を確定する場合は `service_config_file_commit`、戻す場合は `service_config_file_rollback` を実行します。

### `service_config_file_check_write`

目的:

provider が許可したサービス設定ファイルへ、指定した限定編集を書き込めるかを実変更なしで確認します。

入力引数:

- `profileName`: SSH プロファイル名。
- `serviceKey`: 対応サービスキー。例: `nginx`。
- `path`: 書き込み可否を確認する設定ファイルの full path。
- `targetKey`: provider-specific target key。
- `targetValue`: 書き込み値または挿入行。`delete` では省略可。
- `method`: `replace` / `insert` / `delete`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "serviceKey": "nginx",
  "path": "/etc/nginx/nginx.conf",
  "targetKey": "server.server_name[0]",
  "targetValue": "_",
  "method": "replace"
}
```

確認文字列:

- なし。この tool は実変更しません。

処理内容:

provider の許可範囲、path 検証、設定ファイル読み取り、provider matcher による target 解決、編集後サイズ、書き込み権限、backup path の安全性を確認します。Nginx provider では sudo 経由の非変更チェックで、本体ファイルを `r+b` で開けること、親ディレクトリ、backup path を確認します。

戻り値:

- config file access check result。

実行結果サンプル:

```json
{
  "serviceKey": "nginx",
  "displayName": "Nginx",
  "path": "/etc/nginx/nginx.conf",
  "canRead": true,
  "canWrite": true,
  "requiresConfirmation": true,
  "confirmation": "service_config_file_write:nginx:/etc/nginx/nginx.conf:replace:server.server_name[0]",
  "method": "replace",
  "targetKey": "server.server_name[0]",
  "encoding": "utf-8",
  "warnings": [],
  "reason": null
}
```

安全上の注意:

- 実際の設定ファイル本文は変更しません。
- TOCTOU を避けるため、`service_config_file_write` 側の権限検証と provider 検証は省略しません。
- `canWrite: true` は「同時点の事前診断で書き込み可能と判断した」ことを示します。実 write 成功を保証するものではありません。
- Nginx provider の write check は実 write と同じ権限前提を確認するため、MCP 経由では `Expert` mode または該当 role/policy が必要です。

### `service_config_file_rollback`

目的:

provider が許可したサービス設定ファイルを、`service_config_file_write` が作成した `<path>.kelpiebakup` から復元し、復元後にバックアップを削除します。

入力引数:

- `profileName`: SSH プロファイル名。
- `serviceKey`: 対応サービスキー。例: `nginx`。
- `path`: 復元する設定ファイルの full path。
- `confirmation`: `service_config_file_rollback:<serviceKey>:<path>`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "serviceKey": "nginx",
  "path": "/etc/nginx/nginx.conf",
  "confirmation": "service_config_file_rollback:nginx:/etc/nginx/nginx.conf"
}
```

確認文字列:

- `service_config_file_rollback:<serviceKey>:<path>`

処理内容:

provider が許可した設定ファイルだけを対象に、`/etc/nginx/nginx.conf.kelpiebakup` のような `<path>.kelpiebakup` を元の `path` へ書き戻します。書き戻しに成功したらバックアップファイルは削除されます。バックアップが存在しない場合は失敗します。

戻り値:

- config file backup action result。

実行結果サンプル:

```json
{
  "serviceKey": "nginx",
  "displayName": "Nginx",
  "path": "/etc/nginx/nginx.conf",
  "backupPath": "/etc/nginx/nginx.conf.kelpiebakup",
  "changed": true,
  "warnings": []
}
```

確認文字列がない場合:

```json
{
  "serviceKey": "nginx",
  "displayName": "",
  "path": "/etc/nginx/nginx.conf",
  "backupPath": "/etc/nginx/nginx.conf.kelpiebakup",
  "changed": false,
  "warnings": [],
  "error": "Confirmation is required: service_config_file_rollback:nginx:/etc/nginx/nginx.conf"
}
```

安全上の注意:

- SSH 先の設定ファイルを変更します。
- provider が許可した設定ファイルだけを復元対象にします。
- rollback 後はバックアップが削除されるため、同じバックアップから再 rollback はできません。

### `service_config_file_commit`

目的:

provider が許可したサービス設定ファイルの変更を確定し、`service_config_file_write` が作成した `<path>.kelpiebakup` を削除します。

入力引数:

- `profileName`: SSH プロファイル名。
- `serviceKey`: 対応サービスキー。例: `nginx`。
- `path`: 変更を確定する設定ファイルの full path。
- `confirmation`: `service_config_file_commit:<serviceKey>:<path>`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "serviceKey": "nginx",
  "path": "/etc/nginx/nginx.conf",
  "confirmation": "service_config_file_commit:nginx:/etc/nginx/nginx.conf"
}
```

確認文字列:

- `service_config_file_commit:<serviceKey>:<path>`

処理内容:

provider が許可した設定ファイルだけを対象に、`/etc/nginx/nginx.conf.kelpiebakup` のような `<path>.kelpiebakup` を削除します。設定ファイル本体は変更しません。バックアップが存在しない場合は失敗します。

戻り値:

- config file backup action result。

実行結果サンプル:

```json
{
  "serviceKey": "nginx",
  "displayName": "Nginx",
  "path": "/etc/nginx/nginx.conf",
  "backupPath": "/etc/nginx/nginx.conf.kelpiebakup",
  "changed": true,
  "warnings": []
}
```

確認文字列がない場合:

```json
{
  "serviceKey": "nginx",
  "displayName": "",
  "path": "/etc/nginx/nginx.conf",
  "backupPath": "/etc/nginx/nginx.conf.kelpiebakup",
  "changed": false,
  "warnings": [],
  "error": "Confirmation is required: service_config_file_commit:nginx:/etc/nginx/nginx.conf"
}
```

安全上の注意:

- 設定ファイル本体は変更せず、バックアップだけを削除します。
- commit 後はバックアップが残らないため、`service_config_file_rollback` では戻せません。

### `service_config_test`

目的:

provider 管理の設定テストコマンドを実行します。

入力引数:

- `profileName`: SSH プロファイル名。
- `serviceKey`: 対応サービスキー。例: `nginx`。
- `confirmation`: `service_config_test:<serviceKey>`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "serviceKey": "nginx",
  "confirmation": "service_config_test:nginx"
}
```

確認文字列:

- `service_config_test:<serviceKey>`

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- config file test result。

実行結果サンプル:

```json
{
  "serviceKey": "nginx",
  "displayName": "nginx",
  "testCommand": "nginx -t",
  "exitCode": 0,
  "standardOutput": "",
  "standardError": "nginx: configuration file /etc/nginx/nginx.conf test is successful\n",
  "warnings": []
}
```

確認文字列がない場合:

```json
{
  "serviceKey": "nginx",
  "displayName": "",
  "testCommand": "",
  "exitCode": -1,
  "standardOutput": "",
  "standardError": "Confirmation is required: service_config_test:nginx",
  "warnings": [],
  "error": "Confirmation is required: service_config_test:nginx"
}
```

安全上の注意:

- provider 管理のテストコマンドだけを実行します。

### `ssh_service_config_nginx_enable_php`

目的:

provider が許可した nginx site 設定に、PHP-FPM 連携用の固定テンプレートを適用し、対象 listen の既定 server として応答できるようにします。

入力引数:

- `profileName`: SSH プロファイル名。
- `socketPath`: PHP-FPM Unix socket path。`/run/php/php8.3-fpm.sock` のような `/run` または `/var/run` 配下の安全な absolute socket path だけを許可します。
- `confirmation`: `ssh_service_config_nginx_enable_php:<siteKey>:<socketPath>:<extension>`。
- `siteKey`: provider が解決する site key。既定値は `default`。
- `extension`: PHP-FPM へ渡す拡張子。既定値は `.php`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "siteKey": "default",
  "socketPath": "/run/php/php8.3-fpm.sock",
  "extension": ".php",
  "confirmation": "ssh_service_config_nginx_enable_php:default:/run/php/php8.3-fpm.sock:.php"
}
```

確認文字列:

- `ssh_service_config_nginx_enable_php:<siteKey>:<socketPath>:<extension>`

処理内容:

Kelpie は nginx site key を `/etc/nginx/conf.d/<site>.conf` や `/etc/nginx/sites-enabled/<site>` のような provider 許可済み site include file に解決します。`/etc/nginx/modules-enabled/*.conf` のような module include file は PHP site 設定の対象にしません。

Kelpie は対象ファイルを読み取り、次の固定テンプレートだけを適用します。対象 site file が存在しない場合は、最小限の固定 server block を新規作成してから同じテンプレートを適用します。

```nginx
listen 80 default_server;
index index.php ...

location ~ \.php$ {
    include snippets/fastcgi-php.conf;
    fastcgi_pass unix:/run/php/php8.3-fpm.sock;
}
```

任意の nginx block、`proxy_pass`、`root`、`alias` は受け取りません。設定テスト前に、`/etc/nginx/sites-enabled/<name>` の symlink 先が `listen 80 ... default_server` を含む場合は競合する有効 site とみなし、symlink target を `/etc/nginx/.kelpie-disabled-sites/` 配下へ記録してから `sites-enabled` 上の symlink だけを削除します。これにより一般的な `include /etc/nginx/sites-enabled/*;` の対象外へ退避します。regular file や provider 外の `sites-available` 本文は編集しません。書き込みと競合解消後に `nginx -t` を実行し、失敗した場合は作成済み backup から rollback し、この実行で退避した symlink も復元します。nginx の reload はこの tool では行いません。成功後に `ssh_service_reload` を別途実行します。

戻り値:

- `NginxPhpEnableResult`。
- `changed`: 既に同じ固定テンプレートがある場合は `false`。
- `tested`: `nginx -t` を実行した場合は `true`。
- `rolledBack`: 書き込み後の `nginx -t` 失敗により rollback した場合は `true`。
- `committed`: `nginx -t` 成功後に backup commit まで完了した場合は `true`。
- `warnings`: 競合する Nginx `default_server` site link を退避した場合、件数だけを含む警告が入ることがあります。

実行結果サンプル:

```json
{
  "serviceKey": "nginx",
  "displayName": "Nginx",
  "siteKey": "default",
  "path": "/etc/nginx/conf.d/default.conf",
  "socketPath": "/run/php/php8.3-fpm.sock",
  "extension": ".php",
  "changed": true,
  "tested": true,
  "rolledBack": false,
  "committed": true,
  "bytesWritten": 512,
  "warnings": []
}
```

確認文字列がない場合:

```json
{
  "serviceKey": "nginx",
  "displayName": "",
  "siteKey": "default",
  "path": null,
  "socketPath": "/run/php/php8.3-fpm.sock",
  "extension": ".php",
  "changed": false,
  "tested": false,
  "rolledBack": false,
  "committed": false,
  "bytesWritten": 0,
  "warnings": [],
  "error": "Confirmation is required: ssh_service_config_nginx_enable_php:default:/run/php/php8.3-fpm.sock:.php"
}
```

安全上の注意:

- SSH 先の nginx 設定ファイルを変更します。
- 固定テンプレートだけを適用し、任意設定 block は受け取りません。
- provider が許可した nginx site 設定ファイルだけを編集します。解決済み site file が存在しない場合は新規作成できます。
- 既存の `sites-enabled` 競合は symlink を include glob 外へ退避するだけで解消し、provider 外の `sites-available` 本文は編集しません。
- `nginx -t` 成功を必須とし、失敗時は rollback を試行します。
- nginx の reload は別操作です。結果確認後に `ssh_service_reload` を実行してください。

### `service_logfile_read`

目的:

provider が許可したアプリログファイルを読み取ります。

入力引数:

- `profileName`: SSH プロファイル名。
- `serviceKey`: 対応サービスキー。例: `nginx`。
- `logKey`: provider 定義のログキー。
- `sinceMinutes`: 直近何分を対象にするか。省略可。
- `lines`: 最大取得行数。省略時は `500`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "serviceKey": "nginx",
  "logKey": "access",
  "sinceMinutes": 60,
  "lines": 200
}
```

確認文字列:

- なし。

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- service logfile read result。

実行結果サンプル:

```json
{
  "serviceKey": "nginx",
  "displayName": "nginx",
  "logKey": "access",
  "path": "/var/log/nginx/access.log",
  "content": "127.0.0.1 - - ...\n",
  "encoding": "utf-8",
  "truncated": false,
  "warnings": []
}
```

安全上の注意:

- provider が許可したログファイルだけを読み取ります。

### `web_file_list`

目的:

指定 `siteKey` の Web 公開ルート配下から、provider が許可したディレクトリ一覧を取得します。

入力引数:

- `profileName`: SSH プロファイル名。
- `siteKey`: Web 公開サイト設定のキー。例: `default`。
- `path`: site-relative absolute directory path。省略時は `/`。
- `maxDepth`: 再帰取得の最大深さ。省略時は `0`、最大 `5`。
- `limit`: 最大件数。省略時は `100`、最大 `500`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "siteKey": "default",
  "path": "/",
  "maxDepth": 0,
  "limit": 100
}
```

確認文字列:

- なし。

処理内容:

site root 配下に解決されるディレクトリだけを対象に、ファイル名、site-relative path、種別、サイズ、owner、group、mode、mtime を取得します。

戻り値:

- web file list result。

実行結果サンプル:

```json
{
  "siteKey": "default",
  "displayName": "Default Web Site",
  "path": "/",
  "resolvedPath": "/var/www/html",
  "exists": true,
  "entries": [
    {
      "name": "index.html",
      "path": "/index.html",
      "type": "file",
      "size": 128,
      "mode": "644",
      "owner": "nginx",
      "group": "nginx",
      "depth": 0,
      "isSymlink": false
    }
  ],
  "truncated": false,
  "warnings": []
}
```

安全上の注意:

- 解決後の対象パスが Web 公開ルート外へ出る場合は拒否します。
- `limit` と `maxDepth` で取得範囲を制限します。
- symlink は追跡しません。

### `web_file_search_name`

目的:

指定 `siteKey` の Web 公開ルート配下から、provider が許可したディレクトリ範囲の file / directory 名を glob で検索します。

入力引数:

- `profileName`: SSH プロファイル名。
- `siteKey`: Web 公開サイト設定のキー。
- `pattern`: file name glob。例: `*.html`。path separator は指定できません。
- `path`: site-relative absolute directory path。省略時は `/`。
- `maxDepth`: 再帰取得の最大深さ。省略時は `3`、最大 `5`。
- `limit`: 最大 scan 件数。省略時は `100`、最大 `500`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "siteKey": "default",
  "pattern": "*.html",
  "path": "/",
  "maxDepth": 3,
  "limit": 100
}
```

確認文字列:

- なし。

処理内容:

`web_file_list` と同じ root 解決、symlink 非追跡、`maxDepth` / `limit` 制限の範囲で一覧を取得し、Kelpie 側で file name glob に一致する entry だけを返します。

戻り値:

- web file list result。

安全上の注意:

- 読み取り専用です。
- `pattern` は file name 用であり、`/` や `\` を含む path pattern は拒否します。
- 検索は bounded scan 後の filter です。大量ファイル環境では `limit` により期待する一致が返らない場合があります。

### `web_file_search_text`

目的:

指定 `siteKey` の Web 公開ルート配下から、provider が読み取りを許可したテキストファイル内の文字列を検索します。

入力引数:

- `profileName`: SSH プロファイル名。
- `siteKey`: Web 公開サイト設定のキー。
- `query`: 検索文字列。1から128文字の制御文字を含まない文字列。
- `path`: site-relative absolute directory path。省略時は `/`。
- `maxDepth`: 再帰取得の最大深さ。省略時は `3`、最大 `5`。
- `limit`: 最大一致件数。省略時は `50`、最大 `200`。
- `maxFileBytes`: 検索対象にする1ファイルの最大サイズ。省略時は `262144`、最大 `1048576`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "siteKey": "default",
  "query": "server_name",
  "path": "/",
  "maxDepth": 3,
  "limit": 50,
  "maxFileBytes": 262144
}
```

確認文字列:

- なし。

処理内容:

`web_file_list` と同じ root 解決、symlink 非追跡、`maxDepth` / `limit` 制限の範囲で一覧を取得し、読み取り許可済みかつ text 系 content type のファイルだけを bounded read して検索します。

戻り値:

- web text search result。

安全上の注意:

- 読み取り専用です。
- binary と判定される内容、UTF-8 として読めない内容、text 系ではない content type は検索対象外です。
- 検索は bounded scan 後の filter です。大量ファイル環境では `limit` により期待する一致が返らない場合があります。

### `web_file_stat`

目的:

指定 `siteKey` の Web 公開ルート配下から、provider が許可した1パスのメタデータを取得します。

入力引数:

- `profileName`: SSH プロファイル名。
- `siteKey`: Web 公開サイト設定のキー。
- `path`: site-relative absolute path。

引数サンプル:

```json
{
  "profileName": "vps01",
  "siteKey": "default",
  "path": "/index.html"
}
```

確認文字列:

- なし。

処理内容:

対象パスの存在、種別、サイズ、owner、group、mode、mtime、symlink 情報を取得します。

戻り値:

- web file stat result。

実行結果サンプル:

```json
{
  "siteKey": "default",
  "displayName": "Default Web Site",
  "path": "/index.html",
  "resolvedPath": "/var/www/html/index.html",
  "exists": true,
  "type": "file",
  "size": 128,
  "mode": "644",
  "owner": "nginx",
  "group": "nginx",
  "isSymlink": false,
  "warnings": []
}
```

安全上の注意:

- 解決後の対象パスが Web 公開ルート外へ出る場合は拒否します。

### `web_file_check_write`

目的:

指定 `siteKey` の Web 公開ルート配下へ、実書き込みなしで1ファイルの書き込み可否を確認します。

入力引数:

- `profileName`: SSH プロファイル名。
- `siteKey`: Web 公開サイト設定のキー。
- `path`: site-relative absolute file path。
- `contentType`: MIME type。省略可。

引数サンプル:

```json
{
  "profileName": "vps01",
  "siteKey": "default",
  "path": "/kelpie-mcp-test.txt",
  "contentType": "text/plain"
}
```

確認文字列:

- なし。この tool は実変更しません。

処理内容:

provider の許可範囲、path 検証、content type 検証、親ディレクトリ、既存対象の通常ファイル性、現在の SSH ユーザーでの書き込み可否を確認します。

戻り値:

- web file write check result。

実行結果サンプル:

```json
{
  "siteKey": "default",
  "displayName": "Default Web Site",
  "path": "/kelpie-mcp-test.txt",
  "resolvedPath": "/var/www/html/kelpie-mcp-test.txt",
  "exists": false,
  "canWrite": true,
  "requiresConfirmation": true,
  "confirmation": "web_file_write:default:/kelpie-mcp-test.txt",
  "contentType": "text/plain",
  "reason": null,
  "warnings": []
}
```

安全上の注意:

- 実際のファイル本文は変更しません。
- TOCTOU を避けるため、`web_file_write` 側の検証は省略しません。
- `canWrite: true` は「同時点の事前診断で書き込み可能と判断した」ことを示します。実 write 成功を保証するものではありません。

### `web_file_check_permissions`

目的:

指定 `siteKey` の Web 公開ルート配下について、owner / group / mode 変更前に対象 path と確認文字列を実変更なしで確認します。

入力引数:

- `profileName`: SSH プロファイル名。
- `siteKey`: Web 公開サイト設定のキー。
- `path`: site-relative absolute path。
- `owner`: 変更候補 owner。省略可。
- `group`: 変更候補 group。省略可。
- `mode`: 変更候補 mode。省略可。
- `recursive`: recursive 操作用の確認文字列を返すか。省略時は `false`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "siteKey": "default",
  "path": "/my_dir",
  "owner": "www-data",
  "group": "www-data",
  "mode": "775",
  "recursive": true
}
```

確認文字列:

- なし。この tool は実変更しません。

処理内容:

対象 path の存在、種別、現在の owner / group / mode、変更候補の安全性を確認し、実行可能な場合は `web_change_owner*` / `web_change_mode*` 用の confirmation を返します。

戻り値:

- web permission check result。

安全上の注意:

- 実際の owner / group / mode は変更しません。
- `root` / `0`、world-writable mode、symlink、root 外 path は拒否または変更不可として返します。
- TOCTOU を避けるため、`web_change_*` 側の検証は省略しません。

### `web_file_read`

目的:

指定 `siteKey` の Web 公開ルート配下から、provider が許可した1ファイルを読み取ります。

入力引数:

- `profileName`: SSH プロファイル名。
- `siteKey`: Web 公開サイト設定のキー。例: `default`。
- `path`: site-relative absolute path。例: `/index.html`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "siteKey": "default",
  "path": "/index.html"
}
```

確認文字列:

- なし。

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- web file read result。

実行結果サンプル:

```json
{
  "siteKey": "default",
  "displayName": "Default Web Site",
  "path": "/index.html",
  "resolvedPath": "/var/www/html/index.html",
  "exists": true,
  "encoding": "utf-8",
  "contentType": "text/html",
  "size": 128,
  "warnings": []
}
```

安全上の注意:

- 解決後の対象パスが Web 公開ルート外へ出る場合は拒否します。

### `web_file_head`

目的:

指定 `siteKey` の Web 公開ルート配下から、provider が許可した1ファイルの先頭部分だけを読み取ります。

入力引数:

- `profileName`: SSH プロファイル名。
- `siteKey`: Web 公開サイト設定のキー。例: `default`。
- `path`: site-relative absolute path。例: `/index.html`。
- `maxBytes`: 最大読み取り byte 数。省略時は `4096`、最大 `1048576`。
- `maxLines`: 最大行数。省略時は `100`。`0` の場合は行数制限なし。

引数サンプル:

```json
{
  "profileName": "vps01",
  "siteKey": "default",
  "path": "/index.html",
  "maxBytes": 4096,
  "maxLines": 100
}
```

確認文字列:

- なし。

処理内容:

Web 公開ルート外へ出ないこと、読み取り許可、content type 許可を確認したうえで、リモート側の固定 Python ラッパーで先頭を `maxBytes` / `maxLines` に制限して返します。

戻り値:

- web file read result。

安全上の注意:

- 読み取り専用です。
- `maxBytes` は site の `MaxReadBytes` も超えません。

### `web_file_tail`

目的:

指定 `siteKey` の Web 公開ルート配下から、provider が許可した1ファイルの末尾部分だけを読み取ります。

入力引数:

- `profileName`: SSH プロファイル名。
- `siteKey`: Web 公開サイト設定のキー。例: `default`。
- `path`: site-relative absolute path。例: `/logs/access.txt`。
- `maxBytes`: 最大読み取り byte 数。省略時は `4096`、最大 `1048576`。
- `maxLines`: 最大行数。省略時は `100`。`0` の場合は行数制限なし。

引数サンプル:

```json
{
  "profileName": "vps01",
  "siteKey": "default",
  "path": "/logs/access.txt",
  "maxBytes": 4096,
  "maxLines": 100
}
```

確認文字列:

- なし。

処理内容:

Web 公開ルート外へ出ないこと、読み取り許可、content type 許可を確認したうえで、リモート側の固定 Python ラッパーで末尾を `maxBytes` / `maxLines` に制限して返します。

戻り値:

- web file read result。

安全上の注意:

- 読み取り専用です。
- service log 読み取りではなく、Web 公開ルート配下の provider 許可ファイルだけを対象にします。

### `web_file_write`

目的:

指定 `siteKey` の Web 公開ルート配下へ、provider が許可した1ファイルを書き込みます。`owner` または `mode` 指定時は sudo helper 経由で一時ファイルへ内容と権限を設定してから rename で置き換えます。

入力引数:

- `profileName`: SSH プロファイル名。
- `siteKey`: Web 公開サイト設定のキー。
- `path`: site-relative absolute path。
- `contentBase64`: 書き込むファイル本文を UTF-8 bytes として Base64 化した文字列。例: `<h1>Hello Kelpie</h1>\n` を UTF-8 で Base64 化すると `PGgxPkhlbGxvIEtlbHBpZTwvaDE+Cg==`。
- `confirmation`: 実行確認文字列。
- `encoding`: テキストエンコーディング。省略可。
- `contentType`: MIME type。省略可。
- `owner`: `owner[:group]` 形式。省略可。
- `mode`: 3桁 octal mode。省略可。

引数サンプル:

この例では、Web ファイル本文として次の UTF-8 テキストを書き込みます。

```html
<h1>Hello Kelpie</h1>
```

上記テキスト末尾に改行 `\n` を付けた `<h1>Hello Kelpie</h1>\n` を UTF-8 bytes として Base64 化した値が `contentBase64` です。

```json
{
  "profileName": "vps01",
  "siteKey": "default",
  "path": "/index.html",
  "contentBase64": "PGgxPkhlbGxvIEtlbHBpZTwvaDE+Cg==",
  "contentType": "text/html",
  "encoding": "utf-8",
  "owner": "www-data:www-data",
  "mode": "775",
  "confirmation": "web_file_write:default:/index.html:www-data:www-data:775"
}
```

確認文字列:

- 通常書き込み: `web_file_write:<siteKey>:<path>`
- owner/mode 指定付き: `web_file_write:<siteKey>:<path>:<owner>:<mode>`
- owner だけ指定: `web_file_write:<siteKey>:<path>:<owner>:`
- mode だけ指定: `web_file_write:<siteKey>:<path>::<mode>`

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- web file write result。

実行結果サンプル:

```json
{
  "siteKey": "default",
  "displayName": "Default Web Site",
  "path": "/index.html",
  "resolvedPath": "/var/www/html/index.html",
  "written": true,
  "created": true,
  "overwritten": false,
  "contentType": "text/html",
  "size": 128,
  "owner": "www-data",
  "group": "www-data",
  "mode": "775",
  "warnings": []
}
```

確認文字列がない場合:

```json
{
  "siteKey": "default",
  "path": "/index.html",
  "resolvedPath": "",
  "written": false,
  "error": "Confirmation is required: web_file_write:default:/index.html"
}
```

安全上の注意:

- SSH 先の Web 公開ファイルを変更します。
- `owner` / `mode` 指定付き書き込みは `sudo -n /usr/local/libexec/kelpie/kelpie-web-permission-helper ...` 経由です。
- 解決後の対象パスが Web 公開ルート外へ出る場合は拒否します。
- `.php` などの実行可能な Web 拡張子は既定では書き込み拒否です。対象プロファイルのサイト設定で `WritableExecutableExtensions` に明示列挙されている場合だけ書き込みできます。
- `WritableExecutableExtensions` は、その書き込みについて実行可能拡張子の拒否と `AllowedExtensions` 不足だけを解除します。パストラバーサル拒否、ドットファイル拒否、秘密ファイル拒否、サイズ上限、MIME type 判定は従来どおり適用されます。

### `web_change_owner`

目的:

Web 公開ルート配下の1パスへ、sudo helper 経由で owner/group を変更します。

入力引数:

- `profileName`: SSH プロファイル名。MCP 経由では `Expert` mode が必要。
- `siteKey`: Web 公開サイト設定のキー。
- `path`: site-relative absolute path。
- `owner`: 変更後 owner。`root` / `0` は不可。
- `group`: 変更後 group。`root` / `0` は不可。
- `confirmation`: `web_change_owner:<siteKey>:<path>:<owner>:<group>`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "siteKey": "default",
  "path": "/my_dir/index.html",
  "owner": "www-data",
  "group": "www-data",
  "confirmation": "web_change_owner:default:/my_dir/index.html:www-data:www-data"
}
```

確認文字列:

- `web_change_owner:<siteKey>:<path>:<owner>:<group>`

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- web permission change result。

実行結果サンプル:

```json
{
  "siteKey": "default",
  "displayName": "Default Web Site",
  "path": "/my_dir/index.html",
  "resolvedPath": "/var/www/html/my_dir/index.html",
  "changed": true,
  "owner": "www-data",
  "group": "www-data",
  "mode": "",
  "warnings": []
}
```

安全上の注意:

- SSH 先の owner/group を変更します。
- `sudo -n /usr/local/libexec/kelpie/kelpie-web-permission-helper ...` 経由です。

### `web_change_owner_recursive`

目的:

Web 公開ルート配下の1ディレクトリツリーへ、sudo helper 経由で owner/group を再帰変更します。

入力引数:

- `profileName`: SSH プロファイル名。MCP 経由では `Expert` mode が必要。
- `siteKey`: Web 公開サイト設定のキー。
- `path`: site-relative absolute path。
- `owner`: 変更後 owner。`root` / `0` は不可。
- `group`: 変更後 group。`root` / `0` は不可。
- `confirmation`: `web_change_owner_recursive:<siteKey>:<path>:<owner>:<group>`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "siteKey": "default",
  "path": "/my_dir",
  "owner": "www-data",
  "group": "www-data",
  "confirmation": "web_change_owner_recursive:default:/my_dir:www-data:www-data"
}
```

確認文字列:

- `web_change_owner_recursive:<siteKey>:<path>:<owner>:<group>`

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- web permission change result。

確認文字列がない場合:

```json
{
  "siteKey": "default",
  "displayName": "",
  "path": "/my_dir",
  "resolvedPath": "",
  "changed": false,
  "owner": "www-data",
  "group": "www-data",
  "mode": "",
  "warnings": [],
  "error": "Confirmation is required: web_change_owner_recursive:default:/my_dir:www-data:www-data"
}
```

安全上の注意:

- SSH 先の owner/group を再帰変更します。
- recursive 処理は symlink を追跡せず、配下の symlink は変更対象から除外します。
- 対象パスそのものが symlink の場合も拒否します。

### `web_change_mode`

目的:

Web 公開ルート配下の1パスへ、sudo helper 経由で mode を変更します。

入力引数:

- `profileName`: SSH プロファイル名。MCP 経由では `Expert` mode が必要。
- `siteKey`: Web 公開サイト設定のキー。
- `path`: site-relative absolute path。
- `mode`: 3桁 octal mode。world-writable は不可。
- `confirmation`: `web_change_mode:<siteKey>:<path>:<mode>`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "siteKey": "default",
  "path": "/my_dir/index.html",
  "mode": "775",
  "confirmation": "web_change_mode:default:/my_dir/index.html:775"
}
```

確認文字列:

- `web_change_mode:<siteKey>:<path>:<mode>`

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- web permission change result。

確認文字列がない場合:

```json
{
  "siteKey": "default",
  "displayName": "",
  "path": "/my_dir",
  "resolvedPath": "",
  "changed": false,
  "owner": "",
  "group": "",
  "mode": "775",
  "warnings": [],
  "error": "Confirmation is required: web_change_mode:default:/my_dir:775"
}
```

安全上の注意:

- SSH 先の mode を変更します。
- world-writable になる mode は拒否します。

### `web_change_mode_recursive`

目的:

Web 公開ルート配下の1ディレクトリツリーへ、sudo helper 経由で mode を再帰変更します。

入力引数:

- `profileName`: SSH プロファイル名。MCP 経由では `Expert` mode が必要。
- `siteKey`: Web 公開サイト設定のキー。
- `path`: site-relative absolute path。
- `mode`: 3桁 octal mode。world-writable は不可。
- `confirmation`: `web_change_mode_recursive:<siteKey>:<path>:<mode>`。

引数サンプル:

```json
{
  "profileName": "vps01",
  "siteKey": "default",
  "path": "/my_dir",
  "mode": "775",
  "confirmation": "web_change_mode_recursive:default:/my_dir:775"
}
```

確認文字列:

- `web_change_mode_recursive:<siteKey>:<path>:<mode>`

処理内容:

対象 tool の目的に沿って、許可された範囲の読み取り、確認要求、または確認済み変更操作を実行します。

戻り値:

- web permission change result。

確認文字列がない場合:

```json
{
  "siteKey": "default",
  "displayName": "",
  "path": "/my_dir",
  "resolvedPath": "",
  "changed": false,
  "owner": "",
  "group": "",
  "mode": "775",
  "warnings": [],
  "error": "Confirmation is required: web_change_mode_recursive:default:/my_dir:775"
}
```

安全上の注意:

- SSH 先の mode を再帰変更します。
- recursive 処理は symlink を追跡せず、配下の symlink は変更対象から除外します。
- 対象パスそのものが symlink の場合も拒否します。

## 安全メモ

- MCP callable tool は `kelpie` / `kelpiemcp` のターミナル実行コマンドとは別物として扱います。
- 変更操作は confirmation 文字列が一致しない限り実変更しません。
- SSH プロファイルの `Mode` / provider の許可範囲 / path 解決結果により、tool 呼び出し自体が拒否される場合があります。
- Web 公開ファイル tool の `path` は site-relative absolute path とし、解決後に Web 公開ルート外へ出る場合は拒否します。
- `web_file_write` の owner/mode 指定付き書き込みと `web_change_*` は `sudo -n /usr/local/libexec/kelpie/kelpie-web-permission-helper ...` 経由で実行します。
- SSH 先に `kelpie-web-permission-helper` が未配置の場合、Web 権限変更系は `sudo: /usr/local/libexec/kelpie/kelpie-web-permission-helper: command not found` などで失敗します。
- sudoers では `python3`、`chown`、`chmod` を直接許可せず、Kelpie 専用 helper だけを許可します。
- `owner` / `group` に `root` または `0` は指定できません。
- `mode` は3桁 octal のみ許可し、world-writable になる mode は拒否します。

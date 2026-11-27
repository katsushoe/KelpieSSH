# ホストの通常再起動

## 権限と導入手順

保存済みのDebian/RHEL系SSHプロファイルに対して通常再起動を一度だけ要求します。`HostReboot.Enabled`、MaintenanceまたはExpertモード、root以外のSSHユーザー、固定した`Host.HostKeyFingerprintSha256`が必要です。任意コマンドや`SshRemoteOperation`は受け付けません。CLI専用のPoliciesだけでは有効になりません。

サービス所有者が対象、実行ユーザー、期待するカーネル、稼働確認項目を決めます。ホスト管理者がSSHホスト鍵を別経路で確認し、限定したsudo権限を設定します。クライアントはプレビューの対象と停止影響を人間の管理者に提示し、要求ごとに明示的な承認を得てください。確認トークンは呼び出し側の意思確認を表し、承認者本人を独立して認証する仕組みではありません。

管理者は対象ホストの実行ファイルの配置を確認し、`visudo`で次のように引数まで限定した規則を登録できます。アカウント名だけを対象ユーザーへ置き換えます。

```sudoers
kelpieops ALL=(root) NOPASSWD: /usr/bin/systemctl reboot
```

sudoersファイルと実行ファイルはrootが所有し、SSHユーザーから書き換えられないようにします。引数のワイルドカード、シェル、他のsystemctl操作、強制再起動は許可しません。実装は明示設定したsudoとsystemctlのパスを使い、引数は`-n`と`reboot`に固定します。例のパスでは`/usr/bin/sudo -n /usr/bin/systemctl reboot`となり、sudoパスワードを対話入力しません。読み取り用に`/usr/bin/cat`、`/usr/bin/uname`、`/usr/bin/systemctl`、HTTP確認時は`/usr/bin/curl`が必要です。前提不足はホスト管理者が明示的に修正してください。

ローカル管理者はプロファイル、`dat/host-reboots/`、ログを信頼しない利用者の書き込みから保護し、必要なCLI/MCP実行主体へ権限を付与します。CLIとMCPは同じデータディレクトリを使ってください。有効化や有効な再起動設定の変更は信頼登録上の権限拡張です。内容を確認した管理者が既存の`kelpiemcp profile add`または`profile reload --approve-privilege-expansion`で承認します。設定、sudo権限、ホスト鍵を自動変更しません。

## プロファイル設定

ルートへ任意の`HostReboot`オブジェクトを追加します。省略時は再起動無効です。

| 項目 | 必須 | 初期値 | 内容 |
| :--- | :--- | :--- | :--- |
| `Paths` | いいえ | 空object | 下記のリモートパス。実行ファイルの自動既定値なし。 |
| `Enabled` | いいえ | `false` | 管理者による明示的な有効化。 |
| `ExpectedKernel` | いいえ | 空文字 | 再起動後の`uname -r`との完全一致。空文字は任意の有効なカーネルを許容。 |
| `Services` | いいえ | 空 | 再起動後に稼働を求めるsystemdユニット。最大32件、各1～128文字。 |
| `HttpUrls` | いいえ | 空 | SSHホストから確認するHTTP(S)の絶対URL。最大16件、各2048文字以下。認証情報、フラグメント、制御文字は禁止。 |
| `TimeoutSeconds` | いいえ | `120` | 要求送信からの総確認期限。10～600秒。 |
| `PollSeconds` | いいえ | `3` | 確認間隔。1～30秒。 |

```json
{
  "HostReboot": {
    "Enabled": false,
    "Paths": {
      "Sudo": "/usr/bin/sudo",
      "Systemctl": "/usr/bin/systemctl",
      "Cat": "/usr/bin/cat",
      "Uname": "/usr/bin/uname",
      "Curl": "/usr/bin/curl",
      "BootId": "/proc/sys/kernel/random/boot_id",
      "NullDevice": "/dev/null"
    },
    "ExpectedKernel": "",
    "Services": ["nginx.service"],
    "HttpUrls": ["http://127.0.0.1/health"],
    "TimeoutSeconds": 120,
    "PollSeconds": 3
  }
}
```

有効化時は`Paths.Sudo`、`Systemctl`、`Cat`、`Uname`、`BootId`が必須です。HTTP確認を指定する場合は`Curl`と`NullDevice`も必須です。各パスの初期値は空文字で、512文字以下のPOSIX絶対パスを要求します。空白、シェル構文、ドット・ドット2個のパス要素は禁止です。末尾名はそれぞれ`sudo`、`systemctl`、`cat`、`uname`、`boot_id`、`curl`、`null`とします。管理者が所有者と配置を確認し、sudoersの対象を設定したsystemctlパスと一致させます。例のパスへ自動的に切り替えません。

期待するカーネルは英数字と`_.+~-`の128文字以下です。ユニット名の先頭は英数字またはアンダースコア、残りは英数字と`_@.:-`です。シェル構文やパターンを拒否します。HTTP確認はSSHホスト側から行い、2xxのみ成功とし、TLSを検証し、転送先へ追従しません。認証情報を含まない公開可能な確認用URLだけを指定してください。URLは操作記録へ保存されます。全項目の成功を要求します。空配列の場合、サービスやHTTPの正常性は保証しません。

## CLIとMCPの入出力

| 手順 | CLI | MCPツールと入力 |
| :--- | :--- | :--- |
| プレビュー | `kelpie host-reboot preview <profile>` | `ssh_host_reboot_preview(profileName)` |
| 要求 | `kelpie host-reboot request <profile> <operationId> <confirmation>` | `ssh_host_reboot_request(profileName, operationId, confirmation)` |
| 確認 | `kelpie host-reboot inspect <profile> <operationId> [--wait]` | `ssh_host_reboot_inspect(profileName, operationId, wait=false)` |

プレビューは読み取りSSH確認を行い、10分有効の記録を保存します。対象、指紋、ユーザー、現在のboot IDとカーネル、`Checks`、`Impact`、`Confirmation`を管理者へ提示します。次回起動するカーネルは自動推定せず、`NextKernel`はnullです。`ExpectedKernel`は管理者の期待値であり、検出した次回カーネルではありません。`ssh_get_capabilities`は3ツールの利用可否を返します。汎用の許可コマンド実行から再起動することはできません。

次の例の操作IDとトークンは、実際のプレビューの値に置き換えます。CLIの出力JSONはPascalCase、MCPの入力引数はcamelCaseです。

```text
kelpie host-reboot preview sample
kelpie host-reboot request sample <OperationId-from-preview> <Confirmation-from-preview>
kelpie host-reboot inspect sample <same-OperationId> --wait
```

```json
{"name":"ssh_host_reboot_inspect","arguments":{"profileName":"sample","operationId":"11111111111111111111111111111111","wait":true}}
```

返却する`HostRebootOperation`は`OperationId`、`TargetKey`、`Binding`、`ProfileName`、`Host`、`Port`、`UserName`、`HostKeyFingerprint`、`BeforeBootId`、`BeforeKernel`、`Checks`、`CreatedAt`、`DispatchedAt`、`State`、`Error`、`BootId`、`Kernel`、`RebootObserved`、`Reconnected`、`FailedServices`、`FailedHttpUrls`、`DispatchRejected`、`Confirmation`、`Impact`、`NextKernel`、`Deadline`を持ちます。ハッシュは対象と承認済み設定の識別用で、認証情報ではありません。未確認の起動情報や失敗一覧はnull、日時はUTCオフセット付きです。トークンは`host_reboot:<ProfileName>:<OperationId>`との完全一致を要求します。送信前に操作IDを保存し、応答喪失時も自動で別IDを作成しないでください。

CLIはJSONを出力し、エラーのない結果で終了コード0、エラーまたはFailed/Unknownで非0を返します。不正入力、プロファイル拒否、記録の欠損・破損、対象不一致は送信せず例外となり、MCPではツールエラーになります。対象がロック中なら直ちに失敗するため既存操作を確認します。確認のキャンセルで送信履歴を消去しません。

## 状態、エラー、復旧

| 状態 | 意味 |
| :--- | :--- |
| `Preview` | 未送信。 |
| `Dispatching` | 送信意図を保存済み。プロセス終了時は送信済みの可能性あり。 |
| `Accepted` | 通常再起動コマンドが0で終了。完了は未確認。 |
| `Unknown` | 送信や再接続の結果が不明。自動再送禁止。 |
| `Reconnected` | SSH応答あり。boot IDの変更または稼働確認が未完了。 |
| `RebootObserved` | 稼働確認前にboot IDの変更を保存済み。 |
| `Verified` | boot ID変更、期待するカーネル、指定した稼働確認がすべて成功。 |
| `Failed` | 明確な拒否または検証失敗。 |

| エラー | 扱い |
| :--- | :--- |
| `ApprovalRequired`, `PreviewExpired`, `PreviewStale`, `ProfileChanged` | 未送信。原因解消後、新しいプレビューと承認が必要。 |
| `OperationInProgress` | 同じ固定ホスト鍵の未解決操作が新規送信を抑止。 |
| `PermissionDenied` | sudoが再起動要求を拒否。権限を自動変更しない。 |
| `RequestRejected` | 通常再起動コマンドがその他の非0結果で終了。 |
| `HostKeyMismatch`, `AuthenticationFailed` | セキュリティ上の失敗として停止。管理者が調査。 |
| `DispatchUnknown` | 送信意図保存後の応答喪失、期限切れ、キャンセル。同じIDを確認。 |
| `ReconnectPending`, `RebootNotObserved` | 元の期限内だけ確認を継続。 |
| `ReconnectTimeout` | 確認期限切れ。boot ID変更未確認ならUnknown。 |
| `BootChangedDuringVerification` | 稼働確認中に再び起動情報が変わったため検証未完了。 |
| `KernelMismatch` | boot IDは変更されたが、実行カーネルが期待値と不一致。 |
| `ServiceStopped`, `HttpFailed` | 稼働確認未達。待機指定なら期限まで確認し、未達なら失敗。 |

確認は再起動を送信せず、`Deadline`を延長しません。確認完了まで承認済みのモードとHostReboot設定を維持し、不一致はProfileChangedで拒否します。同じプロファイル、接続先、ユーザー、固定ホスト鍵を要求します。既存IDで再要求しても保存された結果を返し、再送しません。未解決操作はプロセス再起動後も新規操作を抑止します。期限切れ後も記録を残し、自動消去、強制再起動、権限拡張へ切り替えません。管理者は別経路で実際の起動状態を確定してから保護された記録を整合させてください。MCPは明示的な`--dat-dir`を含む操作記録の保存先を起動時に確定します。CLIとMCPで操作履歴を共有するには同じデータディレクトリを指定します。異なるデータディレクトリでは重複抑止を共有しないため、別々の導入環境から送信しないでください。

## 検証

自動テストでは成功、権限・設定拒否、古いプレビュー、重複、永続化とプロセス中断、応答喪失、再接続期限、ホスト鍵・認証失敗、カーネル不一致、サービス停止、HTTP失敗を確認します。実ホストの再起動には別途承認した保守時間帯が必要です。設計判断は[ADR-0005](../adr/ADR-0005-HOST-REBOOT-VERIFICATION.md)、コマンドの外部仕様は[systemctlの公式資料](https://www.freedesktop.org/software/systemd/man/latest/systemctl.html)を参照してください。

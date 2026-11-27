# nginx設定helper

[English](../NGINX_HELPER.md) | [日本語](NGINX_HELPER.ja.md)

## 対象と前提

nginx設定変更providerは、nginx protocol 1に対応した`kelpie-web-permission-helper` 0.2.4.0以降を使用します。初期対応はLinux x64です。特権Python経路6件を置き換えますが、既存の設定/ログ読取は非特権Python経路を維持します。今回の移行だけで読取のPython依存やPHP/Node代替を解消しません。

管理者が管理するhelper更新手順で導入します。実行ファイルと祖先はroot所有かつgroup/others書込不可にします。SSHプロファイルやWebPublicSitesとは別に`/etc/kelpie/nginx-config-helper-policy.json`を設定します。[サンプル](../../config_samples/nginx-config-helper-policy.json)の初期値は呼出者/対象をすべて拒否します。失敗を理由にポリシー・sudoers・runtimeを自動導入しません。

設定した状態ディレクトリをroot所有・0700で事前作成します。対象SSHアカウントにはレビューしたnginx helperの固定起動と、別途レビューした`nginx -t`だけを許可し、汎用interpreterやshellを許可しません。helperは実効UID 0と、ポリシーに列挙した非0の`SUDO_UID`を要求します。これにより対象アカウントへnginx設定管理権限を明示付与します。helperがアプリの確認画面自体を認証するわけではありません。

## ポリシー

厳密なJSONで、未知フィールドとキー重複を拒否します。ファイルと全祖先はroot所有・group/others書込不可にし、ポリシー/状態領域のsymlinkを拒否します。

| キー | 必須 | 初期値 | 意味 |
| :--- | :--- | :--- | :--- |
| `protocol` | はい | サンプルは`1` | 必須のprotocol版。 |
| `revision` | はい | サンプルは`configure-before-use` | 管理者の改訂識別子。ポリシーのバイト列全体をトランザクションに結び付けます。 |
| `stateDirectory` | はい | サンプルは`/var/lib/kelpie/nginx-transactions` | root所有の非公開・永続記録ディレクトリ。 |
| `callerUids` | はい | 空 | sudo経由で許可する非0の利用者UID。 |
| `targets` | はい | 空 | 正規化した設定パスの完全一致。 |
| `directories` | はい | 空 | 直下ファイルを許可する親ディレクトリの完全一致。再帰許可ではありません。 |
| `sitesEnabledDirectory` | いいえ | 空文字 | 既定サイトsymlink用の任意のroot所有ディレクトリ。 |
| `siteTargetDirectories` | はい | 空 | そのリンクの解決先に許可する親ディレクトリの完全一致。 |

設定symlinkは論理パスと解決先の通常ファイルを両方許可します。信頼したディレクトリを辿り、追跡しないdescriptor相対操作で論理リンクを維持します。未承認先・書込可能な祖先・通常ファイル以外・データファイルのhard linkを拒否します。既定サイトは別の計画済み効果で、設定ディレクトリ内のport 80 `default_server`に一致するリンクだけを解除します。確認するディレクトリエントリは最大64件で、任意の再帰探索をしません。

## helperの契約

`nginx-capabilities`はsudoなしでprotocol/操作の情報を返しますが、権限付与や対象ポリシーの存在保証ではありません。操作は固定helperパス・操作名・引数`1`で起動し、stdinへJSON要求を1件送ります。

| 操作 | 動作 |
| :--- | :--- |
| `nginx-plan 1` | 具体的な前提と非公開計画を永続化します。nginxのファイル/リンクは変更しません。 |
| `nginx-check 1` | ポリシー・ファイル/バックアップの準備状態を調べ、対象やバックアップを作りません。 |
| `nginx-apply 1` | 確認したダイジェストと期待状態に基づき、効果を永続記録しながら適用します。 |
| `nginx-status 1` | ホストプロセスの再起動後も含め、トランザクションを読取/照合します。 |
| `nginx-commit 1` | 呼出側の設定検証後に当該バックアップを確定処理します。 |
| `nginx-rollback 1` | 当該変更の適用後状態が維持されている効果だけを復元します。 |

要求はcamelCaseの`protocol`、`requestId`（GUIDの16進32文字）、`target`と、任意の`contentBase64`、`expectedHash`、`expectedExists`、`disableDefaultSites`、`transactionId`、`intentDigest`です。トランザクション/ダイジェストの省略は空文字として扱い、明示nullは拒否します。Base64は転送表現であり暗号化や許可ではありません。適用には計画のID・ダイジェスト・同一内容が必要です。再起動後の状態照合/復旧は、対象と呼出UIDから一意の進行中トランザクションを解決できます。

成功/失敗応答は`protocol`、要求/トランザクションID、`ok`、`state`、ファイル/リンク変更数、`bytesWritten`、意図/元/現在のハッシュ、`recoverable`、固定エラー分類を返します。失敗は終了値1とstdoutのJSONで返し、例外原文や設定本文を返しません。未対応/旧版helperは安全に失敗し、interpreterへ切り替えません。

要求最大256 KiB、復号後設定65536 bytes、応答64 KiB、受け付ける診断stderr 4 KiBです。設定はNULを含まない妥当なUTF-8とし、JSON解析は16階層までです。ホスト側タイムアウトは30秒、不明な変更への状態照会は別途10秒上限で1回行います。状態ロックはプロセス共有・非待機式です。計画期限は5分で、記録256件に達すると新規計画を拒否します。運用者が進行中の復旧記録を消さずに保持/掃除を管理します。

## 復旧と公開応答

隣接バックアップの接尾辞`.kelpiebakup`を維持します。元の存在は明示メタデータで記録し、設定内容を削除sentinelにしません。旧形式/無関係なバックアップは取込/上書きせず拒否します。移行前に旧`.kelpiebakup`や`.kelpie-disabled-sites`を照合し、処理を通すために不明なバックアップを削除しないでください。

ファイルの設置前にハッシュとinodeを記録します。復元時は競合編集・同内容の別inodeへの置換・リンク再作成を拒否します。複数ファイル/リンク全体は原子的ではなく、中断後も復旧記録を保持します。状態照合では競合を表し、復元済みと偽りません。変更の自動反復やサービスの再読込/再起動はしません。

`service_config_file_write`、`service_config_file_rollback`、`service_config_file_commit`、`ssh_service_config_nginx_enable_php`の入力/確認規則と既存フィールドを維持し、`OutcomeKnown`を追加します。falseなら効果の情報が不完全なため、再試行前に照合してください。エラーは変更なしの証拠ではありません。失われた確定/復元応答は、状態照合で完了を確認できれば変更を反復せず解消できます。

PHP有効化はファイルとリンクをまとめて変更し、固定`nginx -t`で検証、成功後に確定、検証失敗時に両方を復元します。本当の無変更は変更/検証/確定なし・0 bytesです。確定時の掃除失敗は復旧資料を残し、nginx設定不正とは区別します。

## 更新と検証

所有者と復旧意図を確認するまで旧バックアップを残します。helper/providerを対応版へ更新し、独立ポリシーと限定sudo範囲を確認します。helper不在やパス拒否を理由に許可を広げません。既存読取は従来の依存と許可に従って利用できます。

導入前にprotocol/provider単体試験と、使い捨てLinux/SSH/nginxで実効果・新規ファイル・symlink・競合編集・ポリシー変更・サイズ境界・応答喪失・再起動照合を検証します。合成データだけを使います。構成判断と代替案は[ADR-0006](ADR-0006-NGINX-HELPER-TRANSACTIONS.ja.md)を参照してください。

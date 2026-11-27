# 隔離したruntime情報

CLIは`kelpie runtime-info <profile> <php|node> [--refresh]`、MCPは`ssh_runtime_info(profileName, runtime, refresh=false)`です。

runtimeは`php`または`node`の完全一致です。パス、コード、モジュール名、生引数、環境変数辞書は受け付けません。現在のホスト鍵を固定した非rootのSSHプロファイルを使い、通常のコマンド権限評価を経由します。ReadOnly、Safe、Maintenance、ExpertのCLI/MCPで読取操作として利用できます。新しい設定、helper、sudo、パッケージ導入は不要です。

結果には`protocol=1`、ホスト生成の32文字`requestId`、`operation=runtime.info`、`runtime`、`status`、`startupMode`、`version`、`features.json`、`features.sha256`、ホストUTCの`checkedAt`、`cached=false`、`warnings=[]`を含みます。成功時は`status=ok`、`startupMode=isolated`、数値の正規化版、JSONが真、明示測定したSHA-256の真偽を返します。失敗時の起動方式・版・機能はnullです。パス、設定本文、stderr、環境値、プロセス引数は返しません。

状態は`ok`、`unavailable`、`unsupported`、`denied`、`invalid-input`、`invalid-response`、`timeout`、`cancelled`、`connection-failed`です。同種類の固定候補が不在の場合だけunavailable、信頼できない候補はdenied、起動前提やメジャー版の不対応はunsupportedです。重複・未知項目・容量超過・要求不一致等の応答はinvalid-responseです。CLI終了コードはokのみ0、その他は1です。CLI構文不正やプロファイル不在は既存CLIの入力エラー経路、MCPのプロファイル不在は既存MCPの入力エラー経路を使います。

毎回現在のプロファイルと実行候補を検証します。本段階では準備・結果キャッシュを持たず、refresh指定にかかわらず新規測定します。機能一覧への登録は操作の実装を示し、runtimeの導入や正常稼働を保証しません。既存inventoryの動作は変更しません。

[起動の信頼境界](RUNTIME_DIAGNOSTIC_STARTUP.ja.md)と[ADR-0007](ADR-0007-BOUNDED-RUNTIME-STARTUP.ja.md)を参照してください。D1の準備確認はライブラリ内のままです。登録する固定定義は`runtime_php_info`と`runtime_node_info`です。対応版はPHP 8、Node 22/24です。D3受入はDebian bookwormの専用環境と権限・プロトコル境界の単体試験を組み合わせ、全OSや実AIデスクトップアプリの動作保証は含みません。PHPはphp.iniを使わない隔離CLIの組込み機能で、FPMやアプリ稼働正常とは区別します。Nodeの読込先は製品固定の組込みfs/cryptoのみで、アプリ読込・子プロセス・通信は操作に含みません。

上限は要求4096 bytes、stdout16384 bytesと通信フレーム96 bytes、stderr2048 bytes、JSON深度8、数値版64文字、リモート処理5秒、全体15秒です。リモート期限の終了137もtimeoutへ正規化します。通信取消だけでは任意の子プロセス終了を保証できないため、固定起動処理に独立した期限を設けます。自動再試行や別runtimeへの切替はしません。

## 成功結果の例

固定起動では、PHPの終了1（CGI実行ファイルを含むCLIオプション拒否）とNodeの終了9（不正な起動オプション）を、それぞれのruntimeに限って`unsupported`へ分類します。他の非0終了は、期限・信頼・候補不在の分類に該当しなければ`invalid-response`です。リモートのエラー本文は返さず、起動拒否後の再試行もしません。[PHPのCLI専用オプション](https://www.php.net/manual/en/features.commandline.options.php)と[Nodeの終了コード](https://nodejs.org/docs/latest-v22.x/api/process.html#exit-codes)を参照してください。

```json
{
  "protocol": 1,
  "requestId": "0123456789abcdef0123456789abcdef",
  "operation": "runtime.info",
  "runtime": "php",
  "status": "ok",
  "startupMode": "isolated",
  "version": "8.2.35",
  "features": {
    "json": true,
    "sha256": true
  },
  "checkedAt": "2026-10-27T00:00:00Z",
  "cached": false,
  "warnings": []
}
```

# 任意runtimeの起動基盤

[英語版](../RUNTIME_DIAGNOSTIC_STARTUP.md)。信頼境界と代替案は[採用した設計](ADR-0007-BOUNDED-RUNTIME-STARTUP.ja.md)を正とします。

## 対象

D1は、将来のPHP/Node診断向けライブラリ基盤です。`RuntimeDiagnosticStartup.CreateProbeDefinition("php" | "node")`は固定の読取専用定義を作り、利用者のコード・実行パス・生引数を受け取りません。既定カタログには登録していません。新CLI・MCP呼出ツール・プロファイル設定は追加せず、アダプター、登録、構造化した機能応答は後続です。

## 起動契約

固定候補は`/usr/bin/php`、`/usr/local/bin/php`、`/usr/bin/node`、`/usr/local/bin/node`です。信頼済みGNU stat/readlinkを使い、所有者、書込・set-idビット、ディレクトリ、リンク成分を調べます。root所有のシステムリンクは信頼済みで検索可能な親を通る場合だけ許可します。最大16リンク・128成分、リンク先は長さを制限したASCIIのシステムパスに限定します。実行直前に再走査して解決先・属性を比較します。存在する危険な候補では失敗し、同種類の次候補へ進めるのは固定候補がない場合だけです。

信頼済みの/bin/sh、env、base64、stat、readlink、timeoutと固定候補の親が前提です。必要なOS機能がなければ導入・sudoを行わず失敗します。作業場所は/で、環境を固定PATH・HOME・ロケールだけへ初期化します。PHPは`-n -r`とCLI SAPI検証、Nodeは`--input-type=commonjs --no-addons --eval`を使います。固定の準備検査だけを実行し、成功時は`KELPIE_RUNTIME_READY_V1`を出力します。アプリ稼働正常、設定済みPHP拡張、FPM、提案中のruntime.info応答とは異なります。

GNU timeoutで5秒後にリモートruntimeグループを停止します。要求全体の期限は15秒です。リモート期限は通信のTimedOutではなく終了137になる場合があり、後続アダプターで固定契約を解釈する必要があります。取消・接続断だけで任意コマンドの終了を断定せず、runtime段階には独立したリモート期限を持たせます。再試行、別runtimeへの切替、autoload・import、パッケージ操作、サービス変更は行いません。

## 上限付きSSHの任意指定契約

| ライブラリ項目 | 動作 |
| :--- | :--- |
| `AllowedCommandDefinition.ExecutionLimits` | 任意の上限で、通常のコマンド・権限評価後にSshCommandServiceが転送します。nullは従来経路です。 |
| `SshCommandRequest.ExecutionLimits` | 直接受信で容量を制限する経路を選びます。文字列入力だけに対応します。 |
| `SshCommandExecutionLimits` | stdoutは1〜2097152、stderrは1〜65536、入力は0〜65536バイト。起動検査は16384/2048/4096です。 |
| `IgnoreEnvironmentOverrides` | 固定定義が保存済み・プロファイルの環境exportを除外し、起動処理も継承変数を初期化します。 |

非PTYシェルと無作為な終了フレームを使い、SshCommandによる全文蓄積を避けます。出力上限とフレーム用96バイト、独立したstderr上限の固定容量です。超過時は部分本文を返さず、安全なSshCommandOutputLimitExceptionを返します。不正UTF-8・未完了フレームも失敗です。モード・経路規則は変えず、呼出側は現在の信頼済みプロファイルを解決して通常の権限評価を通す必要があります。

SSHプロファイル、信頼ストア、環境ストア、helperポリシー、sudoersの変更は不要です。検出runtimeをプロファイルへ保存しません。サーバーが未対応でも上限なし通信へ戻しません。既存コマンドに自動で上限を付ける変更ではありません。

## 検証と後続段階

単体では容量、UTF-8のバイト計算、完了フレーム、標準入力のデータ扱い、定義の転送を検査します。隔離した実SSHではPHP/Node起動、継承フック、大量出力、取消・期限、リンク信頼、指定runtime不在を確認します。診断アダプター全体の受入とは区別します。後続で型付き応答、機能情報の鮮度、権限の全条件、計画済みruntime試験、公開ツール仕様を追加してから診断利用可能と扱います。

## D2アダプター

専用CLI/MCPを追加しました。[runtime情報](RUNTIME_INFO.ja.md)を参照してください。D1の準備確認は未登録です。D2は毎回新規測定し、準備・結果を保持しません。D3全条件の受入は後続です。

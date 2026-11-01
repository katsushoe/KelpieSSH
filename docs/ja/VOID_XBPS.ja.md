# Void Linux XBPS provider

## 選択と設定

Platform.OsFamilyはvoidまたはvoidlinux（大小文字不問）を受け付け、voidlinuxはvoidへ正規化します。PackageManager省略時はxbps、明示指定も可能です。異なる管理方式なら選択しません。[設定例](../../config_samples/servers/void.json)を参照してください。公開tool/CLI引数は追加せず、信頼済みプロファイルから既存パッケージ操作を選択します。runitサービス操作やWeb/Nginxの全コマンド互換性まで保証するものではありません。

## 操作

| 内部操作 | 接続先コマンド | 上限・期限 | リスク |
| :--- | :--- | :--- | :--- |
| pkg_check_updates | xbps-install -un | 60秒 | 読取専用の模擬操作 |
| pkg_info | xbps-query -R package | 30秒 | 読取専用 |
| pkg_search | xbps-query -Rs query | 先頭0..999行・45秒 | 読取専用 |
| pkg_list_installed | xbps-query -l後に大小文字を無視した文字列フィルター | 先頭0..999行・45秒 | 読取専用 |
| pkg_simulate_install | xbps-install -n package | 60秒 | 読取専用の模擬操作 |
| pkg_simulate_remove | xbps-remove -n package | 60秒 | 読取専用の模擬操作 |
| pkg_install | sudo -n xbps-install -y package | 10分 | 明示確認が必要 |
| pkg_remove | sudo -n xbps-remove -y package | 10分 | 明示確認が必要 |

packageは先頭をASCII英数字とし、英数字と. + : _ -の1..128文字です。query/filterは同じ文字規則の1..64文字です。シェル断片・空白・パス区切り・先頭オプションは拒否します。検索はXBPS標準のパターン意味、導入済みフィルターは文字列として評価します。行数は0..999、既存の公開既定値を維持し、0では行を返しません。フィルター/切詰め後もXBPSの終了コードを維持します。出力・非0終了・標準エラー・期限切れ・取消は既存応答へ変換します。

## 状態と安全性

読取/模擬操作では-S/-Mを使わず、キャッシュ済み索引を参照します。索引は古い場合や不在の場合があります。providerが索引更新・署名鍵取込・sudo導入を行うことはありません。リポジトリ設定と信頼は管理者が管理します。実変更には既存のモード/確認判定と管理者設定済みsudo権限が必要です。-yでも新しい署名鍵の確認は残る場合があり、期限内で失敗させ、管理者が対処します。版比較演算子を含むパッケージ式は今回の許可対象外です。

## 受入

隔離Void Linux・XBPS 0.60.7で非root実SSH15件が合格しました。読取/模擬操作のDB・キャッシュ・リポジトリのハッシュ不変、存在しないpackageの失敗、行数、ReadOnly拒否、合成packageの導入/削除を確認しました。公開CLI4件でvoidlinux別名・管理方式省略と未実行の確認プレビューも合格しました。Release回帰は1236成功・失敗0・Unix専用3スキップ（計1239件）、追加provider21件で選択・コマンド生成・危険引数を検証しました。runit・自動索引更新・システム全更新は対象外です。

根拠: [query](https://man.voidlinux.org/xbps-query.1)・[install](https://man.voidlinux.org/xbps-install.1)・[remove](https://man.voidlinux.org/xbps-remove.1)・[公式イメージ](https://github.com/void-linux/void-containers)。[設計判断](ADR-0015-VOID-XBPS.ja.md)。

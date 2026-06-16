# DOC_STANDARD.ja.md Version
2026.06.14

# 変更履歴
- 2026.06.14

# 目的
このファイルは、KelpieSSH 固有のドキュメント・API命名・互換性判断ルールを定義する。
共通のドキュメント標準は `D:\Workspace\sazysoft\AI_prompt\DOC_STANDARD.md` を正とし、本ファイルは KelpieSSH 固有の補足ルールを置く。

# MCP_COMMANDS.ja.md ルール
KelpieSSH の MCP callable tool は、通常のターミナルで実行する CLI コマンドとは利用経路、引数形式、確認文字列、戻り値が異なるため、`COMMANDS.ja.md` ではなく `MCP_COMMANDS.ja.md` に記載する。

`MCP_COMMANDS.ja.md` は `COMMANDS.ja.md` の派生ドキュメントとして扱い、構成と記述粒度は共通標準の `COMMANDS.md` 標準に従う。各 tool には目的、入力引数、確認文字列、戻り値、実行結果サンプル、安全上の注意を記載する。

`COMMANDS.ja.md` は `kelpie` / `kelpiemcp` など、利用者がターミナルから直接実行する CLI コマンド、サービス制御、外部連携コマンドの正本とする。MCP callable tool の仕様や実行例は `MCP_COMMANDS.ja.md` を正本とし、必要な場合のみ `COMMANDS.ja.md` から概要または参照を置く。

# MCP_COMMAND_SCENARIO.ja.md / MCP_COMMAND_TEST.ja.md ルール
KelpieSSH の MCP callable tool は、CLI コマンドとはテスト経路と安全確認が異なるため、CLI 向けの `COMMAND_SCENARIO.ja.md` / `COMMAND_TEST.ja.md` とは分離し、`MCP_COMMAND_SCENARIO.ja.md` / `MCP_COMMAND_TEST.ja.md` に記載する。

`MCP_COMMAND_SCENARIO.ja.md` は `COMMAND_SCENARIO.ja.md` の派生ドキュメントとして扱い、MCPクライアント経由で callable tool を呼び出すシナリオの正本とする。各シナリオには ID、分類、tool 名、目的、実SSH要否、実変更要否、手順、期待結果、安全上の注意を記載する。

`MCP_COMMAND_TEST.ja.md` は `COMMAND_TEST.ja.md` の派生ドキュメントとして扱い、MCP callable tool テスト結果の正本とする。結果サマリ、結果記号、実施結果を記録し、実ホスト名、実ユーザー名、秘密鍵、パスワード、公開前の設定値は記録しない。

変更系 tool のシナリオでは、空または不一致の `confirmation` による非破壊確認を優先する。実変更を行う場合は provider が許可した安全な対象に限定し、`service_config_file_write` 後は必ず `service_config_file_commit` または `service_config_file_rollback` で backup workflow を閉じる。

# MCPクライアント互換性ルール
KelpieSSH の MCP クライアント向け tool は、現時点では公開されて間もないため、後方互換性を重視しすぎない。

tool 名、引数名、確認文字列、戻り値の形が不自然または曖昧な場合は、既存名へ alias を追加するより、分かりやすい正規名へ置き換えることを優先する。

## alias 実装ルール
MCP tool 名、引数名、確認文字列、戻り値の互換 alias を実装する場合は、作業前にユーザーへ確認する。

alias を追加する判断が必要になる例:

- 旧 tool 名を残す。
- 旧引数名を受け付ける。
- 旧確認文字列を受け付ける。
- 旧戻り値フィールドを併記する。

ユーザーから明示的に互換 alias が必要だと指示された場合のみ、alias を実装する。

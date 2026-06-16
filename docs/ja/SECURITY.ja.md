# セキュリティポリシー

English documentation is available in [SECURITY.md](../../SECURITY.md).

## 対応バージョン

KelpieSSH は現在 early alpha 開発段階です。セキュリティ修正は、最新の公開リリースと既定の開発ブランチへ適用します。

## 脆弱性の報告

セキュリティ問題は、利用可能な場合は GitHub security advisories を使って非公開で報告してください。利用できない場合は、リポジトリ owner profile 経由で maintainer へ連絡してください。

秘密情報の露出、認証回避、安全でないコマンド実行、remote host への意図しない access につながる脆弱性を public issue に投稿しないでください。

## セキュリティモデル

KelpieSSH は、SSH 越しの VPS 診断と保守を補助しつつ、コマンド実行を制限することを目的とします。

- SSH profile で直接 `root` login を使ってはいけません。
- 平文 password を JSON 設定ファイルに保存してはいけません。
- password authentication は実行中の `KelpieMCPServer` session の memory にのみ保持します。
- SSH command execution は policy-based で、allow-listed diagnostic operations から始めます。
- 危険な変更操作は dedicated command、policy check、confirmation string を通す必要があります。

## 秘密情報の扱い

- Private keys、passwords、passphrases、real host names、real user names を commit しないでください。
- Production `profiles/*.json`、`keys/`、`dat/`、`logs/` は public repository の外で管理してください。
- MCP 経由では、`Expert` mode でも password や private key の値を返してはいけません。

## 利用者の責任

- SSH key の権限と保管場所を適切に管理してください。
- KelpieSSH 用の SSH user は必要最小限の権限にしてください。
- Test target と production target を混同しないでください。
- Confirmation が必要な operation は、対象と影響範囲を確認してから実行してください。

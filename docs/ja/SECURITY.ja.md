# セキュリティポリシー

特権リモートWeb policyの安全境界は[ADR-0001](../adr/ADR-0001-REMOTE-WEB-POLICY-MANAGEMENT.md)を正本とします。この操作は人間専用で、MCP callable toolには公開しません。
人間専用の特権helper更新boundaryは[ADR-0002](../adr/ADR-0002-PRIVILEGED-HELPER-UPDATE.md)を正本とします。

English documentation is available in [SECURITY.md](../../SECURITY.md).

## 対応バージョン

KelpieSSH は現在 early alpha 開発段階です。セキュリティ修正は、最新の公開リリースと既定の開発ブランチへ適用します。

## 脆弱性の報告

セキュリティ問題は、利用可能な場合は GitHub security advisories を使って非公開で報告してください。

GitHub security advisories を利用できない場合は、[shoe0604@akatsukisoft.com](mailto:shoe0604@akatsukisoft.com) へ直接連絡してください。

秘密情報の露出、認証回避、安全でないコマンド実行、remote host への意図しない access につながる脆弱性を public issue に投稿しないでください。

報告時は、影響を受ける version、環境、再現手順、想定される影響、関連する log や screenshot を含めてください。ただし、実 password、private key、passphrase、本番 profile files、秘密情報を含む raw log は含めないでください。

## セキュリティモデル

KelpieSSH は、SSH 越しの VPS 診断と保守を補助しつつ、コマンド実行を制限することを目的とします。

- SSH profile で直接 `root` login を使ってはいけません。
- 平文 password を JSON 設定ファイルに保存してはいけません。
- password authentication は実行中の `KelpieMCPServer` session の memory にのみ保持します。
- SSH command execution は policy-based で、allow-listed diagnostic operations から始めます。
- Path-based operations は `AllowedRoots` と `SpecialPaths` で制限してください。
- MCP tools は secrets を表示してはいけません。
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

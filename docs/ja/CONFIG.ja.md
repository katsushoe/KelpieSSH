# KelpieSSH 設定

最終更新: 2026-06-18

この文書は、KelpieSSH の設定ファイル配置と host level settings をまとめる公開リファレンスです。
Profile の詳細な設定ガイダンスは [PROFILE_GUIDE.ja.md](PROFILE_GUIDE.ja.md) を参照してください。
英語版 profile guide は [../../PROFILE_GUIDE.md](../../PROFILE_GUIDE.md) です。

## 設定ディレクトリ

KelpieSSH はローカルの Kelpie home directory を使います。
既定の手動配置では次の場所です。

```text
D:\Kelpie
```

通常の構成は次のとおりです。

```text
D:\Kelpie
├─ config
│  ├─ kelpie.json
│  └─ kelpiemcp.json
├─ profiles
│  └─ sample.json
├─ keys
├─ dat
├─ logs
└─ bin
```

## ファイル生成

`kelpie init` はローカルの directory layout と sample files を作成します。
既存ファイルは上書きしません。

公開サンプルは repository の `config_samples/` 配下にあります。
これらは例であり、実ホストや secrets を含めてはいけません。

```text
config_samples/
├─ kelpie.json
├─ kelpiemcp.json
└─ servers/
   └─ vps01.json
```

## Main Settings

### `config/kelpie.json`

`kelpie` command が読む設定ファイルです。

| Setting | Purpose |
| :--- | :--- |
| `LogDirectory` | CLI logs の出力先。 |
| `OpenProfile` | `kelpie open <profile>` で最後に開いた profile 名。 |
| `Server:ControlPipeName` | `kelpie` / `kelpiemcp` が server control に使う local named pipe。 |
| `Commands:ExecutablePath` | 任意の `kelpie` command 明示 path。 |
| `Commands:WorkingDirectory` | 任意の command working directory。 |
| `editor` | `kelpie profile edit <profile>` のエディタモードで使う任意の editor command。引数も指定できます。 |

最小例:

```json
{
  "LogDirectory": "D:\\Kelpie\\logs",
  "editor": ""
}
```

`kelpie profile edit <profile>` は editor を次の順に解決します。

1. `config/kelpie.json` の `editor`
2. `KELPIE_EDITOR`
3. `VISUAL`
4. `EDITOR`
5. OS 既定: Windows は `notepad`、Unix は `vi`

エディタプロセスはブロッキング起動し、Kelpie はエディタ終了後に profile を検証します。
通常は即時終了するエディタを使う場合は、次のように待機オプション付きで設定します。

```json
{
  "editor": "code --wait"
}
```

special value:

| Value | 意味 |
| :--- | :--- |
| `Notepad` | 大文字小文字を区別せず Windows Notepad を起動します。 |
| `default` | 大文字小文字を区別せず、OS が `.json` に関連付けたアプリで profile file を開きます。 |

### `config/kelpiemcp.json`

`kelpiemcp` と `KelpieMCPServer` が読む設定ファイルです。

| Setting | Purpose |
| :--- | :--- |
| `AllowedHosts` | local MCP server の HTTP Host allow-list。 |
| `Server:ControlPipeName` | `kelpiemcp` が server control に使う local named pipe。 |
| `LogDirectory` | MCP server logs の出力先。 |
| `Commands:ExecutablePath` | 任意の `KelpieMCPServer` executable path。 |
| `Commands:WorkingDirectory` | 任意の server working directory。 |
| `ProfileOperations` | profile trust 操作を呼び出し経路ごとに許可または拒否する設定。既定では CLI 操作を許可し、MCP 操作を拒否する。 |

既定の MCP endpoint は次のとおりです。

```text
http://127.0.0.1:45432/mcp
```

ブラウザで health check する場合は次を使います。

```text
http://127.0.0.1:45432/health
```

公開ポートは永続的な `kelpiemcp.json` 設定ではなく、起動時オプションです。`KelpieMCPServer` の起動時に `--port <port-number>` で指定します。設定範囲は `1`～`65535`、未指定時の既定値は `45432` です。既存ファイルに従来の `Server.Port` が残っていても無視され、次回 `kelpie init` で設定を更新するときに削除されます。

最小例:

```json
{
  "LogDirectory": "D:\\Kelpie\\logs",
  "Server": {
    "ControlPipeName": "KelpieMCPServer.Control"
  },
  "ProfileOperations": {
    "Add": {
      "CLI": "Allow",
      "MCP": "Deny"
    },
    "Reload": {
      "CLI": "Allow",
      "MCP": "Deny"
    },
    "Revoke": {
      "CLI": "Allow",
      "MCP": "Deny"
    }
  }
}
```

### `ProfileOperations`

`ProfileOperations` は、profile trust 操作を呼び出し経路ごとに制御します。
各操作は `CLI` と `MCP` の設定を持ちます。

設定値:

| 値 | 意味 |
| :--- | :--- |
| `Allow` | その経路で操作を許可する。 |
| `Deny` | その経路で操作を拒否する。 |

互換のため、旧値も読み取ります。`Allowed` と boolean `true` は `Allow`、boolean `false` は `Deny` として扱います。

既定値:

| Setting | 既定値 | 目的 |
| :--- | :--- | :--- |
| `ProfileOperations:Add:CLI` | `Allow` | `kelpiemcp profile add <profile>` を許可する。 |
| `ProfileOperations:Reload:CLI` | `Allow` | `kelpiemcp profile reload <profile>` を許可する。 |
| `ProfileOperations:Revoke:CLI` | `Allow` | `kelpiemcp profile revoke <profile>` を許可する。 |
| `ProfileOperations:Add:MCP` | `Deny` | MCP 経由の profile add は公開しない。 |
| `ProfileOperations:Reload:MCP` | `Deny` | `ssh_profile_capabilities` が返す `ReloadAllowed` を制御する。 |
| `ProfileOperations:Revoke:MCP` | `Deny` | MCP 経由の profile revoke は公開しない。 |

CLI 操作が拒否されている場合、該当 command は `Success: false`、`Status: disabled-by-config` の JSON を返します。
`kelpiemcp profile-capabilities [profile]` は trust store の状態と `ProfileOperations:*:CLI` の両方を反映した `AddAllowed`、`ReloadAllowed`、`RevokeAllowed` を返します。

`ProfileOperations:Reload:MCP` が `Deny` の場合、`ssh_profile_capabilities` は `ReloadAllowed: false` と `Reason: disabled-by-config` を返します。
profile file の変更受け入れは、既定では次のユーザー側明示 command で行う設計です。

```powershell
kelpiemcp profile add <profile>
kelpiemcp profile reload <profile>
kelpiemcp profile revoke <profile>
```

`ProfileOperations:Reload:MCP` を `Allow` にするのは、運用者が MCP client へ接続中 profile の reload capability を見せることを明示的に許可する場合だけにしてください。
その場合でも trusted profile hash validation は引き続き適用されるため、この flag だけで編集済み profile file が無条件に受け入れられることはありません。

## Runtime State

### `dat/storm_state.dat`

`storm_state.dat` は `kelpie` CLI の runtime state を保存します。
ユーザーが通常編集する設定ファイルではありません。

例:

```json
{
  "OpenProfile": "vps01",
  "ClientMode": "cli"
}
```

| Setting | Purpose |
| :--- | :--- |
| `OpenProfile` | `kelpie open <profile>` で最後に開いた profile。`kelpie login` が参照します。 |
| `ClientMode` | `kelpie gui` / `kelpie cli` などで選択した client mode。 |

## SSH Profiles

SSH profiles は `profiles/` 配下の JSON ファイルです。
ファイル名が profile 名になるため、`profiles/vps01.json` は profile `vps01` です。

Profile 詳細は [PROFILE_GUIDE.ja.md](PROFILE_GUIDE.ja.md) を参照してください。

よく使う command:

```powershell
kelpie init vps01
kelpie profile show vps01
kelpie open vps01
kelpie login
```

## Log Directory Resolution

Log directory は次の順で解決します。

1. 現在の command が読む設定ファイルの `LogDirectory`
2. `KelpieHome/logs`
3. startup directory 配下の `logs`
4. startup directory

`LogDirectory` が相対パスの場合は、設定ファイルの directory から解決します。

## Security Notes

- 実 profile files を commit しないでください。
- private keys、passwords、passphrases、real host names、real user names を commit しないでください。
- production `profiles/`、`keys/`、`dat/`、`logs/` は public repository の外に置いてください。
- plain text password を JSON 設定ファイルに保存してはいけません。

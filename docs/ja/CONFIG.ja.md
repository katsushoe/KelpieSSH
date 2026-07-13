# KelpieSSH 設定

最終更新: 2026-07-12

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

Kelpie home は次の順で解決します。

1. runtime path override として `--bin-dir <dir>` が指定されている場合、`<dir>` の親ディレクトリを Kelpie home とします。
2. `KELPIE_HOME` が設定され、そのディレクトリが存在する場合、`KELPIE_HOME` を Kelpie home とします。
3. それ以外の場合、startup directory の親ディレクトリを Kelpie home とします。

KelpieSSH は `KELPIEPRO_HOME` を読みません。

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

安全に検証する場合は、`config_samples/servers/vps01.json` を `KelpieHome/profiles/vps01.json` へコピーし、ローカル Docker SSH コンテナなどの使い捨て SSH ターゲット向けに編集してから、次を実行します。

```powershell
kelpie config check
kelpie profile check vps01
```

check コマンドは、SSH 接続を開始する前にローカル設定とプロファイルファイルを検証します。実ホスト名、実ユーザー名、秘密鍵、パスワード、パスフレーズ、raw log をサンプルファイルへコピーしないでください。

## Main Settings

### `config/kelpie.json`

`kelpie` command が読む設定ファイルです。

| Setting | 必須 | 初期値 | Purpose |
| :--- | :---: | :--- | :--- |
| `LogDirectory` | いいえ | `KelpieHome\logs` | CLI logs の出力先。 |
| `OpenProfile` | いいえ | なし | `kelpie open <profile>` で最後に開いた profile 名。通常は runtime state として `dat/kelpie_client_state.json` に保存します。 |
| `Server:ControlPipeName` | いいえ | なし | `kelpie` / `kelpiemcp` が server control に使う local named pipe。通常は `kelpiemcp.json` に設定し、server へ接続する command では有効値が必要です。 |
| `Commands:ExecutablePath` | いいえ | なし | 任意の `kelpie` command 明示 path。 |
| `Commands:WorkingDirectory` | いいえ | なし | 任意の command working directory。 |
| `Editor` | いいえ | 空文字 | `kelpie profile edit <profile>` のエディタモードで使う任意の editor command。引数も指定できます。 |

最小例:

```json
{
  "LogDirectory": "D:\\Kelpie\\logs",
  "Editor": ""
}
```

`kelpie profile edit <profile>` は editor を次の順に解決します。

1. `config/kelpie.json` の `Editor`
2. `KELPIE_EDITOR`
3. `VISUAL`
4. `EDITOR`
5. OS 既定: Windows は `notepad`、Unix は `vi`

互換性のため、旧小文字 `editor` も受理します。`kelpie.json` に `editor` が含まれる場合、`kelpie` コマンドは実行ごとに標準出力へ `Editor` へのリネームを促す warning を表示します。Kelpie が設定ファイルを更新するタイミングでは `Editor` に正規化します。

エディタプロセスはブロッキング起動し、Kelpie はエディタ終了後に profile を検証します。
通常は即時終了するエディタを使う場合は、次のように待機オプション付きで設定します。

```json
{
  "Editor": "code --wait"
}
```

special value:

| Value | 意味 |
| :--- | :--- |
| `vscode` | 大文字小文字を区別しない VS Code `code` CLI の別名です。Windows では可能な場合に `PATH` / `PATHEXT` から `code` を解決するため、`"Editor": "vscode --wait"` でインストール済みの `code.cmd` を実パス決め打ちなしに使えます。 |
| `Notepad` | 大文字小文字を区別せず Windows Notepad を起動します。 |
| `default` | 大文字小文字を区別せず、OS が `.json` に関連付けたアプリで profile file を開きます。 |

### `config/kelpiemcp.json`

`kelpiemcp` と `KelpieMCPServer` が読む設定ファイルです。

| Setting | 必須 | 初期値 | Purpose |
| :--- | :---: | :--- | :--- |
| `AllowedHosts` | いいえ | `localhost;127.0.0.1;[::1]` | local MCP server の HTTP Host allow-list。 |
| `Server:ControlPipeName` | はい | `KelpieMCPServer.Control` | `kelpiemcp` が server control に使う local named pipe。 |
| `LogDirectory` | いいえ | `KelpieHome\logs` | MCP server logs の出力先。 |
| `Commands:ExecutablePath` | いいえ | Windows では `KelpieHome\bin\mcp\KelpieMCPServer.exe` | 任意の `KelpieMCPServer` executable path。 |
| `Commands:WorkingDirectory` | いいえ | `KelpieHome\bin` | 任意の server working directory。 |
| `ProfileOperations` | いいえ | CLI `Allow`、MCP `Deny` | profile trust 操作を呼び出し経路ごとに許可または拒否する設定。既定では CLI 操作を許可し、MCP 操作を拒否する。 |

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
    "Port": 45432,
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

| Setting | 必須 | 初期値 | 目的 |
| :--- | :---: | :--- | :--- |
| `ProfileOperations:Add:CLI` | いいえ | `Allow` | `kelpiemcp profile add <profile>` を許可する。 |
| `ProfileOperations:Reload:CLI` | いいえ | `Allow` | `kelpiemcp profile reload <profile>` を許可する。 |
| `ProfileOperations:Revoke:CLI` | いいえ | `Allow` | `kelpiemcp profile revoke <profile>` を許可する。 |
| `ProfileOperations:Add:MCP` | いいえ | `Deny` | MCP 経由の profile add は公開しない。 |
| `ProfileOperations:Reload:MCP` | いいえ | `Deny` | `profile_reload` の実行と `ssh_profile_capabilities` が返す `ReloadAllowed` の両方を制御する。 |
| `ProfileOperations:Revoke:MCP` | いいえ | `Deny` | MCP 経由の profile revoke は公開しない。 |

CLI 操作が拒否されている場合、該当 command は `Success: false`、`Status: disabled-by-config` の JSON を返します。
`kelpiemcp profile-capabilities [profile]` は trust store の状態と `ProfileOperations:*:CLI` の両方を反映した `AddAllowed`、`ReloadAllowed`、`RevokeAllowed` を返します。

`ProfileOperations:Reload:MCP` が `Deny` の場合、`profile_reload` はcatalogを変更せず `Status: forbidden` を返し、`ssh_profile_capabilities` は `ReloadAllowed: false` と `Reason: disabled-by-config` を返します。
profile file の変更受け入れは、既定では次のユーザー側明示 command で行う設計です。

```powershell
kelpiemcp profile add <profile>
kelpiemcp profile reload <profile>
kelpiemcp profile revoke <profile>
```

`ProfileOperations:Reload:MCP` を `Allow` にするのは、運用者が MCP client にprofile reloadの実行と接続中profileの reload capability表示を明示的に許可する場合だけにしてください。
その場合でも trusted profile hash validation は引き続き適用されるため、この flag だけで編集済み profile file が無条件に受け入れられることはありません。

暗号化trust storeには、信頼済みprofileごとの正規化authorization snapshotも保存します。`kelpiemcp profile reload <profile>`で権限拡張を検出した場合、管理者が返却された変更項目を確認し、`--approve-privilege-expansion`を付けて再実行するまで拒否します。

## Runtime State

### `dat/kelpie_client_state.json`

`kelpie_client_state.json` は `kelpie` CLI の runtime state を保存します。
ユーザーが通常編集する設定ファイルではありません。

`kelpie_client_state.json`が存在せず、旧`storm_state.dat`が存在する場合は一度だけrename移行します。正規fileがすでに存在する場合は正規fileを優先し、旧fileを読み取りfallbackや上書き対象にしません。

例:

```json
{
  "OpenProfile": "vps01"
}
```

| Setting | 必須 | 初期値 | Purpose |
| :--- | :---: | :--- | :--- |
| `OpenProfile` | いいえ | なし | `kelpie open <profile>` で最後に開いた profile。`kelpie login` が参照します。 |

## SSH Profiles

SSH profiles は `profiles/` 配下の JSON ファイルです。
ファイル名が profile 名になるため、`profiles/vps01.json` は profile `vps01` です。

Profile 詳細は [PROFILE_GUIDE.ja.md](PROFILE_GUIDE.ja.md) を参照してください。

よく使う command:

```powershell
kelpie init vps01
kelpie config check
kelpie profile check vps01
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

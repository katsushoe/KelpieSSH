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
| `Server:Port` | MCP server が使う local HTTP port。 |
| `Server:ControlPipeName` | `kelpie` / `kelpiemcp` が server control に使う local named pipe。 |
| `Commands:ExecutablePath` | 任意の `kelpie` command 明示 path。 |
| `Commands:WorkingDirectory` | 任意の command working directory。 |

最小例:

```json
{
  "LogDirectory": "D:\\Kelpie\\logs"
}
```

### `config/kelpiemcp.json`

`kelpiemcp` と `KelpieMCPServer` が読む設定ファイルです。

| Setting | Purpose |
| :--- | :--- |
| `AllowedHosts` | local MCP server の HTTP Host allow-list。 |
| `Server:Port` | MCP endpoint の local HTTP port。 |
| `Server:ControlPipeName` | `kelpiemcp` が server control に使う local named pipe。 |
| `LogDirectory` | MCP server logs の出力先。 |
| `Commands:ExecutablePath` | 任意の `KelpieMCPServer` executable path。 |
| `Commands:WorkingDirectory` | 任意の server working directory。 |
| `ProfileOperations:Reload:MCP` | MCP client に MCP 経由の profile reload 可否を見せる設定。既定は `false`。正規の profile file 編集受け入れは `kelpiemcp profile reload <profile>` を使う。 |

既定の MCP endpoint は次のとおりです。

```text
http://127.0.0.1:45432/mcp
```

ブラウザで health check する場合は次を使います。

```text
http://127.0.0.1:45432/health
```

最小例:

```json
{
  "LogDirectory": "D:\\Kelpie\\logs",
  "Server": {
    "Port": 45432,
    "ControlPipeName": "KelpieMCPServer.Control"
  },
  "ProfileOperations": {
    "Reload": {
      "MCP": false
    }
  }
}
```

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

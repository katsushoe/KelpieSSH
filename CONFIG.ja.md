# Kelpie Configuration

最終更新: 2026-06-14

この文書は、Kelpie の設定ファイルの配置場所、項目名、フォーマット、公開サンプルをまとめる。

## 公開サンプル配置

公開リポジトリには、実運用設定ではなくサンプル設定だけを `config_samples/` に置く。
通常は `kelpie init` で `KelpieHome` 配下の初期ファイルを生成し、`config_samples/` は生成内容の参照や手動復旧時の元サンプルとして使う。`KelpieHome` は設定ファイルへ書かず、`kelpie` / `kelpiemcp` の配置ディレクトリの1つ上に固定する。

```text
config_samples/
├─ kelpie.json
├─ kelpiemcp.json
└─ servers/
   └─ vps01.json
```

| サンプル | 実運用時の配置先 | 説明 |
| :--- | :--- | :--- |
| `config_samples/kelpie.json` | `KelpieHome/config/kelpie.json` | `kelpie` command settings |
| `config_samples/kelpiemcp.json` | `KelpieHome/config/kelpiemcp.json` | `kelpiemcp` command settings |
| `config_samples/servers/vps01.json` | `KelpieHome/profiles/vps01.json` | sample profile for server `vps01` |

実ホスト名、実ユーザー名、秘密鍵名、パスワード参照名、実パス設定を含むファイルはコミットしない。

初期生成:

```powershell
kelpie init
kelpie init vps01
```

## ディレクトリ構成

`KelpieHome` が `D:\Kelpie` の場合、Kelpie は以下の構成を使う。

```text
$KelpieHome/
├─ config/
│  ├─ kelpie.json
│  └─ kelpiemcp.json
├─ bin/
│  ├─ kelpie.exe
│  ├─ kelpiemcp.exe
│  └─ mcp/
│     └─ KelpieMCPServer.exe
├─ profiles/
│  └─ vps01.json
├─ keys/
│  └─ vps01_ed25519
├─ dat/
│  └─ storm_state.dat
└─ logs/
```

Windows の例:

```text
D:\Kelpie
├─ config
├─ bin
├─ profiles
├─ keys
├─ dat
└─ logs
```

## 構成要素の生成方法

| パス | ユーザ作成 | ビルド生成 | プログラム作成 | 生成/配置方法 |
| :--- | :---: | :---: | :---: | :--- |
| `KelpieHome/` | - | - | yes | `kelpie init` が `kelpie` コマンド配置ディレクトリの1つ上を基準ディレクトリとして作成する。 |
| `KelpieHome/config/` | - | - | yes | `kelpie init` が作成する。 |
| `KelpieHome/config/kelpie.json` | - | - | yes | `kelpie init` がログ出力先などのCLI設定を書き込む。 |
| `KelpieHome/config/kelpiemcp.json` | - | - | yes | `kelpie init` がポート、NamedPipe名、MCPサーバー起動先の絶対パスを書き込む。`kelpiemcp` と `KelpieMCPServer` が共通で読む。 |
| `KelpieHome/profiles/` | - | - | yes | `kelpie init` が作成する。 |
| `KelpieHome/profiles/sample.json` | - | - | yes | `kelpie init` が作成するサンプルプロファイル。実環境に合わせて編集するか、参照用として使う。 |
| `KelpieHome/profiles/vps01.json` | - | - | yes | `kelpie init vps01` が作成する名前付きプロファイル。ホスト、ユーザー、認証方式、権限設定を実環境に合わせて編集する。 |
| `KelpieHome/keys/` | - | - | yes | `kelpie init` が作成する。パスワード認証だけを使う場合、秘密鍵ファイルの配置は不要。 |
| `KelpieHome/keys/vps01_ed25519` | yes | - | - | `ssh-keygen` などで作成した秘密鍵を配置する。既存鍵を使う場合は安全な方法でコピーする。 |
| `KelpieHome/bin/kelpie` | - | yes | - | `dotnet publish` で作成した `kelpie` 実行ファイルを配置する。Windowsでは `kelpie.exe`。 |
| `KelpieHome/bin/kelpiemcp` | - | yes | - | `dotnet publish` で作成した `kelpiemcp` 実行ファイルを配置する。Windowsでは `kelpiemcp.exe`。 |
| `KelpieHome/bin/mcp/KelpieMCPServer` | - | yes | - | `dotnet publish` で作成した `KelpieMCPServer` 実行ファイルを配置する。Windowsでは `KelpieMCPServer.exe`。 |
| `KelpieHome/dat/` | - | - | yes | `kelpie init` または `kelpie open` などの実行時状態保存で必要になったときに作成される。 |
| `KelpieHome/dat/storm_state.dat` | - | - | yes | `kelpie open`、`kelpie gui`、`kelpie cli` などの実行時に作成・更新される。通常は手動編集しない。 |
| `KelpieHome/logs/` | - | - | yes | `kelpie init` またはログ出力時に作成される。 |
| `KelpieHome/logs/*.log` | - | - | yes | `kelpie`、`kelpiemcp`、`KelpieMCPServer` の実行時に作成・追記される。 |

## `dat/storm_state.dat`

`kelpie` CLI が現在の操作状態を保存するランタイム状態ファイル。ユーザーが通常編集する設定ファイルではない。

### フォーマット

```json
{
  "OpenProfile": "vps01",
  "ClientMode": "cli"
}
```

### 項目

| 項目 | 説明 |
| :--- | :--- |
| `OpenProfile` | `kelpie open <profile>` で最後に開いたプロファイル名。`kelpie login` が参照する。 |
| `ClientMode` | `kelpie gui` / `kelpie cli` で切り替えたクライアントモード。`gui` または `cli`。 |

## `config/kelpie.json`

`kelpie` CLI が読む設定ファイル。

### フォーマット

```json
{
  "LogDirectory": "D:\\Kelpie\\logs",
  "Server": {
    "Port": 45432,
    "ControlPipeName": "KelpieMCPServer.Control"
  },
  "Commands": {
    "ExecutablePath": "D:\\Kelpie\\bin\\kelpie.exe",
    "WorkingDirectory": "D:\\Kelpie\\bin"
  }
}
```

### 項目

| 項目 | 必須 | 説明 |
| :--- | :--- | :--- |
| `LogDirectory` | no | ログ出力先。相対パスの場合は設定ファイル配置ディレクトリ基準。 |
| `Server.Port` | no | KelpieMCPServer の Streamable HTTP ポート。既定値は `45432`。 |
| `Server.ControlPipeName` | no | NamedPipe 制御名。既定値は `KelpieMCPServer.Control`。 |
| `Commands.ExecutablePath` | no | 外部起動するコマンド実体の明示パス。未指定時は既定解決。 |
| `Commands.WorkingDirectory` | no | 外部起動時の作業ディレクトリ。未指定時は既定解決。 |

最小例:

```json
{
  "LogDirectory": "D:\\Kelpie\\logs"
}
```

## `config/kelpiemcp.json`

`kelpiemcp` と `KelpieMCPServer` が共通で読む設定ファイル。

### フォーマット

```json
{
  "AllowedHosts": "localhost;127.0.0.1;[::1]",
  "LogDirectory": "D:\\Kelpie\\logs",
  "Server": {
    "Port": 45432,
    "ControlPipeName": "KelpieMCPServer.Control"
  },
  "Commands": {
    "ExecutablePath": "D:\\Kelpie\\bin\\mcp\\KelpieMCPServer.exe",
    "WorkingDirectory": "D:\\Kelpie\\bin"
  }
}
```

### 項目

| 項目 | 必須 | 説明 |
| :--- | :--- | :--- |
| `AllowedHosts` | no | HTTP Host 制限。セミコロン区切り。既定はローカルホストのみ。 |
| `LogDirectory` | no | ログ出力先。相対パスの場合は設定ファイル配置ディレクトリ基準。 |
| `Server.Port` | no | MCP Streamable HTTP ポート。既定値は `45432`。 |
| `Server.ControlPipeName` | no | `start` / `stop` / `status` / `password` / `forget` が使う NamedPipe 制御名。 |
| `Commands.ExecutablePath` | no | `kelpiemcp start` が起動するサーバー実体の明示パス。未指定時は既定解決。 |
| `Commands.WorkingDirectory` | no | サーバー起動時の作業ディレクトリ。通常は `KelpieHome/bin`。未指定時は `kelpiemcp` の配置ディレクトリ。 |

未指定時、`kelpiemcp start` は `KelpieHome/bin/mcp/KelpieMCPServer.exe` 相当、つまり `kelpiemcp.exe` の配置ディレクトリ直下の `mcp` ディレクトリを優先して探す。`kelpie` / `kelpiemcp` と `KelpieMCPServer` の DLL バージョン不整合を避けるため、MCPサーバー本体は `bin/mcp` 以下へ分離して発行する。

`/mcp` はMCPクライアント用のStreamable HTTPエンドポイント。ブラウザで起動確認する場合は `http://127.0.0.1:45432/health` を使う。

最小例:

```json
{
  "LogDirectory": "D:\\Kelpie\\logs",
  "Server": {
    "Port": 45432,
    "ControlPipeName": "KelpieMCPServer.Control"
  }
}
```

## ログ出力先の解決順

ログ出力先は以下の順で解決する。

1. 各コマンドが読む設定ファイル直下の `LogDirectory`
2. `KelpieHome/logs`
3. 起動ディレクトリ直下の `logs`
4. 起動ディレクトリ

`LogDirectory` が相対パスの場合は、設定ファイル配置ディレクトリ基準で解決する。

## `profiles/<profile>.json`

SSH接続先ごとに1ファイルを置く。ファイル名がプロファイル名になる。

例:

```text
KelpieHome/profiles/vps01.json
```

この場合、CLIやMCPでは `vps01` をプロファイル名として指定する。

## 秘密鍵認証プロファイル

### フォーマット

```json
{
  "Host": {
    "Address": "203.0.113.10",
    "Port": 22
  },
  "Auth": {
    "UserName": "deploy",
    "Method": "privateKey",
    "PrivateKeyFile": "vps01_ed25519"
  },
  "Connection": {
    "TimeoutSeconds": 10
  },
  "Platform": {
    "OsFamily": "alma"
  },
  "Mode": "Safe",
  "Capabilities": [
    "AllowListPackage"
  ],
  "Rights": {
    "$WebDeploy": "$ReadWrite|@Import",
    "$LogRead": "$ReadOnly"
  },
  "AllowedRoots": {
    "/var/www": "$WebDeploy",
    "/var/log": "$LogRead",
    "/tmp": "$ALL"
  },
  "SpecialPaths": {
    "**/.env": "Deny",
    "**/.ssh/**": "Deny",
    "**/.htaccess": "Confirm",
    "/var/www/.well-known/**": "Allow"
  }
}
```

`Auth` と `Authentication` はどちらも使える。正式名は `Authentication`、手書きしやすい短縮名は `Auth`。両方を書いた場合は `Authentication` を優先する。

### 項目

| 項目 | 必須 | 説明 |
| :--- | :--- | :--- |
| `Host.Address` | yes | SSH接続先ホスト名またはIPアドレス。 |
| `Host.Port` | no | SSHポート。既定値は `22`。 |
| `Auth.UserName` / `Authentication.UserName` | single-user時yes | SSHユーザー名。`Users` を使う場合は `Users` object のキー、または互換配列の `Users[].UserName` に書く。`root` 直接ログインは禁止。 |
| `Auth.Method` / `Authentication.Method` | yes | `privateKey` または `password`。 |
| `Auth.PrivateKeyFile` / `Authentication.PrivateKeyFile` | privateKey時yes | `KelpieHome/keys` 配下の秘密鍵ファイル名。`Users` を使う場合はプロファイル共通認証として扱う。絶対パスも指定可能。 |
| `Auth.PrivateKeyPath` / `Authentication.PrivateKeyPath` | no | 互換用の秘密鍵パス。新規設定では `PrivateKeyFile` を使う。 |
| `Auth.PrivateKeyPassphrase` / `Authentication.PrivateKeyPassphrase` | no | 秘密鍵パスフレーズ。ログに出してはならない。 |
| `Connection.TimeoutSeconds` | no | SSH接続タイムアウト秒数。 |
| `Platform.OsFamily` | yes | 対象OS family またはエイリアス。例: `debian`, `ubuntu`, `rhel`, `alma`。 |
| `Platform.PackageManager` | no | `apt`, `dnf`, `yum` など。未指定時は `OsFamily` から推定する。 |
| `Mode` | no | 互換用のロール式。`ReadOnly`, `Safe`, `Maintenance`, `Expert`, `WebUser`, `WebAdmin` を `|` 区切りで指定する。省略または空の場合は `Safe` ロールを持つ。 |
| `Capabilities` | no | CLI専用の追加許可フラグ。配列または `|` 区切り文字列。MCP経由では無視する。 |
| `Rights` | no | `AllowedRoots` の値として使える名前付き権限フラグセット。 |
| `AllowedRoots` | no | パス操作を許可するルートとアクセス属性。省略または空はパス指定操作不可。 |
| `SpecialPaths` | no | `AllowedRoots` 内で追加扱いするパスパターン。`Deny` / `Confirm` / `Allow` で制御する。 |
| `Services` | no | Webサーバーなど、サービス固有の初期設定値。 |

## 複数ユーザー認証プロファイル

同じVPSで複数のSSHログインユーザーを使い分ける場合は、`Users` を使う。`DefaultUser` を指定すると、CLIやMCPツールでユーザー指定が省略された場合の既定ユーザーとして扱う。

```json
{
  "Host": {
    "Address": "203.0.113.10",
    "Port": 22
  },
  "Auth": {
    "Method": "privateKey",
    "PrivateKeyFile": "vps01_ed25519"
  },
  "DefaultUser": "deploy",
  "Users": {
    "deploy": {
      "Mode": "Safe",
      "AllowedRoots": {
        "/var/www": "@Read|@Write|@List|@CD",
        "/var/log": "@Read|@List"
      }
    },
    "readonly": "ReadOnly"
  },
  "Platform": {
    "OsFamily": "alma"
  }
}
```

`Users` がある場合は `DefaultUser` を既定の接続ユーザーとして扱う。`Auth` / `Authentication` の `Method` / `PrivateKeyFile` / `PrivateKeyPath` / `PrivateKeyPassphrase` / `PasswordSecretName` はプロファイル共通認証として扱う。通常は `Users` にはユーザー名とユーザー別ポリシーだけを書く。既存の `Auth` / `Authentication` は単一ユーザー互換形式としても引き続き使用できる。

`Users` の推奨形式は object で、キーをSSHユーザー名、値をロール式または詳細設定objectにする。従来の配列形式も互換として読み取る。

`Users` の文字列値は `Role|Role` 形式で指定する。`ReadOnly` / `Safe` / `Maintenance` / `Expert` もロールであり、互換用の内部有効モードはロールから導出する。ロール式にポリシーロールが含まれない場合は `Safe` ロールを持つ。

```json
{
  "Users": {
    "alma": "Expert|WebUser",
    "hoge": "Safe|WebAdmin"
  }
}
```

| ロール | 説明 |
| :--- | :--- |
| `ReadOnly` | 読み取り専用。診断、一覧、ログ表示などを中心に許可する。 |
| `Safe` | 標準セーフロール。未指定時の既定ロール。危険な変更操作、秘密情報表示、sudo、削除、移動、インストールは禁止する。 |
| `Maintenance` | VPS保守ロール。パッケージの一覧、更新確認、インストールなどを主に許可する想定。 |
| `Expert` | エキスパートロール。CLI経由では強い権限を許可する想定。ただしMCP経由では秘密情報表示を禁止する。 |
| `WebUser` | `WebPublicSites` の `Root`、`Services.Nginx.Root`、または既定の `/var/www` を Web ルートとして、読み取り・一覧・書き込み・`cd` を許可する。 |
| `WebAdmin` | Nginx 設定変更、設定テスト、Webサーバー関連サービス操作を許可する。MCP経由でも対象コマンドの `sudo` と確認必須操作を通す。 |

詳細設定objectでは `Roles` を文字列または配列で指定できる。

```json
{
  "Users": {
    "deploy": {
      "Mode": "Safe",
      "Roles": [
        "WebUser"
      ]
    }
  }
}
```

## サービス固有設定

`Services` は、Webサーバーなどサービス固有の初期設定値をプロファイルに持たせるための領域。現在は `Nginx` をサポートする。

```json
{
  "Services": {
    "Nginx": {
      "User": "user01",
      "Group": "group01",
      "Port": 8081,
      "Root": "/var/www/myRoot"
    }
  }
}
```

| 項目 | 必須 | 説明 |
| :--- | :--- | :--- |
| `Services.Nginx.User` | no | Nginx worker user の初期設定値。 |
| `Services.Nginx.Group` | no | Nginx worker group の初期設定値。 |
| `Services.Nginx.Port` | no | Nginx listen port の初期設定値。1から65535まで。 |
| `Services.Nginx.Root` | no | Web公開ルート。`WebUser` ロールの許可ルートにも使う。 |

## パスワード認証プロファイル

平文パスワードは設定ファイルに書かない。`PasswordSecretName` だけを書く。

```json
{
  "Host": {
    "Address": "203.0.113.10",
    "Port": 22
  },
  "Auth": {
    "UserName": "deploy",
    "Method": "password",
    "PasswordSecretName": "kelpie:vps01"
  },
  "Connection": {
    "TimeoutSeconds": 10
  },
  "Platform": {
    "OsFamily": "alma"
  },
  "Mode": "Safe"
}
```

パスワードはサーバー起動後に一時登録する。

```powershell
kelpiemcp start
kelpiemcp password vps01
```

削除する場合:

```powershell
kelpiemcp forget vps01
```

## `Platform.OsFamily`

| 設定値 | コマンド処理用OS family | 主な対象OS |
| :--- | :--- | :--- |
| `debian` | `debian` | Debian |
| `ubuntu` | `debian` | Ubuntu |
| `rhel` | `rhel` | Red Hat Enterprise Linux 系 |
| `alma` | `rhel` | AlmaLinux |
| `almalinux` | `rhel` | AlmaLinux |
| `rocky` | `rhel` | Rocky Linux |
| `rockylinux` | `rhel` | Rocky Linux |
| `centos` | `rhel` | CentOS / CentOS Stream |
| `oraclelinux` | `rhel` | Oracle Linux |
| `ol` | `rhel` | Oracle Linux |

`Platform.PackageManager` が未指定の場合、`OsFamily` から既定値を推定する。推定できない場合は設定エラーとする。

## ロールと互換 `Mode`

`ReadOnly` / `Safe` / `Maintenance` / `Expert` はロールとして扱う。プロファイル内の `Mode` キーは後方互換のため残しており、値はロール式として読み取る。新規設定では `Users` の値や `Roles` にロール式を書く。

互換用の内部有効モードはロールから導出する。複数のポリシーロールがある場合は `Expert`、`Maintenance`、`Safe`、`ReadOnly` の順に強いものを採用する。ポリシーロールが無い場合は `Safe` ロールを自動付与する。

## `Capabilities`

`Capabilities` はCLI専用の追加許可フラグ。MCP経由では無視する。

配列形式:

```json
{
  "Capabilities": [
    "AllowAlias",
    "AllowSudo",
    "AllowMoveFiles"
  ]
}
```

文字列形式:

```json
{
  "Capabilities": "AllowAlias|AllowSudo|AllowMoveFiles"
}
```

代表的な値:

- `AllowAlias`
- `AllowSudo`
- `AllowShowPassword`
- `AllowShowPrivateKey`
- `AllowListPackage`
- `AllowUpdatePackageIndex`
- `AllowInstallPackage`
- `AllowRemovePackage`
- `AllowDeleteFiles`
- `AllowMoveFiles`
- `AllowMoveDirectory`

## `AllowedRoots`

`AllowedRoots` は、パス指定が必要な操作を許可するルートとアクセス属性を指定する。推奨形式は object で、キーは gitignore風globのパス、値は `@Read|@List|@Write` のような `|` 区切りの生フラグ、`$` で始まる `Rights` 参照、または `$ALL`。

```json
{
  "Rights": {
    "$WebDeploy": "$ReadWrite|@Import",
    "$LogRead": "$ReadOnly"
  },
  "AllowedRoots": {
    "/var/www": "$WebDeploy",
    "/var/log": "$LogRead",
    "/home/*": "@Read|@List",
    "/opt/apps/**": "@Read|@List"
  }
}
```

`$ReadOnly`、`$ReadWrite`、`$ALL` はシステム定義の `Rights` として常に使える。システム定義は `$ReadOnly = @Read|@List|@CD`、`$ReadWrite = $ReadOnly|@Write`、`$ALL = @Read|@List|@Write|@Import|@Export|@CD`。システム定義はプロファイルの `Rights` で上書きしてはならず、上書きした場合は設定エラー。

`Rights` はプロファイル内だけで有効な名前付き権限セット。ユーザー定義 `Rights` のキーは必ず `$` で始める。`AllowedRoots` の値には、直接 `@Read|@List` のように書くことも、`Rights` で定義した `$WebDeploy` のような名前を書くこともできる。ユーザー定義 `Rights` の値から、システム定義または同じ `Rights` 内の別定義を参照できる。名前は大文字小文字を区別しない。未知の名前、`$` なしの名前付き権限参照、`@` なしの生フラグ、循環参照は設定エラー。

ルール:

- 省略または空配列: パス指定が必要な操作は不可。
- `@Read`: ファイル内容の参照を許可する。
- `@List`: ファイル・ディレクトリ一覧を許可する。
- `@Write`: 書き込み、編集、削除、移動の候補にできる。
- `@Import`: ローカルからリモートへの upload/import を許可する。
- `@Export`: リモートからローカルへの download/export を許可する。
- `@CD`: `cd` / change directory を許可する。
- `$ALL`: `@Read|@List|@Write|@Import|@Export|@CD` と同じ。
- `$ReadOnly`: システム定義。`@Read|@List|@CD` として扱う。
- `$ReadWrite`: システム定義。`$ReadOnly|@Write` として扱う。
- 従来の配列形式: 互換形式として読み取り、各要素は `$ReadOnly` として扱う。
- 通常要素: 正規化後の対象パスが、そのルート自身または配下なら、指定されたアクセス属性の範囲で許可。
- `*`: 単体指定の場合のみ、全パス許可。
- `**`: 単体指定の場合のみ、全パス許可。
- パス内の `*`: その階層の任意の1要素にマッチ。
- パス内の `**`: その階層以下の任意の深さにマッチ。
- 正規表現は使用しない。

Windows SSH の例:

```json
{
  "AllowedRoots": {
    "C:/Users/*": "@Read|@List",
    "D:/apps/**": "$ALL"
  }
}
```

## `SpecialPaths`

`SpecialPaths` は `AllowedRoots` の通常判定に追加で適用するパス別例外ポリシー。プロファイルごとに設定できる。

```json
{
  "SpecialPaths": {
    "**/.env": "Deny",
    "**/.env.*": "Deny",
    "**/.ssh/**": "Deny",
    "**/.aws/**": "Deny",
    "**/.kube/**": "Deny",
    "**/.htaccess": "Confirm",
    "/var/www/.well-known/**": "Allow"
  }
}
```

| 値 | 説明 |
| :--- | :--- |
| `Deny` | 読み取り・書き込み・削除を拒否する。 |
| `Confirm` | 操作候補にするが強い確認を要求する。 |
| `Allow` | `AllowedRoots` と `Mode` / `Capabilities` の通常判定に任せる。 |

## 最小セット

`D:\Kelpie\config\kelpie.json`

```json
{
  "LogDirectory": "D:\\Kelpie\\logs"
}
```

`D:\Kelpie\config\kelpiemcp.json`

```json
{
  "LogDirectory": "D:\\Kelpie\\logs",
  "Server": {
    "Port": 45432,
    "ControlPipeName": "KelpieMCPServer.Control"
  }
}
```

`D:\Kelpie\profiles\vps01.json`

```json
{
  "Host": {
    "Address": "203.0.113.10",
    "Port": 22
  },
  "Auth": {
    "UserName": "deploy",
    "Method": "privateKey",
    "PrivateKeyFile": "vps01_ed25519"
  },
  "Connection": {
    "TimeoutSeconds": 10
  },
  "Platform": {
    "OsFamily": "alma"
  },
  "Mode": "Safe",
  "Capabilities": [
    "AllowListPackage"
  ],
  "AllowedRoots": {
    "/var/www": "@Read|@Write|@List|@CD",
    "/var/log": "@Read|@List"
  },
  "SpecialPaths": {
    "**/.env": "Deny",
    "**/.ssh/**": "Deny",
    "/var/www/.well-known/**": "Allow"
  }
}
```

秘密鍵:

```text
D:\Kelpie\keys\vps01_ed25519
```

コマンド配置:

```text
D:\Kelpie\bin\kelpie.exe
D:\Kelpie\bin\kelpiemcp.exe
D:\Kelpie\bin\mcp\KelpieMCPServer.exe
```

実行例:

```powershell
kelpiemcp start
kelpie profile show vps01
kelpie status vps01
kelpie diag vps01
```

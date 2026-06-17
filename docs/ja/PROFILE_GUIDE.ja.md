# KelpieSSH Profile Guide

最終更新: 2026-06-17

この文書は、KelpieSSH の SSH profile 設定方法を説明します。
英語版は [../../PROFILE_GUIDE.md](../../PROFILE_GUIDE.md) です。

`kelpie.json` や `kelpiemcp.json` などの全体設定は [CONFIG.ja.md](CONFIG.ja.md) を参照してください。

## Profile とは

SSH profile は、保存済み SSH 接続設定です。
Profile は `KelpieHome\profiles` 配下の JSON ファイルとして保存します。

ファイル名が profile 名になります。

```text
D:\Kelpie\profiles\vps01.json
```

この profile は `vps01` として使います。

```powershell
kelpie open vps01
kelpie profile show vps01
kelpie status vps01
```

Terminal CLI commands は command flow ごとに profile file を読み取ります。`KelpieMCPServer` は起動中、profiles を in-memory catalog として保持します。MCP 利用中に profile JSON files を編集した場合は、`profile_reload` MCP tool を呼び出すか、MCP サーバーを再起動してください。

## Profile の作成

名前付き profile を作成します。

```powershell
kelpie init vps01
```

次のファイルを編集します。

```text
<KelpieHome>\profiles\vps01.json
```

接続前に最低限、次を設定します。

- 接続先 host
- SSH user
- 認証方式
- 秘密鍵ファイル名または password secret name
- target platform

秘密鍵認証では、秘密鍵を次の場所に置きます。

```text
<KelpieHome>\keys
```

対応する公開鍵は、事前にサーバー側へ登録されている必要があります。
通常は remote user の `~/.ssh/authorized_keys` に登録します。

## 最小秘密鍵 profile

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
  "Mode": "Safe"
}
```

必要な local key file:

```text
<KelpieHome>\keys\vps01_ed25519
```

## 最小パスワード profile

平文パスワードを profile に保存してはいけません。
Profile には secret reference name だけを書きます。

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

Profile を開いた後、実パスワードを実行中 session に入力します。

```powershell
kelpie open vps01
kelpie login
```

MCP server session に保存する場合:

```powershell
kelpiemcp start
kelpiemcp password vps01
```

削除する場合:

```powershell
kelpiemcp forget vps01
```

## Full Example

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
    "OsFamily": "alma",
    "PackageManager": "dnf"
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
    "/var/log": "$LogRead"
  },
  "SpecialPaths": {
    "**/.env": "Deny",
    "**/.ssh/**": "Deny",
    "/var/www/.well-known/**": "Allow"
  },
  "Services": {
    "Nginx": {
      "Root": "/var/www/example",
      "Port": 80
    }
  }
}
```

## 項目リファレンス

### `Host`

SSH 接続先です。

| Field | Required | Description |
| :--- | :---: | :--- |
| `Host.Address` | yes | Host name または IP address。 |
| `Host.Port` | no | SSH port。既定値は `22`。 |

Troubleshooting:

- `Host.Address` が `example.invalid` のままなら、接続前に実ホストへ変更してください。
- SSH port が標準以外の場合は `Host.Port` を設定してください。

### `Auth` / `Authentication`

SSH 認証設定です。
`Authentication` が正式名、`Auth` は samples で使う短縮名です。
両方ある場合は `Authentication` を優先します。

| Field | Required | Description |
| :--- | :---: | :--- |
| `Auth.UserName` | single-user profile では yes | SSH login user。`root` 直接ログインは禁止です。 |
| `Auth.Method` | yes | `privateKey` または `password`。 |
| `Auth.PrivateKeyFile` | `privateKey` では yes | `KelpieHome\keys` 配下の秘密鍵ファイル名、または absolute path。 |
| `Auth.PrivateKeyPath` | no | 互換用 path。新規 profile では `PrivateKeyFile` を推奨します。 |
| `Auth.PrivateKeyPassphrase` | no | 秘密鍵 passphrase。logs や public files に出してはいけません。 |
| `Auth.PasswordSecretName` | `password` では yes | Secret reference name。実 password は runtime に入力します。 |

Troubleshooting:

- `SSH private key path is required`: `Auth.PrivateKeyFile` を設定するか、`Auth.Method` を `password` に変更してください。
- `SSH password secret name is required`: `Auth.PasswordSecretName` を設定してください。
- 秘密鍵認証に失敗する: private key file が `KelpieHome\keys` にあること、remote public key が登録済みであること、SSH user が正しいことを確認してください。
- パスワード認証に失敗する: `kelpie open <profile>` 後に `kelpie login` するか、MCP server session 用に `kelpiemcp password <profile>` を実行してください。

### `Connection`

接続動作です。

| Field | Required | Description |
| :--- | :---: | :--- |
| `Connection.TimeoutSeconds` | no | SSH connection timeout 秒数。既定値は `10`。 |

Troubleshooting:

- 遅い server で timeout する場合は `TimeoutSeconds` を増やしてください。
- 待ち時間が長すぎる場合は `TimeoutSeconds` を下げてください。

### `Platform`

安全な command 選択に使う target OS metadata です。

| Field | Required | Description |
| :--- | :---: | :--- |
| `Platform.OsFamily` | yes | Target OS family または alias。 |
| `Platform.PackageManager` | no | `apt`, `dnf`, `yum` など。省略時は `OsFamily` から推定できる場合があります。 |

代表的な `OsFamily`:

| Value | Effective family | Typical OS |
| :--- | :--- | :--- |
| `debian` | `debian` | Debian |
| `ubuntu` | `debian` | Ubuntu |
| `rhel` | `rhel` | Red Hat Enterprise Linux |
| `alma` | `rhel` | AlmaLinux |
| `almalinux` | `rhel` | AlmaLinux |
| `rocky` | `rhel` | Rocky Linux |
| `centos` | `rhel` | CentOS / CentOS Stream |
| `oraclelinux` | `rhel` | Oracle Linux |

Troubleshooting:

- package command が拒否される、または合わない場合は `OsFamily` と `PackageManager` を確認してください。
- OS が不明な場合でも、実際の target family と一致しない値は設定しないでください。

### `Mode` and `Roles`

`Mode` は互換用 key で、role expression として読み取ります。

| Role | Description |
| :--- | :--- |
| `ReadOnly` | 読み取り中心の診断と listing。 |
| `Safe` | 既定の safe role。危険な変更、secret 表示、sudo、delete、move、install を禁止します。 |
| `Maintenance` | package や service maintenance 向け role。 |
| `Expert` | CLI で強めの権限を許可する想定。MCP では secret exposure を引き続き禁止します。 |
| `WebUser` | web root の read/list/write/cd を許可します。 |
| `WebAdmin` | selected Nginx and web-server administration commands を許可します。 |

例:

```json
{
  "Mode": "Safe"
}
```

```json
{
  "Mode": "Safe|WebUser"
}
```

Troubleshooting:

- path operation が拒否される場合、`Mode` だけでは不足です。`AllowedRoots` を設定してください。
- CLI-only permission が MCP で効かない場合、それが `Capabilities` か確認してください。

### `Capabilities`

CLI-only override flags です。
MCP 実行では `Capabilities` を無視し、mode-based permissions のみを評価します。

例:

```json
{
  "Capabilities": [
    "AllowListPackage"
  ]
}
```

```json
{
  "Capabilities": "AllowListPackage|AllowInstallPackage"
}
```

代表的な値:

- `AllowAlias`
- `AllowSudo`
- `AllowShowPassword`
- `AllowShowPrivateKey`
- `AllowPeekEnvironmentKeys`
- `AllowPeekEnvironmentValues`
- `AllowSetEnvironmentValues`
- `AllowListPackage`
- `AllowUpdatePackageIndex`
- `AllowInstallPackage`
- `AllowRemovePackage`
- `AllowDeleteFiles`
- `AllowMoveFiles`
- `AllowMoveDirectory`

Troubleshooting:

- Unknown name は configuration error です。
- MCP 権限を `Capabilities` で増やすことはできません。

### `EnvironmentValues`

環境変数名ごとの取り扱い rules です。
`Capabilities` は環境変数操作を呼べるかどうかを制御します。
`EnvironmentValues` は各環境変数名に対して何を許可するかを制御します。

例:

```json
{
  "Capabilities": "AllowPeekEnvironmentKeys|AllowPeekEnvironmentValues|AllowSetEnvironmentValues",
  "EnvironmentValues": {
    "PATH": "Common|NoLog",
    "LANG": "Common|NoLog",
    "APP_ENV": "Common|SetLog",
    "GITHUB_TOKEN": "PeekSecret|PeekLog",
    "DEPLOY_TOKEN": "Masked|PeekLog",
    "MY_SECRET_KEY": "Hidden"
  }
}
```

Capability gates:

| Capability | Description |
| :--- | :--- |
| `AllowPeekEnvironmentKeys` | 環境変数名と metadata の一覧取得を許可します。 |
| `AllowPeekEnvironmentValues` | key rule が許可する場合に、環境変数値の読み取りを許可します。 |
| `AllowSetEnvironmentValues` | key rule が許可する場合に、1回の command execution 用の環境変数値設定、または Kelpie env file への永続化を許可します。 |

`EnvironmentValues` rules:

| Rule | Type | Description |
| :--- | :--- | :--- |
| `Common` | alias | `PeekCommon|SetCommon` に展開します。 |
| `Secret` | alias | `PeekSecret|SetSecret` に展開します。この rule を読み込むと warning を出します。 |
| `Log` | alias | `PeekLog|SetLog` に展開します。`Log` 単体は configuration error です。 |
| `PeekCommon` | permission | common 環境変数値の読み取りを許可します。 |
| `SetCommon` | permission | 1回の command execution 用に common 環境変数値の設定を許可します。 |
| `PeekSecret` | permission | secret 環境変数値の読み取りを許可します。`PeekLog` と組み合わせた場合は warning audit log を出します。 |
| `SetSecret` | permission | 1回の command execution 用に secret 環境変数値の設定を許可します。設定時点でも強めの warning 対象です。 |
| `Hidden` | control | key name、存在、値、設定可否をすべて隠します。他の rule より優先します。 |
| `Masked` | control | key name、存在、値の長さ、masked value だけを表示します。実値は返しません。 |
| `KeyOnly` | control | key name だけを表示します。値の読み取りと設定は許可しません。 |
| `PeekLog` | audit | 値の読み取りまたは masked 表示時に warning audit log を出します。 |
| `SetLog` | audit | 値の設定時に warning audit log を出します。 |
| `NoLog` | audit | 通常 access log を抑制します。warning、denied、configuration-error logs は抑制しません。 |

既定の扱い:

- `EnvironmentValues` に書かれていない key でも、`AllowPeekEnvironmentKeys` がある場合は `get_environment_keys` で key name を表示できます。
- `EnvironmentValues` に書かれていない key の値は読めません。
- `EnvironmentValues` に書かれていない key は設定できません。
- `EnvironmentValues` は value access と set の allowlist であり、key listing 専用の allowlist ではありません。

永続 env file:

- `kelpie env persist` と `persist_environment_value` は remote user の `~/.kelpie/.env` に書き込みます。
- `kelpie env remove` と `remove_persistent_environment_value` は同じ file から key を削除します。
- 書き込み前に `~/.kelpie/.env.20260617T120000Z.kelpie` のような timestamp 付き backup を作成します。
- file は `APP_ENV='production'` のような shell 互換 assignment 形式です。
- `kelpie env set` は `~/.kelpie/.env` が存在する場合、source してから1回限りの override を適用します。
- cron job、shell startup file、service wrapper などは、永続値を使うために `~/.kelpie/.env` を明示的に source する必要があります。
- 既存プロセスには自動反映されません。

Control rule の扱い:

- `Hidden` は環境変数を unavailable に見せます。他の rule と組み合わせないでください。
- `Masked` は値を露出せず、存在と長さだけ確認したい場合に使います。
- `KeyOnly` は key name だけを意図的に見せたい場合に使います。metadata と audit logs で未設定 key と区別できます。

Configuration errors:

- `Log`, `PeekLog`, `SetLog`, `NoLog` の単独指定。
- `Hidden` と他 rule の組み合わせ。
- `KeyOnly` と peek / set permission の組み合わせ。
- `Masked` と実値 peek / set permission の組み合わせ。
- 同じ key に `Common` と `Secret` の両方を指定すること。

Logging rules:

- 環境変数値を logs に出してはいけません。
- `Secret`, `PeekSecret`, `SetSecret` の設定は warning log 対象です。
- `PeekLog`, `SetLog` は対象 operation で warning audit log を出します。
- `NoLog` は通常 access log だけを抑制します。

### `Rights`

`AllowedRoots` で使う named access presets です。

例:

```json
{
  "Rights": {
    "$WebDeploy": "$ReadWrite|@Import",
    "$LogRead": "$ReadOnly"
  }
}
```

Rules:

- User-defined name は `$` で始めます。
- Built-in names は `$ReadOnly`, `$ReadWrite`, `$ALL` です。
- Built-in names は上書きできません。
- Names は case-insensitive です。

### `AllowedRoots`

Path-based operations を許可する path または glob rules です。
省略または空の場合、path-based operations は policy 上許可されません。

例:

```json
{
  "AllowedRoots": {
    "/var/www": "$WebDeploy",
    "/var/log": "$LogRead",
    "/home/*": "@Read|@List",
    "/opt/apps/**": "@Read|@List|@CD"
  }
}
```

Access flags:

| Flag | Description |
| :--- | :--- |
| `@Read` | file content read を許可します。 |
| `@List` | file / directory listing を許可します。 |
| `@Write` | write/edit/delete/move candidate を許可します。 |
| `@Import` | local-to-remote import/upload candidate を許可します。 |
| `@Export` | remote-to-local export/download candidate を許可します。 |
| `@CD` | change-directory operation を許可します。 |

Built-in presets:

| Preset | Meaning |
| :--- | :--- |
| `$ReadOnly` | `@Read|@List|@CD` |
| `$ReadWrite` | `$ReadOnly|@Write` |
| `$ALL` | `@Read|@List|@Write|@Import|@Export|@CD` |

Glob rules:

- `*` は1 path segment に一致します。
- `**` は任意の深さに一致します。
- 単体の `*` または `**` は明示的な global permission です。
- 正規表現は使いません。

Troubleshooting:

- `AllowedRoots` を array で書くと互換 read-only behavior になります。新規 profile では object form を使ってください。
- `Read` や `Write` のような bare value は無効です。`@Read` や `@Write` を使ってください。
- Named preset は `$` で始めてください。

### `SpecialPaths`

`AllowedRoots` 内で追加適用する path rules です。

例:

```json
{
  "SpecialPaths": {
    "**/.env": "Deny",
    "**/.ssh/**": "Deny",
    "**/.htaccess": "Confirm",
    "/var/www/.well-known/**": "Allow"
  }
}
```

| Value | Description |
| :--- | :--- |
| `Deny` | read/write/delete を拒否します。 |
| `Confirm` | operation candidate にはできますが、強い確認を要求します。 |
| `Allow` | 通常の `AllowedRoots`, `Mode`, `Capabilities` evaluation に任せます。 |

Troubleshooting:

- allowed root 配下でも `.env` や `.ssh` が拒否される場合は `SpecialPaths` を確認してください。
- `Allow` は意図した例外だけに使ってください。

### `Users` and `DefaultUser`

同じ server profile で複数 SSH login users を使う場合は `Users` を使います。
`DefaultUser` は、command が user を指定しない場合に使います。

例:

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
      "Mode": "Safe|WebUser",
      "AllowedRoots": {
        "/var/www": "@Read|@Write|@List|@CD"
      }
    },
    "readonly": "ReadOnly"
  },
  "Platform": {
    "OsFamily": "alma"
  }
}
```

Notes:

- 推奨 `Users` format は object です。
- Object keys は SSH user names です。
- String values は role expressions です。
- Detailed object values では mode, roles, auth, allowed roots, special paths を user 別に上書きできます。
- Shared `Auth` values は user が上書きしない限り継承されます。

Troubleshooting:

- 複数 users があり default user がない場合は、user を明示するか `DefaultUser` を設定してください。
- Duplicate user names は configuration error です。

### `Services`

Service-specific defaults です。
現在は `Nginx` settings をサポートします。

例:

```json
{
  "Services": {
    "Nginx": {
      "User": "nginx",
      "Group": "nginx",
      "Port": 80,
      "Root": "/var/www/example"
    }
  }
}
```

| Field | Required | Description |
| :--- | :---: | :--- |
| `Services.Nginx.User` | no | Nginx worker user。 |
| `Services.Nginx.Group` | no | Nginx worker group。 |
| `Services.Nginx.Port` | no | Nginx listen port。1 から 65535。 |
| `Services.Nginx.Root` | no | Web public root。`WebUser` role でも使います。 |

## Validation Checklist

接続前に確認してください。

- `Host.Address` が `example.invalid` のままではない。
- `Auth.UserName` または `Users` に正しい SSH user がある。
- `root` 直接ログインを使っていない。
- `Auth.Method` が `privateKey` または `password`。
- 秘密鍵 profile では実 key が `KelpieHome\keys` にある。
- Password profile では plain text password ではなく `Auth.PasswordSecretName` を使っている。
- `Platform.OsFamily` が target OS に合っている。
- `AllowedRoots` が意図した path だけを含んでいる。
- sensitive paths は `SpecialPaths` で deny している。

## Troubleshooting

### `SSH profile name is required`

Profile file name を解決できません。
`KelpieHome\profiles` 配下に `vps01.json` のような file name で置いてください。

### `SSH host is required`

`Host.Address` を設定してください。
空にしないでください。

### `SSH user name is required`

`Auth.UserName` を設定するか、`Users` と `DefaultUser` を設定してください。

### `SSH private key path is required`

`privateKey` authentication では `Auth.PrivateKeyFile` が必要です。
相対 key file は `KelpieHome\keys` 配下に置きます。

### `SSH password secret name is required`

`password` authentication では `Auth.PasswordSecretName` が必要です。
実 password は、CLI の対話 session では `kelpie login` で入力し、MCP server session では `kelpiemcp password <profile>` で入力します。

### `SSH package manager is required`

Known family の `Platform.OsFamily` を設定するか、`Platform.PackageManager` を明示してください。

### Path operation is denied

次を確認してください。

- `AllowedRoots` があり、object form で書かれている。
- target path が allowed root 配下にある。
- access flags が requested operation を含んでいる。
- `SpecialPaths` がその path を deny していない。

### CLI では許可される command が MCP では許可されない

MCP は `Capabilities` を無視します。
`Mode`、roles、`AllowedRoots`、supported MCP tools を使ってください。

### CLI では password login できるが MCP では失敗する

CLI と MCP の password session は別です。
MCP では server を起動してから password を登録します。

```powershell
kelpiemcp start
kelpiemcp password vps01
```

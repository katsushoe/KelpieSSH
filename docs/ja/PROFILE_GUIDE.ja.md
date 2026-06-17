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
実 password は `kelpie login` または `kelpiemcp password <profile>` で入力します。

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

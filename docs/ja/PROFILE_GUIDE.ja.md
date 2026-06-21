# KelpieSSH Profile Guide

最終更新: 2026-06-18

この文書は、KelpieSSH の SSH profile 設定方法を説明します。
英語版は [../../PROFILE_GUIDE.md](../../PROFILE_GUIDE.md) です。

`kelpie.json` や `kelpiemcp.json` などの全体設定は [CONFIG.ja.md](CONFIG.ja.md) を参照してください。

## Profile の安全責任

Profile は、KelpieSSH がどのサーバーへ接続し、どの remote path、user、policy、service、Web root を操作できるかを定義します。Profile の編集は security-sensitive な変更として扱ってください。

Profile は、自分が所有している、または管理権限を持つシステムに対してのみ使用してください。Profile を trust または reload する前に、接続先 host、SSH user、認証設定、mode、allowed roots、special path rules、web public sites、writable executable extensions を確認してください。

本番環境で profile を使う前に、同じ profile 構成を安全な環境で検証し、重要なサーバーとデータには復元可能な backup を用意してください。KelpieSSH の policy check はリスクを下げますが、運用者による確認、backup、復旧計画の代替にはなりません。

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

Terminal CLI commands は command flow ごとに profile file を読み取ります。`KelpieMCPServer` は起動中、profiles を in-memory catalog として保持します。MCP 利用中に profile JSON files を編集した場合は、利用者が `kelpiemcp profile reload <profile>` を実行して trust store と in-memory profile catalog を更新してください。`profile_reload` MCP tool は trust store の profile hash を更新しないため、正規の profile 編集を受け入れる操作には使いません。

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
  "WebPublicSites": {
    "default": {
      "Root": "/var/www/example",
      "WritableExecutableExtensions": [".php"]
    }
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

### プロファイルスキーマ概要

| 項目 | 必須 | 型 | 既定値 | 値 / 制約 |
| :--- | :---: | :--- | :--- | :--- |
| `Host` | yes | object | none | SSH 接続先設定。 |
| `Host.Address` | yes | string | none | Host name または IP address。空不可。 |
| `Host.Port` | no | integer | `22` | SSH port。通常は `1` から `65535`。 |
| `Auth` | `Authentication` がない場合 yes | object | none | `Authentication` の短縮別名。サンプルではこちらを使います。 |
| `Authentication` | `Auth` がない場合 yes | object | none | 正式な認証設定。`Auth` と両方ある場合はこちらを優先します。 |
| `Auth.UserName` / `Authentication.UserName` | single-user profile では yes | string | none | SSH login user。`root` 直接ログインは禁止です。 |
| `Auth.UsrName` / `Authentication.UsrName` | no | string | none | `UserName` の互換 typo alias。新規設定では `UserName` を使います。 |
| `Auth.Method` / `Authentication.Method` | yes | string enum | `privateKey` | `privateKey`: 秘密鍵認証。`password`: `PasswordSecretName` と runtime password session を使う認証。 |
| `Auth.PrivateKeyFile` / `Authentication.PrivateKeyFile` | `privateKey` では yes | string | none | `KelpieHome\keys` 配下のファイル名、または absolute path。 |
| `Auth.PrivateKeyPath` / `Authentication.PrivateKeyPath` | no | string | none | 互換用 path。新規設定では `PrivateKeyFile` を推奨します。 |
| `Auth.PrivateKeyPassphrase` / `Authentication.PrivateKeyPassphrase` | no | string or null | `null` | 秘密鍵 passphrase。公開サンプルに実値を書いてはいけません。 |
| `Auth.PasswordSecretName` / `Authentication.PasswordSecretName` | `password` では yes | string or null | `null` | Secret reference name。profile に平文 password は保存しません。 |
| `Connection` | no | object | `{ "TimeoutSeconds": 10 }` | SSH 接続動作。 |
| `Connection.TimeoutSeconds` | no | integer | `10` | 正の整数。 |
| `Platform` | yes | object | none | provider 選択に使う target OS metadata。 |
| `Platform.OsFamily` | yes | string enum/alias | none | `debian`, `ubuntu`, `rhel`, `alma`, `almalinux`, `rocky`, `centos`, `oraclelinux`。alias は effective family に解決されます。 |
| `Platform.PackageManager` | no | string | `OsFamily` から推定 | effective `debian` は `apt`、effective `rhel` は `dnf`。必要なら明示指定できます。 |
| `Mode` | no | string role expression | `Safe` | `ReadOnly`, `Safe`, `Maintenance`, `Expert`, `WebUser`, `WebAdmin`。`|` で組み合わせ可能。互換 key として role expression として読み取ります。 |
| `Roles` | no | string or string array | `Mode` から解決 | `Mode` と同じ role 名。設定時は role 解決に使います。 |
| `Capabilities` | no | string, string array, or object | empty | CLI 専用 policy flags。MCP では無視します。詳細は [`Capabilities`](#capabilities)。 |
| `Rights` | no | dictionary object | built-ins only | `$` 始まりの名前を key にし、値は preset または `@` flags の access expression。 |
| `AllowedRoots` | no | dictionary object or string array | empty | Object form は path/glob から access expression への map。Array form は互換 read-only/list/cd。 |
| `SpecialPaths` | no | dictionary object | empty | Key は path glob。値は `Deny`, `Confirm`, `Allow`。 |
| `EnvironmentValues` | no | dictionary object | empty | Key は environment variable name。値は environment access expression。 |
| `DefaultUser` | no | string | `Auth.UserName` | `Users` が複数あり、command 側で user 未指定の場合に選ばれる user。 |
| `Users` | no | dictionary object or array | single legacy user | 推奨 object form は SSH user name から role expression または詳細 user object への map。 |
| `Users.<user>` | no | string or object | profile settings を継承 | String value は role expression。Object value は auth, roles, roots, special paths, environment values, web public sites を上書きできます。 |
| `Users.<user>.Method` | no | string enum | profile auth method | `privateKey` または `password`。 |
| `Users.<user>.PrivateKeyFile` | no | string | profile auth value | User-level private key file override。 |
| `Users.<user>.PrivateKeyPath` | no | string | profile auth value | 互換用 user-level private key path。新規設定では `PrivateKeyFile` を推奨します。 |
| `Users.<user>.PrivateKeyPassphrase` | no | string or null | profile auth value | User-level private key passphrase override。 |
| `Users.<user>.PasswordSecretName` | no | string or null | profile auth value | User-level password secret reference override。 |
| `Users.<user>.Mode` | no | string role expression | profile roles | Profile `Mode` と同じ値。 |
| `Users.<user>.Roles` | no | string or string array | profile roles | Profile `Roles` と同じ値。 |
| `Users.<user>.Capabilities` | no | string, string array, or object | profile capabilities | CLI 専用 user-level policy flags。 |
| `Users.<user>.AllowedRoots` | no | dictionary object or string array | profile allowed roots | Profile `AllowedRoots` と同じ形式。 |
| `Users.<user>.SpecialPaths` | no | dictionary object | profile special paths | Profile `SpecialPaths` と同じ形式。 |
| `Users.<user>.EnvironmentValues` | no | dictionary object | profile environment rules | Profile `EnvironmentValues` と同じ形式。 |
| `Users.<user>.WebPublicSites` | no | dictionary object or array | profile web public sites | Profile `WebPublicSites` と同じ形式。 |
| `Services` | no | object | empty object | Service-specific defaults。 |
| `Services.Nginx` | no | object | empty object | Nginx と web helpers が使う Nginx defaults。 |
| `Services.Nginx.User` | no | string | none | Nginx worker user。 |
| `Services.Nginx.Group` | no | string | none | Nginx worker group。 |
| `Services.Nginx.Port` | no | integer | none | 設定時は `1` から `65535`。 |
| `Services.Nginx.Root` | no | string | none | Web public root。`WebPublicSites` 未設定時に `WebUser` role でも使います。 |
| `WebPublicSites` | no | dictionary object or array | provider default site | Provider default site は `/var/www/html` の `default` site。安全な静的拡張子を既定許可します。 |
| `WebPublicSites.<siteKey>.SiteKey` | object form では no | string | dictionary key | Array item では必須。空不可。 |
| `WebPublicSites.<siteKey>.DisplayName` | no | string | `siteKey` | 表示用 site label。 |
| `WebPublicSites.<siteKey>.Root` / `RootPath` | yes | string | none | 安全な absolute Unix web root path。`RootPath` は別名で、サンプルでは `Root` を推奨します。 |
| `WebPublicSites.<siteKey>.AllowedExtensions` | no | string array | built-in safe static extensions | 有効な値は `.html` や `.png` のような、先頭ドット付きの単一ファイル拡張子です。大文字小文字は区別しません。通常の Web 公開ファイル向けだけに使い、path、glob、MIME type、実行可能拡張子は指定しません。 |
| `WebPublicSites.<siteKey>.WritableExecutableExtensions` | no | string array | empty | `.php` のような先頭ドット付き実行可能拡張子。ワイルドカードと path separator は拒否されます。 |
| `WebPublicSites.<siteKey>.AllowedContentTypes` | no | string array or dictionary object | built-in safe content types | Array は read/write を許可。Object は MIME type から access expression への map。 |
| `WebPublicSites.<siteKey>.AllowedFiles` | no | dictionary object | empty | Key は file glob、`file:<glob>`、または `mime:<content-type>`。値は access expression。 |
| `WebPublicSites.<siteKey>.CreateDirectories` | no | boolean | `true` | Web write operation で missing parent directories の作成を許可します。 |
| `WebPublicSites.<siteKey>.MaxReadBytes` | no | integer | `5242880` | Web file read operation の最大読み取り bytes。 |
| `WebPublicSites.<siteKey>.MaxWriteBytes` | no | integer | `5242880` | Web file write operation の最大受け入れ bytes。 |
| `Ssh` | no | object | empty object | Legacy endpoint/auth section。新規設定では `Host` と `Auth` / `Authentication` を使います。 |
| `Ssh.Host` | no | string | none | Legacy host address。`Host.Address` が未設定の場合だけ使います。 |
| `Ssh.Port` | no | integer | `22` | Legacy SSH port。`Host.Address` が未設定の場合だけ使います。 |
| `Ssh.UserName` | no | string | none | Legacy SSH user。Auth user name が未設定の場合だけ使います。 |
| `Ssh.Authentication` | no | object | empty object | Legacy authentication section。優先順位は最も低いです。 |
| `Policy` | no | object | empty object | Legacy CLI policy section。新規設定では `Capabilities` と `AllowedRoots` を使います。 |
| `Policy.Level` | no | string | empty | Legacy capability expression。 |
| `Policy.AllowedRoots` | no | string array | empty | Legacy read-only/list/cd allowed roots。 |

互換性と優先順位:

- `Authentication` は `Auth` より優先し、`Auth` は legacy `Ssh.Authentication` より優先します。
- `Host.Address` / `Host.Port` は legacy `Ssh.Host` / `Ssh.Port` より優先します。
- `Auth.UserName` は `Auth.UsrName` より優先し、どちらも legacy `Ssh.UserName` より優先します。
- `Users.<user>` の user-level settings は、選択された user について profile-level settings を上書きします。
- `Root` と `RootPath` は別名です。サンプルでは `Root` を推奨します。

### 設定値サンプル

このセクションでは、profile 設定で使う値の形ごとに、最低1つの有効なサンプルを示します。
サンプルでは、文書用の host、user、鍵名、secret reference だけを使います。

Scalar と object のサンプル:

```json
{
  "Host": {
    "Address": "203.0.113.10",
    "Port": 22
  },
  "Auth": {
    "UserName": "deploy",
    "Method": "privateKey",
    "PrivateKeyFile": "vps01_ed25519",
    "PrivateKeyPassphrase": "sample-passphrase"
  },
  "Connection": {
    "TimeoutSeconds": 10
  },
  "Platform": {
    "OsFamily": "ubuntu",
    "PackageManager": "apt"
  },
  "Services": {
    "Nginx": {
      "User": "www-data",
      "Group": "www-data",
      "Port": 80,
      "Root": "/var/www/html"
    }
  }
}
```

null を許容する秘密情報参照系のサンプル:

```json
{
  "Auth": {
    "PrivateKeyPassphrase": null,
    "PasswordSecretName": "kelpie:vps01"
  }
}
```

Role expression のサンプル:

```json
{
  "Mode": "Maintenance|WebUser",
  "Roles": "Maintenance|WebUser"
}
```

```json
{
  "Roles": ["Maintenance", "WebUser"]
}
```

Capability のサンプル:

```json
{
  "Capabilities": "AllowListPackage|AllowInstallPackage"
}
```

```json
{
  "Capabilities": ["AllowListPackage", "AllowInstallPackage"]
}
```

```json
{
  "Capabilities": {
    "Flags": ["AllowListPackage", "AllowInstallPackage"]
  }
}
```

Dictionary と array のサンプル:

```json
{
  "Rights": {
    "$WebDeploy": "$ReadWrite|@Import"
  },
  "AllowedRoots": {
    "/var/www": "$WebDeploy",
    "/var/log": "$ReadOnly"
  },
  "SpecialPaths": {
    "**/.env": "Deny",
    "/var/www/.well-known/**": "Allow"
  },
  "EnvironmentValues": {
    "PATH": "Read",
    "APP_ENV": "Read|Write"
  }
}
```

```json
{
  "AllowedRoots": ["/var/log", "/etc/nginx"]
}
```

User のサンプル:

```json
{
  "DefaultUser": "deploy",
  "Users": {
    "deploy": "Maintenance|WebUser",
    "readonly": {
      "Mode": "ReadOnly",
      "AllowedRoots": {
        "/var/log": "$ReadOnly"
      }
    }
  }
}
```

```json
{
  "Users": [
    {
      "UserName": "deploy",
      "Mode": "Safe"
    }
  ]
}
```

Web public site のサンプル:

```json
{
  "WebPublicSites": {
    "default": "/var/www/html"
  }
}
```

```json
{
  "WebPublicSites": {
    "default": {
      "Root": "/var/www/html",
      "AllowedExtensions": [".html", ".css", ".js"],
      "WritableExecutableExtensions": [".php"],
      "AllowedContentTypes": ["text/html", "text/css"],
      "AllowedFiles": {
        "file:assets/**": "$ReadWrite",
        "mime:image/png": "$ReadOnly"
      },
      "CreateDirectories": true,
      "MaxReadBytes": 1048576,
      "MaxWriteBytes": 1048576
    }
  }
}
```

```json
{
  "WebPublicSites": [
    {
      "SiteKey": "default",
      "DisplayName": "Default site",
      "Root": "/var/www/html"
    }
  ]
}
```

`AllowedContentTypes` object form:

```json
{
  "WebPublicSites": {
    "default": {
      "Root": "/var/www/html",
      "AllowedContentTypes": {
        "text/html": "$ReadWrite",
        "image/png": "$ReadOnly"
      }
    }
  }
}
```

Legacy compatibility のサンプル:

```json
{
  "Ssh": {
    "Host": "203.0.113.10",
    "Port": 22,
    "UserName": "deploy",
    "Authentication": {
      "Method": "privateKey",
      "PrivateKeyFile": "vps01_ed25519"
    }
  },
  "Policy": {
    "Level": "AllowListPackage",
    "AllowedRoots": ["/var/log"]
  }
}
```

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

### `WebPublicSites`

`WebPublicSites` は、MCP の Web ファイル操作が触ってよい Web 公開ルートを定義します。
`.php`、`.cgi`、`.py`、`.sh`、`.exe` などの実行可能な拡張子は、既定では書き込み拒否です。

そのサイトで実行可能な Web ファイルの配置を人間が明示的に許可する場合だけ、`WritableExecutableExtensions` を設定します。

所属要素:

- [`WebPublicSites.<siteKey>`](#webpublicsitessitekey)
- [`WebPublicSites.<siteKey>.SiteKey`](#webpublicsitessitekeysitekey)
- [`WebPublicSites.<siteKey>.DisplayName`](#webpublicsitessitekeydisplayname)
- [`WebPublicSites.<siteKey>.Root` / `RootPath`](#webpublicsitessitekeyroot--rootpath)
- [`WebPublicSites.<siteKey>.AllowedExtensions`](#webpublicsitessitekeyallowedextensions)
- [`WebPublicSites.<siteKey>.WritableExecutableExtensions`](#webpublicsitessitekeywritableexecutableextensions)
- [`WebPublicSites.<siteKey>.AllowedContentTypes`](#webpublicsitessitekeyallowedcontenttypes)
- [`WebPublicSites.<siteKey>.AllowedFiles`](#webpublicsitessitekeyallowedfiles)
- [`WebPublicSites.<siteKey>.CreateDirectories`](#webpublicsitessitekeycreatedirectories)
- [`WebPublicSites.<siteKey>.MaxReadBytes`](#webpublicsitessitekeymaxreadbytes)
- [`WebPublicSites.<siteKey>.MaxWriteBytes`](#webpublicsitessitekeymaxwritebytes)

例:

```json
{
  "Users": {
    "deploy": {
      "Mode": "Maintenance|WebUser|WebAdmin",
      "WebPublicSites": {
        "default": {
          "Root": "/var/www/html",
          "AllowedExtensions": [".html", ".css", ".js", ".png", ".jpg", ".txt"],
          "WritableExecutableExtensions": [".php"]
        }
      }
    }
  }
}
```

| Field | Required | Description |
| :--- | :---: | :--- |
| `WebPublicSites.<siteKey>.Root` / `RootPath` | yes | そのサイトの Web 公開ルート。 |
| `WebPublicSites.<siteKey>.AllowedExtensions` | no | このサイトで許可する通常ファイルの拡張子。有効な値は `.html` や `.png` のような、先頭ドット付きの単一ファイル拡張子です。大文字小文字は区別しません。path、glob、MIME type、実行可能拡張子は指定しません。未設定または空の場合は、Kelpie 組み込みの安全な静的ファイル拡張子リストを使います。 |
| `WebPublicSites.<siteKey>.WritableExecutableExtensions` | no | このサイトだけで書き込みを許可する実行可能拡張子。`.php` のように先頭ドット付きで列挙します。ワイルドカードは拒否されます。 |
| `WebPublicSites.<siteKey>.AllowedContentTypes` | no | このサイトで許可する MIME type。Array form は read/write を許可し、object form は MIME type から access expression へ対応付けます。 |
| `WebPublicSites.<siteKey>.AllowedFiles` | no | ファイル単位の許可 rules。Key は file glob、`file:<glob>`、または `mime:<content-type>`。値は access expression。 |
| `WebPublicSites.<siteKey>.CreateDirectories` | no | 書き込み時に missing parent directories を作成できるかを制御します。 |
| `WebPublicSites.<siteKey>.MaxReadBytes` | no | Web file read operation で返せる最大 bytes。 |
| `WebPublicSites.<siteKey>.MaxWriteBytes` | no | Web file write operation で受け入れる最大 bytes。 |

#### `WebPublicSites.<siteKey>`

説明:

`WebPublicSites` 配下の site entry です。

型:

- dictionary object value
- array item object
- root path を表す互換 string value

既定値と省略時の挙動:

`WebPublicSites` を省略した場合、利用可能であれば provider default site を使います。組み込みの Web public default は site key `default`、root `/var/www/html` です。

取りうる値の範囲と制約:

- Object form の key が site key になります。
- Array form の item では `SiteKey` が必須です。
- String form の値は root path として扱います。互換目的以外では object form を推奨します。

サンプル:

```json
{
  "WebPublicSites": {
    "default": {
      "Root": "/var/www/html"
    }
  }
}
```

#### `WebPublicSites.<siteKey>.SiteKey`

説明:

`WebPublicSites` を array で書く場合の site 識別子です。

型:

- string

既定値と省略時の挙動:

Object form では dictionary key を site key として使います。Array form では `SiteKey` が必須です。

取りうる値の範囲と制約:

- 空文字は不可です。
- `default`、`public`、`admin` のような安定した識別子を使います。
- path separator や secret を含めないでください。

サンプル:

```json
{
  "WebPublicSites": [
    {
      "SiteKey": "default",
      "Root": "/var/www/html"
    }
  ]
}
```

#### `WebPublicSites.<siteKey>.DisplayName`

説明:

Site の表示名です。

型:

- string

既定値と省略時の挙動:

省略時は site key を表示名として使います。

取りうる値の範囲と制約:

- 秘密情報を含まない表示用文字列です。
- 公開すべきでない実ホスト名、認証情報、secret は含めないでください。

サンプル:

```json
{
  "WebPublicSites": {
    "default": {
      "DisplayName": "Default site",
      "Root": "/var/www/html"
    }
  }
}
```

#### `WebPublicSites.<siteKey>.Root` / `RootPath`

説明:

その site の Web 公開ルートを示す absolute Unix path です。`RootPath` は互換 alias です。新規 profile では `Root` を使います。

型:

- string

既定値と省略時の挙動:

明示 site entry では必須です。Provider default site は `/var/www/html` を使います。

取りうる値の範囲と制約:

- 安全な absolute Unix path である必要があります。
- path traversal segment は使えません。
- MCP Web file operations に許可する Web root を指定します。

サンプル:

```json
{
  "WebPublicSites": {
    "default": {
      "Root": "/var/www/html"
    }
  }
}
```

#### `WebPublicSites.<siteKey>.AllowedExtensions`

説明:

その site で許可する通常ファイルの拡張子です。

型:

- string array

既定値と省略時の挙動:

未設定または空の場合は、Kelpie 組み込みの安全な静的ファイル拡張子リストを使います。

取りうる値の範囲と制約:

- `.html` や `.png` のような、先頭ドット付きの単一ファイル拡張子を指定します。
- 大文字小文字は区別しません。
- path、glob、MIME type、実行可能拡張子は指定しません。
- HTML、CSS、JavaScript、画像、テキスト、JSON、XML、アーカイブなど通常の Web 公開ファイル向けです。
- 組み込みの安全な静的ファイル拡張子は `.html`, `.htm`, `.css`, `.js`, `.mjs`, `.txt`, `.json`, `.xml`, `.png`, `.jpg`, `.jpeg`, `.webp`, `.gif`, `.svg`, `.ico`, `.zip`, `.gz`, `.tgz`, `.tar`, `.bz2`, `.xz`, `.br` です。

サンプル:

```json
{
  "WebPublicSites": {
    "default": {
      "Root": "/var/www/html",
      "AllowedExtensions": [".html", ".css", ".js", ".png", ".jpg", ".txt"]
    }
  }
}
```

#### `WebPublicSites.<siteKey>.WritableExecutableExtensions`

説明:

この site だけで書き込みを許可する実行可能 Web ファイル拡張子です。

型:

- string array

既定値と省略時の挙動:

未設定または空の場合、実行可能ファイルの書き込みは従来どおり拒否されます。

取りうる値の範囲と制約:

- `.php` のように先頭ドット付きの明示的な拡張子を指定します。
- wildcard と path separator は拒否されます。
- 実行可能またはバイナリコードとして既定拒否される拡張子は `.php`, `.cgi`, `.pl`, `.py`, `.rb`, `.sh`, `.bash`, `.exe`, `.dll`, `.so`, `.jar`, `.war` です。
- ここに列挙した拡張子は、書き込み時に限り `AllowedExtensions` へ重複して書かなくても許可対象になります。
- この設定は書き込み判定だけに効きます。読み取り判定、パストラバーサル拒否、ドットファイル拒否、秘密ファイル拒否、サイズ上限、MIME type 判定は従来どおり適用されます。
- 許可は site 単位です。ほかの site やほかの profile では、同じ拡張子でも明示許可がなければ拒否されます。

サンプル:

```json
{
  "WebPublicSites": {
    "default": {
      "Root": "/var/www/html",
      "WritableExecutableExtensions": [".php"]
    }
  }
}
```

#### `WebPublicSites.<siteKey>.AllowedContentTypes`

説明:

その site で許可する MIME type です。

型:

- string array
- dictionary object

既定値と省略時の挙動:

未設定または空の場合は、Kelpie 組み込みの安全な content type rules を使います。

取りうる値の範囲と制約:

- Array form は各 MIME type に read/write を許可します。
- Object form は MIME type key から access expression へ対応付けます。
- MIME type key は `text/html` や `image/png` のような明示的な content type です。
- file extension、path、glob は MIME type key として使いません。

Array form のサンプル:

```json
{
  "WebPublicSites": {
    "default": {
      "Root": "/var/www/html",
      "AllowedContentTypes": ["text/html", "text/css"]
    }
  }
}
```

Object form のサンプル:

```json
{
  "WebPublicSites": {
    "default": {
      "Root": "/var/www/html",
      "AllowedContentTypes": {
        "text/html": "$ReadWrite",
        "image/png": "$ReadOnly"
      }
    }
  }
}
```

#### `WebPublicSites.<siteKey>.AllowedFiles`

説明:

その site に対するファイル単位の許可 rules です。

型:

- dictionary object

既定値と省略時の挙動:

未設定または空の場合、ファイル単位の allowlist は適用せず、extension / content-type rules で判定します。

取りうる値の範囲と制約:

- Key は file glob、`file:<glob>`、または `mime:<content-type>` です。
- 値は `$ReadOnly`、`$ReadWrite`、`@Read|@List` などの access expression です。
- File glob rules は site root からの相対評価です。
- 拡張子 rules より細かく制御したい場合に使います。

サンプル:

```json
{
  "WebPublicSites": {
    "default": {
      "Root": "/var/www/html",
      "AllowedFiles": {
        "file:assets/**": "$ReadWrite",
        "mime:image/png": "$ReadOnly"
      }
    }
  }
}
```

#### `WebPublicSites.<siteKey>.CreateDirectories`

説明:

書き込み操作で missing parent directories を作成できるかを制御します。

型:

- boolean

既定値と省略時の挙動:

省略時は `true` です。

取りうる値の範囲と制約:

- `true`: Web write operations が missing parent directories を作成できます。
- `false`: parent directories が事前に存在している必要があります。

サンプル:

```json
{
  "WebPublicSites": {
    "default": {
      "Root": "/var/www/html",
      "CreateDirectories": true
    }
  }
}
```

#### `WebPublicSites.<siteKey>.MaxReadBytes`

説明:

この site の Web file read operation で返せる最大 bytes です。

型:

- integer

既定値と省略時の挙動:

省略時は `5242880` です。

取りうる値の範囲と制約:

- 正の整数である必要があります。
- 読み取り上限を厳しくしたい場合は小さい値を設定します。

サンプル:

```json
{
  "WebPublicSites": {
    "default": {
      "Root": "/var/www/html",
      "MaxReadBytes": 1048576
    }
  }
}
```

#### `WebPublicSites.<siteKey>.MaxWriteBytes`

説明:

この site の Web file write operation で受け入れる最大 bytes です。

型:

- integer

既定値と省略時の挙動:

省略時は `5242880` です。

取りうる値の範囲と制約:

- 正の整数である必要があります。
- この値を超える content は書き込み前に拒否されます。

サンプル:

```json
{
  "WebPublicSites": {
    "default": {
      "Root": "/var/www/html",
      "MaxWriteBytes": 1048576
    }
  }
}
```

注意:

- `AllowedExtensions` は HTML、CSS、JavaScript、画像、テキスト、JSON、XML、アーカイブなど通常の Web 公開ファイル向けです。`.php` のような実行可能な Web 拡張子は許可しません。
- 組み込みの安全な静的ファイル拡張子は `.html`, `.htm`, `.css`, `.js`, `.mjs`, `.txt`, `.json`, `.xml`, `.png`, `.jpg`, `.jpeg`, `.webp`, `.gif`, `.svg`, `.ico`, `.zip`, `.gz`, `.tgz`, `.tar`, `.bz2`, `.xz`, `.br` です。
- 実行可能またはバイナリコードとして拒否される拡張子は `.php`, `.cgi`, `.pl`, `.py`, `.rb`, `.sh`, `.bash`, `.exe`, `.dll`, `.so`, `.jar`, `.war` です。明示的に許可する実行可能 Web 拡張子だけを `WritableExecutableExtensions` に指定します。
- `WritableExecutableExtensions` が未設定または空の場合、実行可能ファイルの書き込みは従来どおり拒否されます。
- `WritableExecutableExtensions` に列挙した拡張子は、書き込み時に限り `AllowedExtensions` へ重複して書かなくても許可対象になります。
- この設定は書き込み判定だけに効きます。読み取り判定、パストラバーサル拒否、ドットファイル拒否、秘密ファイル拒否、サイズ上限、MIME type 判定は従来どおり適用されます。
- 許可はサイト単位です。ほかのサイトやほかのプロファイルでは、同じ拡張子でも明示許可がなければ拒否されます。

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
- `WebPublicSites` と `WritableExecutableExtensions` は、KelpieSSH に管理させる意図がある Web root と実行可能 file type だけを指定している。
- 対象システムは自分が所有している、または明示的な管理権限を持っている。
- write、package、service、permission、configuration 操作を使う前に、重要なサーバーとデータの復元可能な backup がある。
- 本番環境へ反映する前に、安全な環境で変更を検証している。

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

# KelpieSSH MSI インストーラー

English documentation is available in [README.md](README.md).

KelpieSSH は WiX Toolset を使って per-user MSI installer を作成します。

MSI は次の場所へ file を install する想定です。

```text
%LocalAppData%\Programs\KelpieSSH
```

この配置により、`config`、`profiles`、`keys`、`dat`、`logs` を current user が書き込み可能な場所に置けます。
`Program Files` 配下への install は既定ではありません。`kelpie init` が Kelpie home directory 配下に file を作成・更新するためです。

## 前提条件

WiX CLI を install します。

```powershell
dotnet tool install --global wix --version 6.*
```

## MSI のビルド

Repository root で次を実行します。

```powershell
.\scripts\Build-Msi.ps1
```

生成された MSI は `.artifacts\msi` 配下に出力されます。

## 注意

- MSI は user-local install を前提とします。
- 実ホスト名、秘密鍵、password、production profile は installer source に含めないでください。
- Third-party notices と MIT license notice を配布物に含めてください。

# THIRD_PARTY_NOTICES.ja.md Version
2026.06.17

# KelpieSSH サードパーティ通知

English documentation is available in [THIRD_PARTY_NOTICES.md](../../THIRD_PARTY_NOTICES.md).

このファイルは、KelpieSSH の runtime libraries と配布 binaries が使用する third-party NuGet packages の通知をまとめる日本語版です。

KelpieSSH 自体は Apache License 2.0 で公開されています。

- Product: KelpieSSH
- Copyright: Copyright (c) 2026 Akatsukisoft
- License: Apache-2.0
- Project URL: https://github.com/katsushoe/KelpieSSH
- Notice source: `dotnet list package --include-transitive` と local package cache 内の NuGet `.nuspec` metadata

`xunit`、`FluentAssertions`、`coverlet.collector`、`Microsoft.NET.Test.Sdk` などの test-only packages は、KelpieSSH または KelpiePro と再配布する runtime dependencies ではないため、この通知対象から除外します。

# ライセンスリスク概要

現在の KelpieSSH runtime dependency set では、GPL、AGPL、LGPL、SSPL、Commons Clause、その他の non-permissive runtime NuGet dependencies は確認されていません。

Runtime package を追加または更新した場合は、英語版 [THIRD_PARTY_NOTICES.md](../../THIRD_PARTY_NOTICES.md) を更新し、日本語版も必要に応じて同期してください。

# 主要 runtime dependencies

| Package | 用途 |
| :--- | :--- |
| `SSH.NET` | SSH 接続、認証、command execution、shell session。 |
| `ModelContextProtocol` | MCP server abstractions。 |
| `ModelContextProtocol.AspNetCore` | Streamable HTTP MCP transport。 |
| `Microsoft.Extensions.Hosting` | ASP.NET Core hosting support。 |
| `Microsoft.Extensions.Hosting.WindowsServices` | Windows Service hosting integration。 |

# 再配布時の注意

KelpieSSH packages または binaries を KelpiePro などの別製品に同梱して再配布する場合は、KelpieSSH の Apache License 2.0 notice と third-party notices を installer、application about box、bundled documentation、または同等の notices location に含めてください。

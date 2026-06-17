# THIRD_PARTY_NOTICES.md Version
2026.06.16

# KelpieSSH Third-Party Notices

This file lists third-party NuGet packages used by KelpieSSH runtime libraries and distributed binaries.

KelpieSSH itself is licensed under the Apache License 2.0.

- Product: KelpieSSH
- Copyright: Copyright (c) 2026 Akatsukisoft
- License: Apache-2.0
- Project URL: https://github.com/katsushoe/KelpieSSH
- Notice source: `dotnet list package --include-transitive` and the resolved NuGet `.nuspec` metadata in the local package cache.

Test-only packages such as `xunit`, `FluentAssertions`, `coverlet.collector`, and `Microsoft.NET.Test.Sdk` are not listed below because they are not runtime dependencies intended for redistribution with KelpieSSH or KelpiePro.

# License Risk Summary

No GPL, AGPL, LGPL, SSPL, Commons Clause, or other non-permissive runtime NuGet dependencies were identified in the current KelpieSSH runtime dependency set.

Runtime licenses identified:

- MIT
- Apache-2.0

BouncyCastle.Cryptography is published on NuGet as MIT. Its package README also notes that it includes a modified Bzip2 library licensed under Apache License 2.0.

# Third-Party Runtime Packages

| Package | Version | License | URL | Copyright / Notice |
| :--- | :--- | :--- | :--- | :--- |
| `BouncyCastle.Cryptography` | `2.6.2` | MIT | https://www.bouncycastle.org/stable/nuget/csharp/website | Copyright (c) Legion of the Bouncy Castle Inc. 2000-2025 |
| `Microsoft.Extensions.AI.Abstractions` | `10.5.2` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.Caching.Abstractions` | `10.0.7` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.Configuration` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.Configuration.Abstractions` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.Configuration.Binder` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.Configuration.CommandLine` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.Configuration.EnvironmentVariables` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.Configuration.FileExtensions` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.Configuration.Json` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.Configuration.UserSecrets` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.DependencyInjection` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | `8.0.2` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.Diagnostics` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.Diagnostics.Abstractions` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.FileProviders.Abstractions` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.FileProviders.Physical` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.FileSystemGlobbing` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.Hosting` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.Hosting.Abstractions` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.Hosting.WindowsServices` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.Logging` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.Logging.Abstractions` | `8.0.3` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.Logging.Abstractions` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.Logging.Configuration` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.Logging.Console` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.Logging.Debug` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.Logging.EventLog` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.Logging.EventSource` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.Options` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.Options.ConfigurationExtensions` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `Microsoft.Extensions.Primitives` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `ModelContextProtocol` | `1.3.0` | Apache-2.0 | https://csharp.sdk.modelcontextprotocol.io/ | Copyright (c) Model Context Protocol a Series of LF Projects, LLC. |
| `ModelContextProtocol.AspNetCore` | `1.3.0` | Apache-2.0 | https://csharp.sdk.modelcontextprotocol.io/ | Copyright (c) Model Context Protocol a Series of LF Projects, LLC. |
| `ModelContextProtocol.Core` | `1.3.0` | Apache-2.0 | https://csharp.sdk.modelcontextprotocol.io/ | Copyright (c) Model Context Protocol a Series of LF Projects, LLC. |
| `SSH.NET` | `2025.1.0` | MIT | https://github.com/sshnet/SSH.NET | Copyright (c) Renci 2010-2025 |
| `System.Diagnostics.DiagnosticSource` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `System.Diagnostics.EventLog` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `System.IO.Pipelines` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `System.Net.ServerSentEvents` | `10.0.7` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `System.ServiceProcess.ServiceController` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `System.Text.Encodings.Web` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |
| `System.Text.Json` | `10.0.8` | MIT | https://dot.net/ | Copyright (c) Microsoft Corporation. All rights reserved. |

# Redistribution Notes for KelpiePro

KelpiePro may redistribute KelpieSSH binaries and reference KelpieSSH NuGet packages under the Apache License 2.0. When redistributing KelpieSSH or dependencies, include:

- KelpieSSH `LICENSE`
- This `THIRD_PARTY_NOTICES.md`
- Any additional notices for dependencies added by KelpiePro itself

If a runtime dependency is added or upgraded, regenerate the package list with:

```powershell
dotnet list package --include-transitive
```

Then confirm the resolved NuGet `.nuspec` license metadata before shipping.

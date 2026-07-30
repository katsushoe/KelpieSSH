[CmdletBinding()]
param(
    [string]$InstallDirectory = (Join-Path $env:LOCALAPPDATA "Programs\KelpieSSH"),
    [switch]$NoPath
)

$ErrorActionPreference = "Stop"

function Test-RequiredFile {
    param([string]$RelativePath)

    $path = Join-Path $PSScriptRoot $RelativePath
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "The ZIP payload is incomplete. Missing file: $RelativePath"
    }
}

Test-RequiredFile "bin\kelpie.exe"
Test-RequiredFile "bin\kelpiemcp.exe"
Test-RequiredFile "bin\mcp\KelpieMCPServer.exe"

$sourceRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$destinationRoot = [System.IO.Path]::GetFullPath($InstallDirectory)
if ($sourceRoot.TrimEnd("\") -eq $destinationRoot.TrimEnd("\")) {
    throw "Extract the ZIP to a temporary folder before running the installer."
}

$runningProcesses = Get-Process -Name "kelpie", "kelpiemcp", "KelpieMCPServer" -ErrorAction SilentlyContinue |
    Where-Object {
        try {
            $_.Path.StartsWith(
                $destinationRoot.TrimEnd("\") + "\",
                [System.StringComparison]::OrdinalIgnoreCase)
        }
        catch {
            $false
        }
    }
if ($runningProcesses) {
    throw "Kelpie is running. Stop Kelpie processes and run this installer again."
}

New-Item -ItemType Directory -Force -Path $destinationRoot | Out-Null

$payloadItems = @(
    "bin",
    "config_samples",
    "docs",
    "README.md",
    "README.ja.md",
    "CLI_OPTIONS.md",
    "COMMANDS.md",
    "CONFIG.md",
    "PROFILE_GUIDE.md",
    "MCP_GUIDE.md",
    "DOCUMENTS.md",
    "MCP_COMMANDS.md",
    "PACKAGES.md",
    "SECURITY.md",
    "THIRD_PARTY_NOTICES.md",
    "LICENSE",
    "Install-Kelpie.ps1"
)

foreach ($item in $payloadItems) {
    $source = Join-Path $sourceRoot $item
    if (Test-Path -LiteralPath $source) {
        Copy-Item -LiteralPath $source -Destination $destinationRoot -Recurse -Force
    }
}

$binDirectory = Join-Path $destinationRoot "bin"
if (!$NoPath) {
    $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
    $pathEntries = @($userPath -split ";" | Where-Object { ![string]::IsNullOrWhiteSpace($_) })
    $alreadyRegistered = $pathEntries | Where-Object {
        $_.TrimEnd("\") -ieq $binDirectory.TrimEnd("\")
    }

    if (!$alreadyRegistered) {
        $newUserPath = ($pathEntries + $binDirectory) -join ";"
        [Environment]::SetEnvironmentVariable("Path", $newUserPath, "User")
    }
}

$versionOutput = & (Join-Path $binDirectory "kelpie.exe") version
if ($LASTEXITCODE -ne 0) {
    throw "Installation verification failed."
}

Write-Host "KelpieSSH installed: $destinationRoot"
Write-Host $versionOutput
if (!$NoPath) {
    Write-Host "Open a new terminal to use kelpie and kelpiemcp from PATH."
}
Write-Host "Next: kelpie init"

param(
    [string]$Configuration = "Release",
    [string]$Version = "",
    [string]$OutputRoot = "",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

function Invoke-Checked {
    param(
        [string]$FileName,
        [string[]]$Arguments
    )

    & $FileName @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FileName failed with exit code $LASTEXITCODE."
    }
}

function Copy-Directory {
    param(
        [string]$Source,
        [string]$Destination
    )

    if (!(Test-Path -LiteralPath $Source)) {
        throw "Source directory was not found: $Source"
    }

    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    Copy-Item -Path (Join-Path $Source "*") -Destination $Destination -Recurse -Force
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$clientProject = Get-Content -LiteralPath (Join-Path $repoRoot "src\KelpieClientCommand\KelpieClientCommand.csproj")
    $Version = $clientProject.Project.PropertyGroup.Version
}

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path "release" $Version
}

$outputRootPath = if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    $OutputRoot
}
else {
    Join-Path $repoRoot $OutputRoot
}

$filesRoot = Join-Path $outputRootPath "files"
$binDir = Join-Path $filesRoot "bin"
$mcpDir = Join-Path $binDir "mcp"
$zipPath = Join-Path $outputRootPath ("KelpieSSH-" + $Version + "-x64.zip")

$resolvedRepoRoot = [System.IO.Path]::GetFullPath($repoRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$resolvedFilesRoot = [System.IO.Path]::GetFullPath($filesRoot)
if (!$resolvedFilesRoot.StartsWith($resolvedRepoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to write outside the repository: $resolvedFilesRoot"
}

if (Test-Path -LiteralPath $filesRoot) {
    Remove-Item -LiteralPath $filesRoot -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $binDir | Out-Null
New-Item -ItemType Directory -Force -Path $mcpDir | Out-Null

if (!$SkipPublish) {
    Invoke-Checked "dotnet" @("publish", (Join-Path $repoRoot "src\KelpieClientCommand\KelpieClientCommand.csproj"), "-c", $Configuration, "-o", $binDir)
    Invoke-Checked "dotnet" @("publish", (Join-Path $repoRoot "src\KelpieServerCommand\KelpieServerCommand.csproj"), "-c", $Configuration, "-o", $binDir)
    Invoke-Checked "dotnet" @("publish", (Join-Path $repoRoot "src\KelpieMCPServer\KelpieMCPServer.csproj"), "-c", $Configuration, "-o", $mcpDir)
}

Copy-Directory (Join-Path $repoRoot "config_samples") (Join-Path $filesRoot "config_samples")
Copy-Directory (Join-Path $repoRoot "docs") (Join-Path $filesRoot "docs")

$documentFiles = @(
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
    "LICENSE"
)

foreach ($documentFile in $documentFiles) {
    $sourcePath = Join-Path $repoRoot $documentFile
    if (Test-Path -LiteralPath $sourcePath) {
        Copy-Item -LiteralPath $sourcePath -Destination $filesRoot -Force
    }
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $filesRoot "*") -DestinationPath $zipPath -Force

Write-Host "ZIP payload created: $filesRoot"
Write-Host "ZIP created: $zipPath"

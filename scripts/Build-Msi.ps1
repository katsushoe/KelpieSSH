param(
    [string]$Configuration = "Release",
    [string]$Version = "",
    [string]$OutputRoot = ".artifacts\msi",
    [switch]$SkipPublish,
    [switch]$SkipWixBuild
)

$ErrorActionPreference = "Stop"

function Get-XmlEscaped {
    param([string]$Value)
    return [System.Security.SecurityElement]::Escape($Value)
}

function New-WixId {
    param(
        [string]$Prefix,
        [string]$Value
    )

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Value))
        $suffix = -join ($hash[0..7] | ForEach-Object { $_.ToString("x2") })
        return $Prefix + "_" + $suffix
    }
    finally {
        $sha256.Dispose()
    }
}

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

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$outputRootPath = Join-Path $repoRoot $OutputRoot
$payloadRoot = Join-Path $outputRootPath "payload"
$binDir = Join-Path $payloadRoot "bin"
$mcpDir = Join-Path $binDir "mcp"
$wxsPath = Join-Path $outputRootPath "KelpieSSH.generated.wxs"

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$clientProject = Get-Content -LiteralPath (Join-Path $repoRoot "src\KelpieClientCommand\KelpieClientCommand.csproj")
    $Version = $clientProject.Project.PropertyGroup.Version
}

$msiPath = Join-Path $outputRootPath ("KelpieSSH-" + $Version + "-x64.msi")

if (!$SkipPublish) {
    if (Test-Path -LiteralPath $payloadRoot) {
        Remove-Item -LiteralPath $payloadRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $binDir | Out-Null
    New-Item -ItemType Directory -Force -Path $mcpDir | Out-Null

    Invoke-Checked "dotnet" @("publish", (Join-Path $repoRoot "src\KelpieClientCommand\KelpieClientCommand.csproj"), "-c", $Configuration, "-o", $binDir)
    Invoke-Checked "dotnet" @("publish", (Join-Path $repoRoot "src\KelpieServerCommand\KelpieServerCommand.csproj"), "-c", $Configuration, "-o", $binDir)
    Invoke-Checked "dotnet" @("publish", (Join-Path $repoRoot "src\KelpieMCPServer\KelpieMCPServer.csproj"), "-c", $Configuration, "-o", $mcpDir)
}

if (!(Test-Path -LiteralPath $binDir)) {
    throw "Payload directory was not found: $binDir"
}

if ((Get-ChildItem -LiteralPath $binDir -File).Count -eq 0) {
    throw "Payload bin directory does not contain files: $binDir"
}

if (!(Test-Path -LiteralPath $mcpDir) -or (Get-ChildItem -LiteralPath $mcpDir -File).Count -eq 0) {
    throw "Payload MCP directory does not contain files: $mcpDir"
}

New-Item -ItemType Directory -Force -Path $outputRootPath | Out-Null

$componentRefs = New-Object System.Collections.Generic.List[string]
$binComponentXml = New-Object System.Collections.Generic.List[string]
$mcpComponentXml = New-Object System.Collections.Generic.List[string]

foreach ($file in Get-ChildItem -LiteralPath $binDir -File) {
    $componentId = New-WixId "CmpBin" $file.FullName
    $fileId = New-WixId "FileBin" $file.FullName
    $componentRefs.Add("      <ComponentRef Id=`"$componentId`" />")
    $binComponentXml.Add("      <Component Id=`"$componentId`" Guid=`"*`">")
    $binComponentXml.Add("        <File Id=`"$fileId`" Source=`"$(Get-XmlEscaped $file.FullName)`" KeyPath=`"yes`" />")
    $binComponentXml.Add("      </Component>")
}

foreach ($file in Get-ChildItem -LiteralPath $mcpDir -File) {
    $componentId = New-WixId "CmpMcp" $file.FullName
    $fileId = New-WixId "FileMcp" $file.FullName
    $componentRefs.Add("      <ComponentRef Id=`"$componentId`" />")
    $mcpComponentXml.Add("      <Component Id=`"$componentId`" Guid=`"*`">")
    $mcpComponentXml.Add("        <File Id=`"$fileId`" Source=`"$(Get-XmlEscaped $file.FullName)`" KeyPath=`"yes`" />")
    $mcpComponentXml.Add("      </Component>")
}

$wxsLines = @(
    "<?xml version=`"1.0`" encoding=`"UTF-8`"?>",
    "<Wix xmlns=`"http://wixtoolset.org/schemas/v4/wxs`">",
    "  <Package Name=`"KelpieSSH`" Manufacturer=`"Akatsukisoft`" Version=`"$Version`" UpgradeCode=`"9E7E2CC6-3CA9-4D44-BFD4-CE0E0A9D4A9C`" Scope=`"perUser`">",
    "    <MajorUpgrade DowngradeErrorMessage=`"A newer version of KelpieSSH is already installed.`" />",
    "    <MediaTemplate EmbedCab=`"yes`" />",
    "    <StandardDirectory Id=`"LocalAppDataFolder`">",
    "      <Directory Id=`"ProgramsFolder`" Name=`"Programs`">",
    "        <Directory Id=`"INSTALLFOLDER`" Name=`"KelpieSSH`">",
    "          <Directory Id=`"ConfigFolder`" Name=`"config`" />",
    "          <Directory Id=`"ProfilesFolder`" Name=`"profiles`" />",
    "          <Directory Id=`"KeysFolder`" Name=`"keys`" />",
    "          <Directory Id=`"DataFolder`" Name=`"dat`" />",
    "          <Directory Id=`"LogsFolder`" Name=`"logs`" />",
    "          <Directory Id=`"BinFolder`" Name=`"bin`">",
    "            <Directory Id=`"McpFolder`" Name=`"mcp`" />",
    "          </Directory>",
    "        </Directory>",
    "      </Directory>",
    "    </StandardDirectory>",
    "    <DirectoryRef Id=`"ConfigFolder`">",
    "      <Component Id=`"ConfigFolderComponent`" Guid=`"*`">",
    "        <CreateFolder />",
    "        <RegistryValue Root=`"HKCU`" Key=`"Software\Akatsukisoft\KelpieSSH`" Name=`"ConfigFolder`" Type=`"integer`" Value=`"1`" KeyPath=`"yes`" />",
    "      </Component>",
    "    </DirectoryRef>",
    "    <DirectoryRef Id=`"ProfilesFolder`">",
    "      <Component Id=`"ProfilesFolderComponent`" Guid=`"*`">",
    "        <CreateFolder />",
    "        <RegistryValue Root=`"HKCU`" Key=`"Software\Akatsukisoft\KelpieSSH`" Name=`"ProfilesFolder`" Type=`"integer`" Value=`"1`" KeyPath=`"yes`" />",
    "      </Component>",
    "    </DirectoryRef>",
    "    <DirectoryRef Id=`"KeysFolder`">",
    "      <Component Id=`"KeysFolderComponent`" Guid=`"*`">",
    "        <CreateFolder />",
    "        <RegistryValue Root=`"HKCU`" Key=`"Software\Akatsukisoft\KelpieSSH`" Name=`"KeysFolder`" Type=`"integer`" Value=`"1`" KeyPath=`"yes`" />",
    "      </Component>",
    "    </DirectoryRef>",
    "    <DirectoryRef Id=`"DataFolder`">",
    "      <Component Id=`"DataFolderComponent`" Guid=`"*`">",
    "        <CreateFolder />",
    "        <RegistryValue Root=`"HKCU`" Key=`"Software\Akatsukisoft\KelpieSSH`" Name=`"DataFolder`" Type=`"integer`" Value=`"1`" KeyPath=`"yes`" />",
    "      </Component>",
    "    </DirectoryRef>",
    "    <DirectoryRef Id=`"LogsFolder`">",
    "      <Component Id=`"LogsFolderComponent`" Guid=`"*`">",
    "        <CreateFolder />",
    "        <RegistryValue Root=`"HKCU`" Key=`"Software\Akatsukisoft\KelpieSSH`" Name=`"LogsFolder`" Type=`"integer`" Value=`"1`" KeyPath=`"yes`" />",
    "      </Component>",
    "    </DirectoryRef>",
    "    <DirectoryRef Id=`"BinFolder`">",
    "      <Component Id=`"UserPathComponent`" Guid=`"*`">",
    "        <Environment Id=`"AddKelpieBinToPath`" Name=`"PATH`" Value=`"[BinFolder]`" Permanent=`"no`" Part=`"last`" Action=`"set`" System=`"no`" />",
    "        <RegistryValue Root=`"HKCU`" Key=`"Software\Akatsukisoft\KelpieSSH`" Name=`"PathRegistered`" Type=`"integer`" Value=`"1`" KeyPath=`"yes`" />",
    "      </Component>"
)

$wxsLines += $binComponentXml
$wxsLines += @(
    "    </DirectoryRef>",
    "    <DirectoryRef Id=`"McpFolder`">"
)
$wxsLines += $mcpComponentXml
$wxsLines += @(
    "    </DirectoryRef>",
    "    <Feature Id=`"MainFeature`" Title=`"KelpieSSH`" Level=`"1`">",
    "      <ComponentRef Id=`"ConfigFolderComponent`" />",
    "      <ComponentRef Id=`"ProfilesFolderComponent`" />",
    "      <ComponentRef Id=`"KeysFolderComponent`" />",
    "      <ComponentRef Id=`"DataFolderComponent`" />",
    "      <ComponentRef Id=`"LogsFolderComponent`" />",
    "      <ComponentRef Id=`"UserPathComponent`" />"
)
$wxsLines += $componentRefs
$wxsLines += @(
    "    </Feature>",
    "  </Package>",
    "</Wix>"
)

Set-Content -LiteralPath $wxsPath -Value $wxsLines -Encoding UTF8

if ($SkipWixBuild) {
    Write-Host "Generated WiX source: $wxsPath"
    return
}

$wixCommand = Get-Command wix -ErrorAction SilentlyContinue
if ($null -eq $wixCommand) {
    throw "WiX CLI was not found. Install it with: dotnet tool install --global wix"
}

Invoke-Checked "wix" @("build", $wxsPath, "-arch", "x64", "-out", $msiPath)
Write-Host "MSI created: $msiPath"

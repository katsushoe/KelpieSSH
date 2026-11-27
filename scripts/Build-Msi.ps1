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

function New-WixGuid {
    param([string]$Value)

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($Value))
        $guidBytes = [byte[]]$hash[0..15]
        return (New-Object System.Guid (,$guidBytes)).ToString().ToUpperInvariant()
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
$clientPublishDir = Join-Path $payloadRoot ".publish-client"
$serverPublishDir = Join-Path $payloadRoot ".publish-server"
$wxsPath = Join-Path $outputRootPath "KelpieSSH.generated.wxs"

if ([string]::IsNullOrWhiteSpace($Version)) {
    [xml]$clientProject = Get-Content -LiteralPath (Join-Path $repoRoot "src\KelpieClientCommand\KelpieClientCommand.csproj")
    $Version = $clientProject.Project.PropertyGroup.Version
}

$msiPath = Join-Path $outputRootPath ("KelpieSSH-" + $Version + "-x64.msi")

$resolvedRepoRoot = [System.IO.Path]::GetFullPath($repoRoot).TrimEnd('\') + '\'
if (![System.IO.Path]::GetFullPath($payloadRoot).StartsWith($resolvedRepoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to write outside the repository: $payloadRoot"
}

if (!$SkipPublish) {
    if (Test-Path -LiteralPath $payloadRoot) {
        Remove-Item -LiteralPath $payloadRoot -Recurse -Force
    }

    New-Item -ItemType Directory -Force -Path $binDir | Out-Null
    New-Item -ItemType Directory -Force -Path $mcpDir | Out-Null
    New-Item -ItemType Directory -Force -Path $clientPublishDir, $serverPublishDir | Out-Null

    Invoke-Checked "dotnet" @("publish", "--disable-build-servers", (Join-Path $repoRoot "src\KelpieClientCommand\KelpieClientCommand.csproj"), "-c", $Configuration, "-o", $clientPublishDir)
    Invoke-Checked "dotnet" @("publish", "--disable-build-servers", (Join-Path $repoRoot "src\KelpieServerCommand\KelpieServerCommand.csproj"), "-c", $Configuration, "-o", $serverPublishDir)
    Invoke-Checked "dotnet" @("publish", "--disable-build-servers", (Join-Path $repoRoot "src\KelpieMCPTray\KelpieMCPTray.csproj"), "-c", $Configuration, "-o", $serverPublishDir)
    Invoke-Checked "dotnet" @("publish", "--disable-build-servers", (Join-Path $repoRoot "src\KelpieMCPServer\KelpieMCPServer.csproj"), "-c", $Configuration, "-o", $mcpDir)
    Copy-Item -Path (Join-Path $clientPublishDir "*") -Destination $binDir -Recurse -Force
    Copy-Item -Path (Join-Path $serverPublishDir "*") -Destination $binDir -Recurse -Force
    Remove-Item -LiteralPath $clientPublishDir, $serverPublishDir -Recurse -Force
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

foreach ($requiredFile in @("kelpie.exe", "kelpiemcp.exe", "KelpieMCPTray.exe")) {
    if (!(Test-Path -LiteralPath (Join-Path $binDir $requiredFile) -PathType Leaf)) {
        throw "Payload is missing required file: $requiredFile"
    }
}

if (!(Test-Path -LiteralPath (Join-Path $mcpDir "KelpieMCPServer.exe") -PathType Leaf)) {
    throw "Payload is missing required file: KelpieMCPServer.exe"
}

New-Item -ItemType Directory -Force -Path $outputRootPath | Out-Null

$componentRefs = New-Object System.Collections.Generic.List[string]
$binComponentXml = New-Object System.Collections.Generic.List[string]
$mcpComponentXml = New-Object System.Collections.Generic.List[string]

function Add-PayloadDirectory {
    param([string]$Path, [string]$Identity, [System.Collections.Generic.List[string]]$Lines, [string]$SkipDirectory = '')
    foreach ($file in Get-ChildItem -LiteralPath $Path -File) {
        $fileIdentity = $Identity + '/' + $file.Name
        $componentPrefix = if ($Identity -eq 'bin/mcp') { 'CmpMcp' } else { 'CmpBin' }
        $filePrefix = if ($Identity -eq 'bin/mcp') { 'FileMcp' } else { 'FileBin' }
        $componentId = New-WixId $componentPrefix $fileIdentity
        $componentGuid = New-WixGuid $fileIdentity
        $fileId = New-WixId $filePrefix $fileIdentity
        $componentRefs.Add("      <ComponentRef Id=`"$componentId`" />")
        $Lines.Add("      <Component Id=`"$componentId`" Guid=`"$componentGuid`">")
        $Lines.Add("        <File Id=`"$fileId`" Source=`"$(Get-XmlEscaped $file.FullName)`" />")
        $Lines.Add("        <RegistryValue Root=`"HKCU`" Key=`"Software\Akatsukisoft\KelpieSSH\Files`" Name=`"$componentId`" Type=`"integer`" Value=`"1`" KeyPath=`"yes`" />")
        $Lines.Add('      </Component>')
    }
    foreach ($directory in Get-ChildItem -LiteralPath $Path -Directory) {
        if ($directory.Name -eq $SkipDirectory) { continue }
        $directoryIdentity = $Identity + '/' + $directory.Name
        $directoryId = New-WixId 'DirPayload' $directoryIdentity
        $Lines.Add("      <Directory Id=`"$directoryId`" Name=`"$(Get-XmlEscaped $directory.Name)`">")
        Add-PayloadDirectory -Path $directory.FullName -Identity $directoryIdentity -Lines $Lines
        $Lines.Add('      </Directory>')
    }
}
Add-PayloadDirectory -Path $binDir -Identity 'bin' -Lines $binComponentXml -SkipDirectory 'mcp'
Add-PayloadDirectory -Path $mcpDir -Identity 'bin/mcp' -Lines $mcpComponentXml

$wxsLines = @(
    "<?xml version=`"1.0`" encoding=`"UTF-8`"?>",
    "<Wix xmlns=`"http://wixtoolset.org/schemas/v4/wxs`">",
    "  <Package Name=`"KelpieSSH`" Manufacturer=`"Akatsukisoft`" Version=`"$Version`" UpgradeCode=`"9E7E2CC6-3CA9-4D44-BFD4-CE0E0A9D4A9C`" Scope=`"perUser`">",
    # Windows Installer ignores the fourth version field; releases such as 0.3.16.1 must still replace 0.3.16.0.
    "    <MajorUpgrade AllowSameVersionUpgrades=`"yes`" DowngradeErrorMessage=`"A newer version of KelpieSSH is already installed.`" />",
    "    <MediaTemplate EmbedCab=`"yes`" />",
    "    <Property Id=`"EXISTINGINSTALLFOLDER`" Secure=`"yes`">",
    "      <RegistrySearch Id=`"FindInstallFolder`" Root=`"HKCU`" Key=`"Software\Akatsukisoft\KelpieSSH`" Name=`"InstallFolder`" Type=`"raw`" />",
    "    </Property>",
    "    <SetProperty Id=`"INSTALLFOLDER`" Value=`"[EXISTINGINSTALLFOLDER]`" Before=`"CostFinalize`" Sequence=`"both`" Condition=`"EXISTINGINSTALLFOLDER AND NOT INSTALLFOLDER`" />",
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
    "    <DirectoryRef Id=`"INSTALLFOLDER`">",
    "      <Component Id=`"InstallLocationComponent`" Guid=`"12DFE865-9E0E-46DF-BE36-67D2D6D430C2`">",
    "        <RegistryValue Root=`"HKCU`" Key=`"Software\Akatsukisoft\KelpieSSH`" Name=`"InstallFolder`" Value=`"[INSTALLFOLDER]`" Type=`"string`" KeyPath=`"yes`" />",
    "      </Component>",
    "    </DirectoryRef>",
    "    <DirectoryRef Id=`"ConfigFolder`">",
    "      <Component Id=`"ConfigFolderComponent`" Guid=`"*`">",
    "        <CreateFolder />",
    "        <RemoveFolder Id=`"RemoveConfigFolder`" On=`"uninstall`" />",
    "        <RegistryValue Root=`"HKCU`" Key=`"Software\Akatsukisoft\KelpieSSH`" Name=`"ConfigFolder`" Type=`"integer`" Value=`"1`" KeyPath=`"yes`" />",
    "      </Component>",
    "    </DirectoryRef>",
    "    <DirectoryRef Id=`"ProfilesFolder`">",
    "      <Component Id=`"ProfilesFolderComponent`" Guid=`"*`">",
    "        <CreateFolder />",
    "        <RemoveFolder Id=`"RemoveProfilesFolder`" On=`"uninstall`" />",
    "        <RegistryValue Root=`"HKCU`" Key=`"Software\Akatsukisoft\KelpieSSH`" Name=`"ProfilesFolder`" Type=`"integer`" Value=`"1`" KeyPath=`"yes`" />",
    "      </Component>",
    "    </DirectoryRef>",
    "    <DirectoryRef Id=`"KeysFolder`">",
    "      <Component Id=`"KeysFolderComponent`" Guid=`"*`">",
    "        <CreateFolder />",
    "        <RemoveFolder Id=`"RemoveKeysFolder`" On=`"uninstall`" />",
    "        <RegistryValue Root=`"HKCU`" Key=`"Software\Akatsukisoft\KelpieSSH`" Name=`"KeysFolder`" Type=`"integer`" Value=`"1`" KeyPath=`"yes`" />",
    "      </Component>",
    "    </DirectoryRef>",
    "    <DirectoryRef Id=`"DataFolder`">",
    "      <Component Id=`"DataFolderComponent`" Guid=`"*`">",
    "        <CreateFolder />",
    "        <RemoveFolder Id=`"RemoveDataFolder`" On=`"uninstall`" />",
    "        <RegistryValue Root=`"HKCU`" Key=`"Software\Akatsukisoft\KelpieSSH`" Name=`"DataFolder`" Type=`"integer`" Value=`"1`" KeyPath=`"yes`" />",
    "      </Component>",
    "    </DirectoryRef>",
    "    <DirectoryRef Id=`"LogsFolder`">",
    "      <Component Id=`"LogsFolderComponent`" Guid=`"*`">",
    "        <CreateFolder />",
    "        <RemoveFolder Id=`"RemoveLogsFolder`" On=`"uninstall`" />",
    "        <RegistryValue Root=`"HKCU`" Key=`"Software\Akatsukisoft\KelpieSSH`" Name=`"LogsFolder`" Type=`"integer`" Value=`"1`" KeyPath=`"yes`" />",
    "      </Component>",
    "    </DirectoryRef>",
    "    <DirectoryRef Id=`"BinFolder`">",
    "      <Component Id=`"UserPathComponent`" Guid=`"*`">",
    "        <Environment Id=`"AddKelpieBinToPath`" Name=`"PATH`" Value=`"[BinFolder]`" Permanent=`"no`" Part=`"last`" Action=`"set`" System=`"no`" />",
    "        <RemoveFolder Id=`"RemoveMcpFolder`" Directory=`"McpFolder`" On=`"uninstall`" />",
    "        <RemoveFolder Id=`"RemoveBinFolder`" Directory=`"BinFolder`" On=`"uninstall`" />",
    "        <RemoveFolder Id=`"RemoveInstallFolder`" Directory=`"INSTALLFOLDER`" On=`"uninstall`" />",
    "        <RemoveFolder Id=`"RemoveProgramsFolder`" Directory=`"ProgramsFolder`" On=`"uninstall`" />",
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
    "      <ComponentRef Id=`"InstallLocationComponent`" />",
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

$trayFileId = New-WixId "FileBin" "bin/KelpieMCPTray.exe"
$removeTrayAction = @(
    "    <CustomAction Id=`"UnregisterTrayLogon`" FileRef=`"$trayFileId`" ExeCommand=`"--unregister-logon --install-root &quot;[INSTALLFOLDER].&quot;`" Execute=`"immediate`" Return=`"ignore`" />",
    "    <InstallExecuteSequence>",
    "      <Custom Action=`"UnregisterTrayLogon`" Before=`"RemoveFiles`" Condition=`"REMOVE=&quot;ALL&quot; AND NOT UPGRADINGPRODUCTCODE`" />",
    "    </InstallExecuteSequence>"
)
$packageEnd = [array]::IndexOf($wxsLines, '  </Package>')
$wxsLines = $wxsLines[0..($packageEnd - 1)] + $removeTrayAction + $wxsLines[$packageEnd..($wxsLines.Length - 1)]

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

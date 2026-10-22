$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$probe = Join-Path $repository 'tests/KelpieSSH.ServiceIdentityProbe/bin/Debug/net8.0/KelpieSSH.ServiceIdentityProbe.exe'
if (-not (Test-Path -LiteralPath $probe -PathType Leaf)) { throw 'Build the service identity probe first.' }
$directory = Join-Path $repository ('.artifacts/worker-regression/direct-' + [guid]::NewGuid().ToString('N'))
$elapsed = [Diagnostics.Stopwatch]::StartNew()
& $probe $directory --allow-current-identity
if ($LASTEXITCODE -ne 0) { throw ('Probe regression failed; results: ' + $directory) }
$elapsed.Stop()
$result = [IO.File]::ReadAllText((Join-Path $directory 'result.json')) | ConvertFrom-Json
$actualSystem = [Security.Principal.WindowsIdentity]::GetCurrent().IsSystem
if ($result.IsSystem -ne $actualSystem -or $result.Passed -ne 5 -or $result.Failed -ne 0 -or @($result.Cases).Count -ne 5) {
    throw 'Probe result identity or case results mismatch.'
}
if (@($result.Cases | Where-Object {$_.Outcome -ne 'Passed'}).Count -ne 0) { throw 'A case did not pass.' }
$state = [IO.File]::ReadAllText((Join-Path $directory 'worker-state.json')) | ConvertFrom-Json
if ($state.Stage -ne 'Finished' -or -not $state.ProcessId) { throw 'Missing execution stage or identity diagnostics.' }
if ((Get-Item (Join-Path $directory 'test.log')).Length -eq 0) { throw 'Missing case log.' }
Write-Output ('Direct probe: PASS; five cases; actual IsSystem=' + $result.IsSystem + '; seconds=' + $elapsed.Elapsed.TotalSeconds + '; results: ' + $directory)
if (-not $actualSystem) {
    $rejected = Join-Path $repository ('.artifacts/worker-regression/identity-rejected-' + [guid]::NewGuid().ToString('N'))
    & $probe $rejected
    if ($LASTEXITCODE -eq 0) { throw 'Non-SYSTEM execution was accepted without the regression flag.' }
    $failure = [IO.File]::ReadAllText((Join-Path $rejected 'result.json')) | ConvertFrom-Json
    if ($failure.IsSystem -or $failure.ExitCode -eq 0 -or $failure.Cases.Count -ne 0 -or $failure.Error -notlike '*did not run as LocalSystem*') {
        throw 'Identity guard did not reject before executing tests.'
    }
    Write-Output 'Direct probe identity guard: PASS (non-SYSTEM identity rejected before tests)'
}

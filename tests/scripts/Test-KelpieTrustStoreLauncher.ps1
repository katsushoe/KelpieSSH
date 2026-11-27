$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source = [IO.File]::ReadAllText((Join-Path $repository 'scripts/Test-KelpieTrustStoreServiceIdentity.ps1'))
$marker = '$registered = $false'
$start = $source.IndexOf($marker, [StringComparison]::Ordinal)
if ($start -lt 0) { throw 'Scheduled task runner block was not found.' }
$runner = [scriptblock]::Create($source.Substring($start))

# All scheduler commands are test doubles. These cases never register a Windows task.
function New-ScheduledTaskAction {
    param($Execute, $Argument, $WorkingDirectory)
    if ($Execute -notlike '*KelpieSSH.ServiceIdentityProbe.exe' -or $Argument -match 'allow-current-identity') { throw 'Expected direct probe execution with mandatory SYSTEM guard.' }
    [pscustomobject]@{ Execute = $Execute; Arguments = $Argument }
}
function New-ScheduledTaskPrincipal {
    param($UserId, $LogonType, $RunLevel)
    if ($UserId -ne 'SYSTEM' -or $LogonType -ne 'ServiceAccount') { throw 'Unexpected task principal.' }
    [pscustomobject]@{ UserId = $UserId }
}
function New-ScheduledTaskSettingsSet {
    param($ExecutionTimeLimit, [switch]$AllowStartIfOnBatteries, [switch]$DontStopIfGoingOnBatteries)
    if (-not $AllowStartIfOnBatteries -or -not $DontStopIfGoingOnBatteries) { throw 'Battery conditions may prevent execution.' }
    [pscustomobject]@{ ExecutionTimeLimit = $ExecutionTimeLimit }
}
function Register-ScheduledTask { param($TaskName, $Action, $Principal, $Settings) }
function Start-ScheduledTask {
    param($TaskName)
    $script:LauncherProbeStarted = $true
    if ($script:LauncherProbeCase -in @('Success', 'MissingCases', 'WrongIdentity')) {
        # Synthetic result tests the runner contract; it is not a LocalSystem test result.
        $cases = if ($script:LauncherProbeCase -eq 'Success') { @('MissingKey','InvalidKeyLength','UnrelatedKey','BackupRestore','AccessDenied') | ForEach-Object { @{Name=$_; Outcome='Passed'} } } else { @() }
        [IO.File]::WriteAllText($resultPath, (@{IsSystem=($script:LauncherProbeCase -ne 'WrongIdentity'); ExitCode=0; Passed=5; Failed=0; Cases=@($cases)} | ConvertTo-Json -Depth 4))
    }
    if ($script:LauncherProbeCase -eq 'RunningTimeout') {
        [IO.File]::WriteAllText((Join-Path $runDirectory 'worker-state.json'), '{"Stage":"BackupRestore"}')
    }
}
function Get-ScheduledTask {
    param($TaskName)
    $state = switch ($script:LauncherProbeCase) { 'QueuedTimeout' {'Queued'} 'RunningTimeout' {'Running'} default {'Ready'} }
    [pscustomobject]@{ State = $state }
}
function Get-ScheduledTaskInfo {
    param($TaskName)
    $runTime = if ($script:LauncherProbeStarted -and $script:LauncherProbeCase -ne 'QueuedTimeout') { [DateTime]::UtcNow } else { [DateTime]::MinValue }
    $code = switch ($script:LauncherProbeCase) { 'Success' { 0 } 'QueuedTimeout' { 267011 } default { 1 } }
    [pscustomobject]@{ LastRunTime = $runTime; LastTaskResult = $code }
}
function Stop-ScheduledTask { param($TaskName) $script:LauncherProbeStopped = $true }
function Unregister-ScheduledTask { param($TaskName, [switch]$Confirm) $script:LauncherProbeUnregistered = $true }

foreach ($case in @('CompletedFailure', 'QueuedTimeout', 'RunningTimeout', 'Success', 'MissingCases', 'WrongIdentity')) {
    $script:LauncherProbeCase = $case
    $script:LauncherProbeStarted = $false
    $script:LauncherProbeStopped = $false
    $script:LauncherProbeUnregistered = $false
    $runDirectory = Join-Path $repository ('.artifacts/launcher-regression/' + $case + '-' + [guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($runDirectory) | Out-Null
    $probe = Join-Path $runDirectory 'KelpieSSH.ServiceIdentityProbe.exe'
    $resultPath = Join-Path $runDirectory 'result.json'
    $taskName = 'TestDoubleOnly-' + $case
    $TimeoutSeconds = if ($case -in @('QueuedTimeout', 'RunningTimeout')) { 0 } else { 90 }
    $elapsed = [Diagnostics.Stopwatch]::StartNew()
    $failure = $null
    $output = $null
    try { $output = & $runner } catch { $failure = $_.Exception.Message }
    $elapsed.Stop()
    if (-not $script:LauncherProbeUnregistered) { throw "$case did not unregister the simulated task." }
    $diagnostics = [IO.File]::ReadAllText((Join-Path $runDirectory 'task-diagnostics.json')) | ConvertFrom-Json
    switch ($case) {
        'CompletedFailure' {
            if ($failure -notlike '*exited without a result*Task result: 1*' -or $elapsed.Elapsed.TotalSeconds -gt 5) { throw 'Worker failure was not reported promptly.' }
            if ($diagnostics.LastTaskResult -ne 1) { throw 'Failure diagnostics lost the task result.' }
        }
        'QueuedTimeout' {
            if ($failure -notlike '*timed out*Task state: Queued*' -or -not $script:LauncherProbeStopped) { throw 'Queued timeout was not diagnosed and stopped.' }
        }
        'Success' {
            if ($failure -or $output.Passed -ne 5 -or -not $output.IsSystem) { throw 'Synthetic success result was not handled.' }
        }
        'MissingCases' {
            if ($failure -notlike '*Expected all five*') { throw 'Missing actual case results were accepted.' }
        }
        'WrongIdentity' {
            if ($failure -notlike '*Service identity tests failed*') { throw 'Non-SYSTEM success was accepted.' }
        }
        'RunningTimeout' {
            if ($failure -notlike '*Task state: Running*stage: BackupRestore*' -or -not $script:LauncherProbeStopped) { throw 'Running timeout lost the execution stage or task cleanup.' }
        }
    }
    Write-Output ($case + ': PASS (scheduler test doubles)')
}

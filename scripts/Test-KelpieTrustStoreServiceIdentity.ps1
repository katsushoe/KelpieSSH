param([ValidateRange(10, 900)][int]$TimeoutSeconds = 600)

$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this script from an administrator PowerShell terminal. No scheduled task was created.'
}
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$probe = Join-Path $repository 'tests/KelpieSSH.ServiceIdentityProbe/bin/Debug/net8.0/KelpieSSH.ServiceIdentityProbe.exe'
if (-not (Test-Path -LiteralPath $probe -PathType Leaf)) {
    throw 'Build tests/KelpieSSH.ServiceIdentityProbe/KelpieSSH.ServiceIdentityProbe.csproj before running this script.'
}
$runDirectory = Join-Path $repository ('.artifacts/service-ops-results/system-' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($runDirectory) | Out-Null
$resultPath = Join-Path $runDirectory 'result.json'
$taskName = 'KelpieSSH-TrustStore-Probe-' + [guid]::NewGuid().ToString('N')
$registered = $false
try {
    # Start the .NET probe itself: no PowerShell worker, SDK, VSTest, or test-host child.
    $action = New-ScheduledTaskAction -Execute $probe -Argument ('"' + $runDirectory + '"') -WorkingDirectory $repository
    $taskPrincipal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
    $settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Seconds ($TimeoutSeconds + 30)) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
    Register-ScheduledTask -TaskName $taskName -Action $action -Principal $taskPrincipal -Settings $settings | Out-Null
    $registered = $true
    $previousRunTime = (Get-ScheduledTaskInfo -TaskName $taskName).LastRunTime
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    Start-ScheduledTask -TaskName $taskName
    while (-not (Test-Path -LiteralPath $resultPath)) {
        $taskInfo = Get-ScheduledTaskInfo -TaskName $taskName
        $task = Get-ScheduledTask -TaskName $taskName
        if ($taskInfo.LastRunTime -gt $previousRunTime -and $task.State -notin @('Running', 'Queued')) {
            throw ('Service identity worker exited without a result. Task result: ' + $taskInfo.LastTaskResult + '. Results: ' + $runDirectory)
        }
        if ([DateTime]::UtcNow -ge $deadline) {
            $stagePath = Join-Path $runDirectory 'worker-state.json'
            $stage = if (Test-Path -LiteralPath $stagePath) { (Get-Content -LiteralPath $stagePath -Raw | ConvertFrom-Json).Stage } else { 'ProcessStartup' }
            throw ('Service identity verification timed out. Task state: ' + $task.State + '; task result: ' + $taskInfo.LastTaskResult + '; stage: ' + $stage + '. Results: ' + $runDirectory)
        }
        Start-Sleep -Milliseconds 500
    }
    while ((Get-ScheduledTask -TaskName $taskName).State -eq 'Running') {
        if ([DateTime]::UtcNow -ge $deadline) { throw ('Service identity verification did not finish. Results: ' + $runDirectory) }
        Start-Sleep -Milliseconds 500
    }
    $result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
    if (-not $result.IsSystem -or $result.ExitCode -ne 0) {
        throw ('Service identity tests failed: ' + $result.Error + ' Results: ' + $runDirectory)
    }
    $expectedCases = @('MissingKey', 'InvalidKeyLength', 'UnrelatedKey', 'BackupRestore', 'AccessDenied')
    $actualCases = @($result.Cases | ForEach-Object { $_.Name } | Sort-Object -Unique)
    if ($result.Passed -ne 5 -or $result.Failed -ne 0 -or @($result.Cases).Count -ne 5 -or
        @($result.Cases | Where-Object { $_.Outcome -ne 'Passed' }).Count -ne 0 -or
        (Compare-Object ($expectedCases | Sort-Object) $actualCases)) {
        throw 'Expected all five TrustStore operational cases to pass.'
    }
    [pscustomobject]@{ IsSystem = $true; Passed = 5; Failed = 0; ResultsDirectory = $runDirectory }
} finally {
    if ($registered) {
        try {
            $task = Get-ScheduledTask -TaskName $taskName
            $taskInfo = Get-ScheduledTaskInfo -TaskName $taskName
            $diagnostics = [ordered]@{
                TaskName = $taskName
                State = [string]$task.State
                LastRunTime = $taskInfo.LastRunTime.ToString('o')
                LastTaskResult = $taskInfo.LastTaskResult
                ExecutionHost = '.NET 8 standalone probe'
            }
            [IO.File]::WriteAllText((Join-Path $runDirectory 'task-diagnostics.json'), ($diagnostics | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
            if ($task.State -in @('Running', 'Queued')) { Stop-ScheduledTask -TaskName $taskName }
        } finally {
            Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
        }
    }
}

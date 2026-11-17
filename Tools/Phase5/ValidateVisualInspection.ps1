param([string]$RunName = 'Validation')
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$logRoot = Join-Path $projectRoot 'Logs/VisualInspection20261117'
$recordPath = Join-Path $logRoot "$RunName.run.json"
if (Test-Path -LiteralPath $recordPath) { throw 'Use a fresh RunName; preserve earlier evidence.' }
$labRoot = Join-Path $projectRoot 'Tools/Phase5/Lab'
$unityExe = 'C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe'
$logPath = Join-Path $logRoot "$RunName.unity.log"
$arguments = "-batchmode -nographics -projectPath `"$labRoot`" -executeMethod Phase5VisualInspectionValidation.Run -logFile `"$logPath`""
$record = @{ executable = $unityExe; arguments = $arguments; timeoutSeconds = 120; startedUtc = [DateTime]::UtcNow.ToString('o'); timedOut = $false }
$watch = [Diagnostics.Stopwatch]::StartNew()
$job = Start-Process -FilePath $unityExe -ArgumentList $arguments -WindowStyle Hidden -PassThru
$record.pid = $job.Id
$record | ConvertTo-Json | Set-Content -LiteralPath $recordPath
try {
    if (-not $job.WaitForExit(120000)) {
        $record.timedOut = $true
        & taskkill /PID $job.Id /T /F | Out-Null
        $record.exitCode = $null
    } else {
        $job.WaitForExit()
        $job.Refresh()
        $record.exitCode = $job.ExitCode
    }
} finally {
    $watch.Stop()
    $record.wallSeconds = $watch.Elapsed.TotalSeconds
    $record | ConvertTo-Json | Set-Content -LiteralPath $recordPath
}
if ($record.timedOut -or $record.exitCode -ne 0) { throw "Bounded Unity validation failed; inspect $logPath" }
if (-not (Select-String -LiteralPath $logPath -SimpleMatch 'PHASE5_VISUAL_INSPECTION_VALIDATION_PASS' -Quiet)) { throw 'PASS marker missing' }
Get-Content -LiteralPath (Join-Path $logRoot 'Validation.txt')

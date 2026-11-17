param([string]$RunName='learned-rendered')
$ErrorActionPreference='Stop'
$projectRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$logRoot=Join-Path $projectRoot 'Logs/DemoPolicy20261117'
$recordPath=Join-Path $logRoot "$RunName.run.json"
if(Test-Path -LiteralPath $recordPath){throw 'Use a fresh render name'}
$exe=Join-Path $projectRoot 'Builds/DemoPolicyCandidate/CombatDemo.exe'
$output=Join-Path $logRoot $RunName
$logPath=Join-Path $logRoot "$RunName.player.log"
$arguments="-screen-fullscreen 0 -screen-width 1280 -screen-height 720 --demo-learned-verification --verification-output `"$output`" -logFile `"$logPath`""
$record=@{executable=$exe;arguments=$arguments;timeoutSeconds=40;startedUtc=[DateTime]::UtcNow.ToString('o');timedOut=$false;training=$false}
$watch=[Diagnostics.Stopwatch]::StartNew();$job=Start-Process $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
$record.pid=$job.Id;$record|ConvertTo-Json|Set-Content -LiteralPath $recordPath
try{if(-not $job.WaitForExit(40000)){$record.timedOut=$true;& taskkill /PID $job.Id /T /F |Out-Null;$record.exitCode=$null}else{$job.WaitForExit();$job.Refresh();$record.exitCode=$job.ExitCode}}
finally{$watch.Stop();$record.wallSeconds=$watch.Elapsed.TotalSeconds;$record|ConvertTo-Json|Set-Content -LiteralPath $recordPath}
if($record.timedOut -or $record.exitCode -ne 0){throw "Rendered verification failed: $logPath"}
if(-not (Select-String -LiteralPath $logPath -SimpleMatch 'LEARNED_RENDER_VERIFICATION_PASS' -Quiet)){throw 'Rendered PASS missing'}
Get-Content -LiteralPath (Join-Path $output 'verification.json')

param([string]$RunName='StandaloneSmoke')
$ErrorActionPreference='Stop'
$projectRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$folder=Join-Path $projectRoot 'Logs/RuntimeIntegration20261117'
$recordPath=Join-Path $folder "$RunName.run.json"
if(Test-Path -LiteralPath $recordPath){throw 'Use a fresh RunName; preserve previous evidence.'}
$log=Join-Path $folder "$RunName.player.log"
$executable=Join-Path $projectRoot 'Builds/CombatDemo/CombatDemo.exe'
$arguments="-batchmode -nographics --combat-runtime-smoke -logFile `"$log`""
$record=@{executable=$executable;arguments=$arguments;timeoutSeconds=60;startedUtc=[DateTime]::UtcNow.ToString('o');timedOut=$false;scope='standalone demonstration build; numerical startup/inference only'}
$watch=[Diagnostics.Stopwatch]::StartNew()
$job=Start-Process -FilePath $executable -ArgumentList $arguments -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru
$record.pid=$job.Id;$record | ConvertTo-Json | Set-Content -LiteralPath $recordPath
try{if(-not $job.WaitForExit(60000)){$record.timedOut=$true;Stop-Process -Id $job.Id -Force;$record.exitCode=$null}else{$job.WaitForExit();$job.Refresh();$record.exitCode=$job.ExitCode}}
finally{$watch.Stop();$record.wallSeconds=$watch.Elapsed.TotalSeconds;$record | ConvertTo-Json | Set-Content -LiteralPath $recordPath}
if($record.timedOut -or $record.exitCode -ne 0 -or -not (Select-String -LiteralPath $log -SimpleMatch 'RUNTIME_DEMO_SMOKE_PASS' -Quiet)){throw "Standalone check failed: $log"}
Write-Output "Standalone smoke PASS ($($record.wallSeconds) s): $log"

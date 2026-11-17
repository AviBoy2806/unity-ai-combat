param([string]$Method='RuntimeIntegrationValidation.Run', [string]$RunName='Runtime', [string]$Marker='RUNTIME_INTEGRATION_VALIDATION_PASS', [switch]$Quit, [switch]$MainProject, [ValidateRange(30,600)][int]$TimeoutSeconds=120, [switch]$Graphics)
$ErrorActionPreference='Stop'
$projectRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$logRoot=Join-Path $projectRoot 'Logs/RuntimeIntegration20261117'
New-Item -ItemType Directory -Force $logRoot | Out-Null
$recordPath=Join-Path $logRoot "$RunName.run.json"
if(Test-Path -LiteralPath $recordPath){throw 'Use a fresh RunName; preserve earlier evidence.'}
$targetRoot=if($MainProject){$projectRoot}else{Join-Path $projectRoot 'Tools/Phase5/Lab'}
if(-not $MainProject){$assets=Get-Item -LiteralPath (Join-Path $targetRoot 'Assets');if($assets.LinkType -ne 'Junction'){throw 'Expected existing Lab Assets junction; inspect before running.'}}
$unityExe='C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe'
$logPath=Join-Path $logRoot "$RunName.unity.log"
$arguments="-batchmode -nographics -projectPath `"$targetRoot`" -executeMethod $Method -logFile `"$logPath`""
if($Quit){$arguments+=' -quit'}
if($Graphics){$arguments=$arguments.Replace('-nographics','-screen-width 1600 -screen-height 900')}
$record=@{executable=$unityExe;arguments=$arguments;timeoutSeconds=$TimeoutSeconds;startedUtc=[DateTime]::UtcNow.ToString('o');timedOut=$false;scope=if($MainProject){'main project'}else{'Lab project with shared Assets junction, separate Library/settings'}}
$watch=[Diagnostics.Stopwatch]::StartNew()
$job=Start-Process -FilePath $unityExe -ArgumentList $arguments -WindowStyle Hidden -PassThru
$record.pid=$job.Id;$record | ConvertTo-Json | Set-Content -LiteralPath $recordPath
try{if(-not $job.WaitForExit($TimeoutSeconds*1000)){$record.timedOut=$true;Stop-Process -Id $job.Id -Force;$record.exitCode=$null}else{$job.WaitForExit();$job.Refresh();$record.exitCode=$job.ExitCode}}
finally{$watch.Stop();$record.wallSeconds=$watch.Elapsed.TotalSeconds;$record | ConvertTo-Json | Set-Content -LiteralPath $recordPath}
if($record.timedOut -or $record.exitCode -ne 0){throw "Bounded Unity check failed: $logPath"}
if(-not (Select-String -LiteralPath $logPath -SimpleMatch $Marker -Quiet)){throw "Missing marker $Marker"}
Write-Output "$RunName PASS ($($record.wallSeconds) seconds): $logPath"

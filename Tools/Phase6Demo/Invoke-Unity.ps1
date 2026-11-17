param([string]$Method='DemoExecutionDiagnosis.Run',[string]$RunName='diagnosis-before',[string]$Marker='DEMO_EXECUTION_DIAGNOSIS_COMPLETE',[string]$MarkerFile,[string]$TrainingBuildPath,[switch]$Quit,[switch]$Rendered,[ValidateRange(30,600)][int]$TimeoutSeconds=150)
$ErrorActionPreference='Stop'
$projectRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$logRoot=Join-Path $projectRoot 'Logs/DemoPolicy20261117'
$recordPath=Join-Path $logRoot "$RunName.run.json"
if(Test-Path -LiteralPath $recordPath){throw 'Use a fresh run name'}
$labRoot=Join-Path $projectRoot 'Tools/Phase5/Lab'
$logPath=Join-Path $logRoot "$RunName.unity.log"
$unityExe='C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe'
$env:DEMO_DIAGNOSIS_NAME=$RunName
if($TrainingBuildPath){$env:DEMO_TRAIN_BUILD_PATH=$TrainingBuildPath}else{Remove-Item Env:DEMO_TRAIN_BUILD_PATH -ErrorAction SilentlyContinue}
$arguments="-batchmode -projectPath `"$labRoot`" -executeMethod $Method -logFile `"$logPath`""
if(-not $Rendered){$arguments+=' -nographics'}
if($Quit){$arguments+=' -quit'}
$record=@{executable=$unityExe;arguments=$arguments;timeoutSeconds=$TimeoutSeconds;startedUtc=[DateTime]::UtcNow.ToString('o');timedOut=$false;training=$false}
$watch=[Diagnostics.Stopwatch]::StartNew()
$job=Start-Process $unityExe -ArgumentList $arguments -WindowStyle Hidden -PassThru
$record.pid=$job.Id;$record|ConvertTo-Json|Set-Content -LiteralPath $recordPath
try{if(-not $job.WaitForExit($TimeoutSeconds*1000)){$record.timedOut=$true;& taskkill /PID $job.Id /T /F | Out-Null;$record.exitCode=$null}else{$job.WaitForExit();$job.Refresh();$record.exitCode=$job.ExitCode}}
finally{$watch.Stop();$record.wallSeconds=$watch.Elapsed.TotalSeconds;$record|ConvertTo-Json|Set-Content -LiteralPath $recordPath}
if($record.timedOut -or $record.exitCode -ne 0){throw "Unity check failed: $logPath"}
if(-not (Select-String -LiteralPath $logPath -SimpleMatch $Marker -Quiet)){
 if(-not $MarkerFile){throw 'Required PASS/complete marker missing'}
 $artifact=Get-Item -LiteralPath (Join-Path $projectRoot $MarkerFile)
 if($artifact.LastWriteTimeUtc -lt [DateTime]::Parse($record.startedUtc).ToUniversalTime() -or -not (Select-String -LiteralPath $artifact.FullName -SimpleMatch $Marker -Quiet)){throw 'Fresh marker artifact missing'}
 $record.markerArtifact=$MarkerFile;$record|ConvertTo-Json|Set-Content -LiteralPath $recordPath
}
Write-Output "$RunName completed in $($record.wallSeconds)s"

$ErrorActionPreference='Stop'
. "$PSScriptRoot/Initialize-Environment.ps1"
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskMarkerPath=Join-Path $taskRoot '.cache/pilot-demo.json'
$taskStatePath=Join-Path $taskRoot '.cache/http-qa.json'
if(-not (Test-Path -LiteralPath $taskMarkerPath)){throw 'No owned fictional demo marker exists.'}
$taskMarker=Get-Content -LiteralPath $taskMarkerPath -Raw | ConvertFrom-Json
$taskState=Get-Content -LiteralPath $taskStatePath -Raw | ConvertFrom-Json
if($taskMarker.purpose -ne 'iteration-9-fictional-demo' -or $taskMarker.database -notmatch '^aulatutor_http_[a-f0-9]{32}$' -or
    $taskMarker.database -ne $taskState.database -or $taskMarker.apiProcessId -ne $taskState.processId){throw 'Unexpected demo ownership; cleanup stopped.'}
$taskApi=Get-CimInstance Win32_Process -Filter "ProcessId=$($taskState.processId)"
$taskWeb=Get-CimInstance Win32_Process -Filter "ProcessId=$($taskMarker.webProcessId)"
$taskDll=Join-Path $taskRoot 'src/AulaTutor.Api/bin/Release/net10.0/AulaTutor.Api.dll'
$taskCli=Join-Path $taskRoot 'src/AulaTutor.Web/node_modules/@angular/cli/bin/ng.js'
if($taskApi -and $taskApi.CommandLine -notlike "*$taskDll*"){throw 'API PID has been reused; cleanup stopped.'}
if($taskWeb -and $taskWeb.CommandLine.Replace('\','/') -notlike "*$($taskCli.Replace('\','/'))*"){throw 'Web PID has been reused; cleanup stopped.'}
if($taskWeb){Stop-Process -Id $taskWeb.ProcessId}
if($taskApi){Stop-Process -Id $taskApi.ProcessId}
. "$PSScriptRoot/Load-LocalConfiguration.ps1"
docker compose --env-file "$taskRoot/.env" -f "$taskRoot/deploy/compose.yaml" exec -T postgres psql -U $localValues['POSTGRES_USER'] -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE IF EXISTS `"$($taskState.database)`" WITH (FORCE);"
if($LASTEXITCODE -ne 0){throw 'Demo database cleanup failed; private state retained for recovery.'}
foreach($taskFile in @($taskStatePath,$taskMarkerPath,(Join-Path $taskRoot '.cache/pilot-scenario.json'))){if(Test-Path -LiteralPath $taskFile){Remove-Item -LiteralPath $taskFile}}
Write-Output 'Only the isolated fictional demo database, owned processes and private demo state were removed. Main database untouched.'

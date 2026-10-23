param([string]$PlaywrightModule=$env:AULATUTOR_PLAYWRIGHT_MODULE)
$ErrorActionPreference='Stop'
. "$PSScriptRoot/Initialize-Environment.ps1"
$taskRoot=Split-Path -Parent $PSScriptRoot
if(-not $PlaywrightModule){throw 'Provide the installed Playwright module path; this script never downloads dependencies.'}
$taskStatePath=Join-Path $taskRoot '.cache/http-qa.json'
if(Test-Path -LiteralPath $taskStatePath){throw 'An isolated HTTP QA environment already exists. Resolve its owned process/database before starting another.'}
foreach($taskPort in 4200,5080,7080){if(Get-NetTCPConnection -State Listen -LocalPort $taskPort -ErrorAction SilentlyContinue){throw 'Stop only the local project API/web before E2E.'}}
$taskWeb=$null
$taskOriginalModule=$env:AULATUTOR_PLAYWRIGHT_MODULE
$taskOriginalProvider=$env:Ai__Provider; $taskOriginalGroq=$env:Ai__Groq__ApiKey; $taskOriginalGemini=$env:Ai__Gemini__ApiKey
@{status='preparing';checks=@()} | ConvertTo-Json | Set-Content "$taskRoot/artifacts/iteration-8-e2e.json" -Encoding utf8
try {
    & "$PSScriptRoot/Prepare-Local.ps1"
    & "$PSScriptRoot/Http-Identity.ps1" -KeepRunning
    $taskCli=Join-Path $taskRoot 'src/AulaTutor.Web/node_modules/@angular/cli/bin/ng.js'
    $taskWeb=Start-Process -FilePath (Get-Command node).Source -ArgumentList @($taskCli,'serve','--host','localhost','--port','4200','--ssl','--ssl-cert','../../.cache/certs/localhost.pem','--ssl-key','../../.cache/certs/localhost.key','--proxy-config','proxy.conf.json') -WorkingDirectory "$taskRoot/src/AulaTutor.Web" -WindowStyle Hidden -PassThru -RedirectStandardOutput "$taskRoot/.cache/e2e-web.log" -RedirectStandardError "$taskRoot/.cache/e2e-web-error.log"
    $taskReady=$false
    for($taskAttempt=0;$taskAttempt -lt 90;$taskAttempt++){
        if($taskWeb.HasExited){throw 'E2E web process exited.'}
        try{$taskHealth=Invoke-RestMethod 'https://localhost:4200/api/health' -SkipCertificateCheck -TimeoutSec 2; if($taskHealth.aiProvider -ne 'Mock'){throw 'E2E requires Mock.'}; $taskReady=$true;break}catch{Start-Sleep -Milliseconds 500}
    }
    if(-not $taskReady){throw 'E2E web/proxy not ready.'}
    $env:AULATUTOR_PLAYWRIGHT_MODULE=$PlaywrightModule
    node "$taskRoot/tests/AulaTutor.E2E/run.mjs"
    if($LASTEXITCODE -ne 0){throw 'Browser E2E failed; see the safe report in artifacts.'}
    & "$PSScriptRoot/Test-RestartRecovery.ps1"
    Write-Output 'E2E suite passed: HTTPS journeys, browser, staged errors and real backend restart.'
} finally {
    if($taskWeb -and -not $taskWeb.HasExited){Stop-Process -Id $taskWeb.Id}
    if(Test-Path -LiteralPath $taskStatePath){
        . "$PSScriptRoot/Load-LocalConfiguration.ps1"
        $taskState=Get-Content -LiteralPath $taskStatePath -Raw | ConvertFrom-Json
        if($taskState.database -notmatch '^aulatutor_http_[a-f0-9]{32}$'){throw 'Unexpected QA database name; automatic cleanup stopped.'}
        $taskApi=Get-CimInstance Win32_Process -Filter "ProcessId=$($taskState.processId)"
        $taskDll=Join-Path $taskRoot 'src/AulaTutor.Api/bin/Release/net10.0/AulaTutor.Api.dll'
        if($taskApi){if($taskApi.CommandLine -notlike "*$taskDll*"){throw 'Unexpected QA process; cleanup stopped.'};Stop-Process -Id $taskState.processId}
        docker compose --env-file "$taskRoot/.env" -f "$taskRoot/deploy/compose.yaml" exec -T postgres psql -U $localValues['POSTGRES_USER'] -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE `"$($taskState.database)`" WITH (FORCE);"
        if($LASTEXITCODE -ne 0){throw 'Could not drop isolated E2E database.'}
        Remove-Item -LiteralPath $taskStatePath
    }
    $env:AULATUTOR_PLAYWRIGHT_MODULE=$taskOriginalModule; $env:Ai__Provider=$taskOriginalProvider
    $env:Ai__Groq__ApiKey=$taskOriginalGroq; $env:Ai__Gemini__ApiKey=$taskOriginalGemini
}

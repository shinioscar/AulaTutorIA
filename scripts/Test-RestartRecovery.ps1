$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Initialize-Environment.ps1"
. "$PSScriptRoot/Load-LocalConfiguration.ps1"
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskStatePath = Join-Path $taskRoot '.cache/http-qa.json'
$taskState = Get-Content -LiteralPath $taskStatePath -Raw | ConvertFrom-Json
if ($taskState.database -notmatch '^aulatutor_http_[a-f0-9]{32}$') { throw 'Only an isolated HTTP QA database is allowed.' }
$taskDll = Join-Path $taskRoot 'src/AulaTutor.Api/bin/Release/net10.0/AulaTutor.Api.dll'
$taskOwned = Get-CimInstance Win32_Process -Filter "ProcessId=$($taskState.processId)"
if (-not $taskOwned -or $taskOwned.CommandLine -notlike "*$taskDll*") { throw 'The recorded process is not the owned QA API.' }
$taskBase = 'https://localhost:7080'
$taskHealth = Invoke-RestMethod "$taskBase/api/health" -SkipCertificateCheck
if ($taskHealth.aiProvider -ne 'Mock') { throw 'Restart verification requires Mock.' }
$taskSession = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
function Invoke-Recovery([string]$method, [string]$route, $body = $null) {
    $taskCsrf = Invoke-RestMethod "$taskBase/api/auth/csrf" -WebSession $taskSession -SkipCertificateCheck
    $taskArgs = @{ Uri=$taskBase+$route; Method=$method; WebSession=$taskSession; SkipCertificateCheck=$true; SkipHttpErrorCheck=$true }
    if ($method -ne 'GET') { $taskArgs.Headers=@{'X-XSRF-TOKEN'=$taskCsrf.requestToken}; $taskArgs.ContentType='application/json'; $taskArgs.Body=$body | ConvertTo-Json -Depth 10 }
    $taskReply = Invoke-WebRequest @taskArgs
    if ([int]$taskReply.StatusCode -notin @(200,201,204)) { throw "Recovery request failed: HTTP $([int]$taskReply.StatusCode)." }
    if ($taskReply.Content) { return $taskReply.Content | ConvertFrom-Json }
}
$null = Invoke-Recovery POST '/api/auth/login' @{email=$taskState.studentEmail;password=$taskState.password}
$taskBefore = Invoke-Recovery GET "/api/tutoring-sessions/$($taskState.completedTutoringId)"
$taskCardsReport = Get-Content "$taskRoot/artifacts/ia-2-e2e.json" -Raw | ConvertFrom-Json
if($taskCardsReport.status -ne 'passed' -or $taskCardsReport.preloadedSessionId -notmatch '^[a-f0-9-]{36}$'){throw 'The passed preloaded E2E session is required for restart verification.'}
$taskCardsBefore = Invoke-Recovery GET "/api/tutoring-sessions/$($taskCardsReport.preloadedSessionId)"
# Respect the retained QA account's previous rate window; never weaken server policy.
Start-Sleep -Seconds 60
$taskNew = Invoke-Recovery POST '/api/tutoring-sessions/' @{ requestId=[Guid]::NewGuid().ToString(); topic='Reinicio ficticio'; goal='Recuperar sin duplicar'; level='Beginner';directReview=$true }
$taskRequest = @{ requestId=[Guid]::NewGuid().ToString();revision=$taskNew.revision;action='Start';text=$null;retry=$false }
$taskStarted = Invoke-Recovery POST "/api/tutoring-sessions/$($taskNew.id)/messages" $taskRequest
Stop-Process -Id $taskState.processId
$env:Ai__Provider='Mock'; $env:Ai__Groq__ApiKey=$null; $env:Ai__Gemini__ApiKey=$null
$env:ConnectionStrings__AulaTutor += ';Database=' + $taskState.database
$env:ASPNETCORE_ENVIRONMENT='Development'; $env:Logging__LogLevel__Default='Warning'
$taskApi = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @($taskDll,'--urls','https://localhost:7080;http://127.0.0.1:5080') -WorkingDirectory $taskRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput "$taskRoot/.cache/restart-qa.log" -RedirectStandardError "$taskRoot/.cache/restart-qa-error.log"
$taskState.processId = $taskApi.Id
$taskState | ConvertTo-Json | Set-Content -LiteralPath $taskStatePath -Encoding utf8
$taskReady=$false
for($taskAttempt=0;$taskAttempt -lt 60;$taskAttempt++) {
    if($taskApi.HasExited){throw 'QA API failed to restart.'}
    try { $taskHealth=Invoke-RestMethod "$taskBase/api/health" -SkipCertificateCheck -TimeoutSec 2; $taskReady=$true; break } catch { Start-Sleep -Milliseconds 500 }
}
if(-not $taskReady -or $taskHealth.aiProvider -ne 'Mock'){throw 'Restarted Mock API was not ready.'}
$taskAfter = Invoke-Recovery GET "/api/tutoring-sessions/$($taskState.completedTutoringId)"
$taskCardsAfter = Invoke-Recovery GET "/api/tutoring-sessions/$($taskCardsReport.preloadedSessionId)"
if($taskCardsAfter.policyVersion -ne 'preloaded-class-v1' -or $taskCardsAfter.state -ne 'Completed' -or
    ($taskCardsBefore.learning | ConvertTo-Json -Depth 32 -Compress) -ne ($taskCardsAfter.learning | ConvertTo-Json -Depth 32 -Compress) -or
    ($taskCardsBefore.turns | ConvertTo-Json -Depth 32 -Compress) -ne ($taskCardsAfter.turns | ConvertTo-Json -Depth 32 -Compress)) {throw 'Preloaded attempts, help/evaluation turns or reports changed across restart.'}
if($taskAfter.messages.Count -ne $taskBefore.messages.Count -or $taskAfter.turns.Count -ne $taskBefore.turns.Count -or $taskAfter.summary -ne $taskBefore.summary){throw 'Committed history changed across restart.'}
$taskReplay = Invoke-Recovery POST "/api/tutoring-sessions/$($taskNew.id)/messages" $taskRequest
if($taskReplay.turns.Count -ne 1 -or $taskReplay.messages.Count -ne $taskStarted.messages.Count){throw 'Replay duplicated the durable turn after restart.'}
@{status='passed';provider='Mock';cookieSurvived=$true;historyPreserved=$true;preloadedEvidencePreserved=$true;replayedTurns=1;duplicateTurns=0;database='isolated-http'} | ConvertTo-Json | Set-Content "$taskRoot/artifacts/ia-2-restart.json" -Encoding utf8
Write-Output 'Controlled API restart passed: cookie, committed history and idempotent replay preserved; Mock only.'

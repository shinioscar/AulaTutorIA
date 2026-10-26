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
$taskByokReport = Get-Content "$taskRoot/artifacts/byok-e2e.json" -Raw | ConvertFrom-Json
if($taskByokReport.status -ne 'passed' -or $taskByokReport.studentSessionId -notmatch '^[a-f0-9-]{36}$'){throw 'Passed isolated BYOK E2E is required for custody recovery.'}
$taskByokBefore = Invoke-Recovery GET "/api/tutoring-sessions/$($taskByokReport.studentSessionId)"
$taskConnectionsBefore = Invoke-Recovery GET '/api/ai-connections/'
$taskLearnReport=Get-Content "$taskRoot/artifacts/learn-1-e2e.json" -Raw | ConvertFrom-Json
if($taskLearnReport.status -ne 'passed' -or $taskLearnReport.pausedGoalId -notmatch '^[a-f0-9-]{36}$'){throw 'Passed LEARN-1 isolated E2E required for prepared recovery.'}
$taskLearnBefore=Invoke-Recovery GET "/api/tutoring-sessions/$($taskLearnReport.pausedGoalId)"
$taskCardsReport = Get-Content "$taskRoot/artifacts/iteration-9-e2e.json" -Raw | ConvertFrom-Json
if($taskCardsReport.status -ne 'passed' -or $taskCardsReport.preloadedSessionId -notmatch '^[a-f0-9-]{36}$'){throw 'The passed preloaded E2E session is required for restart verification.'}
$taskCardsBefore = Invoke-Recovery GET "/api/tutoring-sessions/$($taskCardsReport.preloadedSessionId)"
$taskAcademicBefore=@{}
foreach($taskId in $taskCardsReport.pilot.sessions){
    if($taskId -notmatch '^[a-f0-9-]{36}$'){throw 'Invalid academic recovery target.'}
    # Student B has a separate owner cookie; the owner is selected from the private fixture.
    $null=Invoke-Recovery POST '/api/auth/login' @{email=$(if($taskAcademicBefore.Count -eq 0){$taskState.studentEmail}else{$taskState.tutoringOwnerEmail});password=$taskState.password}
    $taskAcademicBefore[$taskId]=Invoke-Recovery GET "/api/tutoring-sessions/$taskId"
}
$null=Invoke-Recovery POST '/api/auth/login' @{email=$taskState.studentEmail;password=$taskState.password}
# Respect the retained QA account's previous rate window; never weaken server policy.
Start-Sleep -Seconds 60
$taskNew = Invoke-Recovery POST '/api/tutoring-sessions/' @{ requestId=[Guid]::NewGuid().ToString(); topic='Reinicio ficticio'; goal='Recuperar sin duplicar'; level='Beginner';directReview=$true }
$taskRequest = @{ requestId=[Guid]::NewGuid().ToString();revision=$taskNew.revision;action='Start';text=$null;retry=$false }
$taskStarted = Invoke-Recovery POST "/api/tutoring-sessions/$($taskNew.id)/messages" $taskRequest
Stop-Process -Id $taskState.processId
$env:Ai__Provider='Mock'; $env:Ai__Groq__ApiKey=$null; $env:Ai__Gemini__ApiKey=$null
$env:ConnectionStrings__AulaTutor += ';Database=' + $taskState.database
$env:ASPNETCORE_ENVIRONMENT='Development'; $env:Logging__LogLevel__Default='Warning'
$taskPreviousPersonalSimulation=$env:PersonalAi__QaSimulation; $taskPreviousExternalPolicy=$env:AiQuota__AllowExternalCalls
if($taskState.personalAiSimulation){$env:PersonalAi__QaSimulation='true';$env:AiQuota__AllowExternalCalls='false'}
$taskApi = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @($taskDll,'--urls','https://localhost:7080;http://127.0.0.1:5080') -WorkingDirectory $taskRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput "$taskRoot/.cache/restart-qa.log" -RedirectStandardError "$taskRoot/.cache/restart-qa-error.log"
$env:PersonalAi__QaSimulation=$taskPreviousPersonalSimulation; $env:AiQuota__AllowExternalCalls=$taskPreviousExternalPolicy
$taskState.processId = $taskApi.Id
$taskState | ConvertTo-Json | Set-Content -LiteralPath $taskStatePath -Encoding utf8
$taskReady=$false
for($taskAttempt=0;$taskAttempt -lt 60;$taskAttempt++) {
    if($taskApi.HasExited){throw 'QA API failed to restart.'}
    try { $taskHealth=Invoke-RestMethod "$taskBase/api/health" -SkipCertificateCheck -TimeoutSec 2; $taskReady=$true; break } catch { Start-Sleep -Milliseconds 500 }
}
if(-not $taskReady -or $taskHealth.aiProvider -ne 'Mock'){throw 'Restarted Mock API was not ready.'}
$taskAfter = Invoke-Recovery GET "/api/tutoring-sessions/$($taskState.completedTutoringId)"
$taskConnectionsAfter = Invoke-Recovery GET '/api/ai-connections/'
$taskLearnAfter=Invoke-Recovery GET "/api/tutoring-sessions/$($taskLearnReport.pausedGoalId)"
if(($taskLearnBefore | ConvertTo-Json -Depth 32 -Compress) -ne ($taskLearnAfter | ConvertTo-Json -Depth 32 -Compress)){throw 'Prepared script/progress/evidence changed after restart.'}
$taskLearnReplay=Invoke-Recovery POST "/api/tutoring-sessions/$($taskLearnAfter.id)/cards" @{requestId=$taskLearnReport.pausedGoalHelpKey;revision=$taskLearnAfter.revision;action='Hint'}
if($taskLearnReplay.turns.Count -ne $taskLearnAfter.turns.Count){throw 'Prepared help replay duplicated evidence after restart.'}
$taskLearnResumed=Invoke-Recovery POST "/api/tutoring-sessions/$($taskLearnAfter.id)/resume" @{revision=$taskLearnAfter.revision}
if($taskLearnResumed.state -ne 'Active' -or $taskLearnResumed.prepared.contentHash -ne $taskLearnBefore.prepared.contentHash){throw 'Prepared resume did not preserve its immutable script.'}
@{status='passed';scriptPreserved=$true;evidencePreserved=$true;helpReplayDuplicates=0;resumedWithoutInference=$true} | ConvertTo-Json | Set-Content "$taskRoot/artifacts/learn-1-restart.json" -Encoding utf8
if(($taskConnectionsBefore | ConvertTo-Json -Depth 16 -Compress) -ne ($taskConnectionsAfter | ConvertTo-Json -Depth 16 -Compress)){throw 'Own connection metadata changed across restart.'}
$taskByokAfter = Invoke-Recovery GET "/api/tutoring-sessions/$($taskByokReport.studentSessionId)"
if(($taskByokBefore | ConvertTo-Json -Depth 32 -Compress) -ne ($taskByokAfter | ConvertTo-Json -Depth 32 -Compress)){throw 'Personal session history changed across restart.'}
$taskByokRequest = @{requestId=[Guid]::NewGuid().ToString();revision=$taskByokAfter.revision;action='Hint'}
$taskByokContinued = Invoke-Recovery POST "/api/tutoring-sessions/$($taskByokAfter.id)/messages" $taskByokRequest
if($taskByokContinued.turns[-1].state -ne 'Succeeded' -or $taskByokContinued.messages[-1].providerId -ne 'OpenRouter' -or $null -ne $taskByokContinued.messages[-1].usage){throw 'The durable personal credential could not resume its pinned session after restart.'}
$taskByokReplay = Invoke-Recovery POST "/api/tutoring-sessions/$($taskByokAfter.id)/messages" $taskByokRequest
if($taskByokReplay.turns.Count -ne $taskByokContinued.turns.Count -or $taskByokReplay.messages.Count -ne $taskByokContinued.messages.Count){throw 'Personal replay duplicated history after restart.'}
@{status='passed';metadataPreserved=$true;historyPreserved=$true;credentialDecryptedAfterRestart=$true;provider='OpenRouter';transport='isolated-simulation';duplicateTurns=0} | ConvertTo-Json | Set-Content "$taskRoot/artifacts/byok-restart.json" -Encoding utf8
$taskCardsAfter = Invoke-Recovery GET "/api/tutoring-sessions/$($taskCardsReport.preloadedSessionId)"
if($taskCardsAfter.policyVersion -ne 'preloaded-class-v1' -or $taskCardsAfter.state -ne 'Completed' -or
    ($taskCardsBefore.learning | ConvertTo-Json -Depth 32 -Compress) -ne ($taskCardsAfter.learning | ConvertTo-Json -Depth 32 -Compress) -or
    ($taskCardsBefore.turns | ConvertTo-Json -Depth 32 -Compress) -ne ($taskCardsAfter.turns | ConvertTo-Json -Depth 32 -Compress)) {throw 'Preloaded attempts, help/evaluation turns or reports changed across restart.'}
if($taskAfter.messages.Count -ne $taskBefore.messages.Count -or $taskAfter.turns.Count -ne $taskBefore.turns.Count -or $taskAfter.summary -ne $taskBefore.summary){throw 'Committed history changed across restart.'}
$taskReplay = Invoke-Recovery POST "/api/tutoring-sessions/$($taskNew.id)/messages" $taskRequest
if($taskReplay.turns.Count -ne 1 -or $taskReplay.messages.Count -ne $taskStarted.messages.Count){throw 'Replay duplicated the durable turn after restart.'}
$taskAcademicCount=0
foreach($taskId in $taskCardsReport.pilot.sessions){
    $null=Invoke-Recovery POST '/api/auth/login' @{email=$(if($taskAcademicCount -eq 0){$taskState.studentEmail}else{$taskState.tutoringOwnerEmail});password=$taskState.password}
    $taskAcademicAfter=Invoke-Recovery GET "/api/tutoring-sessions/$taskId"
    if($taskAcademicAfter.state -ne 'Completed' -or
        ($taskAcademicBefore[$taskId].learning | ConvertTo-Json -Depth 32 -Compress) -ne ($taskAcademicAfter.learning | ConvertTo-Json -Depth 32 -Compress) -or
        ($taskAcademicBefore[$taskId].turns | ConvertTo-Json -Depth 32 -Compress) -ne ($taskAcademicAfter.turns | ConvertTo-Json -Depth 32 -Compress)){throw 'Academic SQL evidence changed across restart.'}
    $taskAcademicCount++
}
if($taskAcademicCount -ne 2){throw 'Two independent academic recovery targets required.'}
@{status='passed';provider='Mock';cookieSurvived=$true;historyPreserved=$true;preloadedEvidencePreserved=$true;academicSessionsPreserved=$taskAcademicCount;replayedTurns=1;duplicateTurns=0;database='isolated-http'} | ConvertTo-Json | Set-Content "$taskRoot/artifacts/iteration-9-restart.json" -Encoding utf8
Write-Output 'Controlled API restart passed: cookie, committed history and idempotent replay preserved; Mock only.'

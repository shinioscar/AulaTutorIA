param([switch]$AllowRealCalls, [switch]$KeepRunning, [switch]$StudentOnly)
$ErrorActionPreference = 'Stop'
if (-not $AllowRealCalls) { throw 'Real quota is opt-in. Supply -AllowRealCalls only after explicit authorization.' }
. "$PSScriptRoot/Initialize-Environment.ps1"
. "$PSScriptRoot/Load-LocalConfiguration.ps1"
if ($env:Ai__Provider -ne 'Groq' -or -not $env:Ai__Groq__ApiKey) { throw 'Configure Groq privately first.' }
$probeRoot = Split-Path -Parent $PSScriptRoot
$probeDir = Join-Path $probeRoot 'artifacts/groq-probe'
New-Item -ItemType Directory -Path $probeDir -Force | Out-Null
$probeStatePath = Join-Path $probeRoot '.cache/groq-qa.json'
if (Test-Path -LiteralPath $probeStatePath) { throw 'Clean the previous isolated probe before starting another.' }
foreach ($port in 5080,7080) { if (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue) { throw "Stop the project API before probing (port $port occupied)." } }
$probeDb = 'aulatutor_http_' + [Guid]::NewGuid().ToString('N')
$probePassword = 'Aa1!' + [Guid]::NewGuid().ToString('N')
$probeConnection = $env:ConnectionStrings__AulaTutor
$probeRetries = $env:Ai__MaxRetries
$probeApi = $null; $probeCreated = $false; $probeSuccess = $false
$probeDll = Join-Path $probeRoot 'src/AulaTutor.Api/bin/Release/net10.0/AulaTutor.Api.dll'
$probeCalls = 0; $probeLastCall = [DateTimeOffset]::UtcNow
$probeResults = [System.Collections.Generic.List[object]]::new()
function New-ProbeSession { @{Web=[Microsoft.PowerShell.Commands.WebRequestSession]::new();Token=$null} }
function Request-Probe($session,[string]$method,[string]$path,$body=$null,[int]$status=200) {
    $argsHttp=@{Uri="https://localhost:7080$path";Method=$method;WebSession=$session.Web;SkipCertificateCheck=$true;SkipHttpErrorCheck=$true;TimeoutSec=45}
    if($method -ne 'GET'){$argsHttp.Headers=@{'X-XSRF-TOKEN'=$session.Token}}
    if($null -ne $body){$argsHttp.ContentType='application/json';$argsHttp.Body=ConvertTo-Json -InputObject $body -Depth 16}
    $reply=Invoke-WebRequest @argsHttp
    if([int]$reply.StatusCode -ne $status){throw "Probe HTTP $method $path failed with $($reply.StatusCode); no automatic retry."}
    if($reply.Content){$reply.Content|ConvertFrom-Json}
}
function Csrf-Probe($session){$session.Token=(Request-Probe $session GET '/api/auth/csrf').requestToken}
function Login-Probe($session,[string]$email){Csrf-Probe $session;Request-Probe $session POST '/api/auth/login' @{email=$email;password=$probePassword} 204;Csrf-Probe $session}
function Pace-Probe {
    if($script:probeCalls -ge 10){throw 'Probe quota cap reached.'}
    if($script:probeLastCall){$remaining=65-([DateTimeOffset]::UtcNow-$script:probeLastCall).TotalSeconds;if($remaining -gt 0){Start-Sleep -Milliseconds ([int][Math]::Ceiling($remaining*1000))}}
    $script:probeLastCall=[DateTimeOffset]::UtcNow;$script:probeCalls++
}
function Turn-Probe($session,$state,[string]$action,[string]$text='',[string]$exerciseId='') {
    Pace-Probe
    $key=[Guid]::NewGuid().ToString()
    $next=Request-Probe $session POST "/api/tutoring-sessions/$($state.id)/messages" @{requestId=$key;revision=$state.revision;action=$action;text=$(if($text){$text}else{$null});exerciseId=$(if($exerciseId){$exerciseId}else{$null});retry=$false}
    $turn=$next.turns|Where-Object requestId -eq $key
    $record=@{action=$action;state=$turn.state;errorCode=$turn.errorCode;call=$script:probeCalls;at=$script:probeLastCall.ToString('O')}
    $script:probeResults.Add($record);$script:probeResults|ConvertTo-Json -Depth 5|Set-Content "$probeDir/results.json" -Encoding utf8
    Write-Host "Groq probe $action : $($turn.state)"
    if($turn.state -ne 'Succeeded'){throw "Groq $action failed ($($turn.errorCode)); stopped, no retry. RetryNotBefore=$($turn.retryNotBefore)"}
    if($next.pendingRequestId){throw 'Unexpected pending turn.'}
    $next
}
try {
    $env:Ai__MaxRetries='0';$env:ASPNETCORE_ENVIRONMENT='Development';$env:Logging__LogLevel__Default='Warning'
    docker compose --env-file "$probeRoot/.env" -f "$probeRoot/deploy/compose.yaml" exec -T postgres psql -U $localValues['POSTGRES_USER'] -d postgres -c "CREATE DATABASE `"$probeDb`";" | Out-Null
    if($LASTEXITCODE -ne 0){throw 'Could not create isolated probe database.'};$probeCreated=$true
    $env:ConnectionStrings__AulaTutor=$probeConnection+';Database='+$probeDb
    $env:BootstrapAdmin__Email='admin@example.invalid';$env:BootstrapAdmin__Password=$probePassword;$env:BootstrapAdmin__DisplayName='Administración ficticia'
    dotnet $probeDll --migrate --bootstrap-admin *> "$probeDir/maintenance.log"
    if($LASTEXITCODE -ne 0){throw 'Isolated migration/bootstrap failed.'}
    Remove-Item Env:BootstrapAdmin__Email,Env:BootstrapAdmin__Password,Env:BootstrapAdmin__DisplayName
    $probeApi=Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @($probeDll,'--urls','https://localhost:7080;http://127.0.0.1:5080') -WorkingDirectory "$probeRoot/src/AulaTutor.Api" -WindowStyle Hidden -PassThru -RedirectStandardOutput "$probeDir/api.log" -RedirectStandardError "$probeDir/api-error.log"
    for($readyAttempt=0;$readyAttempt -lt 60;$readyAttempt++) {try {Invoke-WebRequest 'https://localhost:7080/api/health' -SkipCertificateCheck -TimeoutSec 2|Out-Null;break}catch{Start-Sleep -Milliseconds 500}}
    @{database=$probeDb;processId=$probeApi.Id;processStartedAt=$probeApi.StartTime.ToUniversalTime().ToString('O');password=$probePassword;adminEmail='admin@example.invalid';teacherEmail='teacher@example.invalid';studentEmail='student@example.invalid'}|ConvertTo-Json|Set-Content $probeStatePath -Encoding utf8
    $admin=New-ProbeSession;Login-Probe $admin 'admin@example.invalid'
    foreach($role in 'Teacher','Student') {
        $register=New-ProbeSession;Csrf-Probe $register
        Request-Probe $register POST '/api/auth/register' @{email="$($role.ToLowerInvariant())@example.invalid";password=$probePassword;displayName="$role ficticio";requestedRole=$role} 202 | Out-Null
    }
    $pending=(Request-Probe $admin GET '/api/admin/users?email=teacher%40example.invalid').items[0]
    Request-Probe $admin POST "/api/admin/users/$($pending.id)/teacher-decision" @{approve=$true} 204
    $teacher=New-ProbeSession;Login-Probe $teacher 'teacher@example.invalid'
    $student=New-ProbeSession;Login-Probe $student 'student@example.invalid'
    $lesson=$null
    if(-not $StudentOnly) {
    $content=@{title='Introducción a procedimientos almacenados en SQL Server';subject='SQL Server';description='Clase ficticia';prerequisites='SELECT';level='Beginner';durationMinutes=40;methodology='Práctica guiada';instructions='Datos ficticios';objectives=@(@{id='o1';text='Crear procedimientos con parámetros y usar IF EXISTS'});sections=@()}
    $lesson=Request-Probe $teacher POST '/api/lessons/' @{requestId=[Guid]::NewGuid().ToString();content=$content} 201
    Pace-Probe;$designKey=[Guid]::NewGuid().ToString()
    $design=@{requestId=$designKey;revision=$lesson.revision;action='Exercises';text='Diseña una clase breve de 40 minutos con 3 secciones y 3 ejercicios, sobre procedimientos con parámetros e IF EXISTS.';retry=$false}
    $lesson=Request-Probe $teacher POST "/api/lessons/$($lesson.id)/design" $design
    $operation=$lesson.operations|Where-Object requestId -eq $designKey
    $probeResults.Add(@{action='LessonExercises';state=$operation.state;errorCode=$operation.errorCode;call=$probeCalls;at=$probeLastCall.ToString('O')})
    Write-Host "Groq probe LessonExercises : $($operation.state)"
    if($operation.state -ne 'Succeeded' -or $lesson.proposals.Count -ne 1){throw "Class design failed ($($operation.errorCode)); stopped."}
    $replay=Request-Probe $teacher POST "/api/lessons/$($lesson.id)/design" $design
    if(($replay.operations|Where-Object requestId -eq $designKey).attempts -ne 1){throw 'Replay generated again.'}
    $lesson=Request-Probe $teacher POST "/api/lessons/$($lesson.id)/apply-proposal" @{requestId=[Guid]::NewGuid().ToString();revision=$lesson.revision;targetId=$lesson.proposals[0].id}
    if($lesson.validation.errors.Count){throw 'Generated class did not pass server review.'}
    }
    $s=Request-Probe $student POST '/api/tutoring-sessions/' @{requestId=[Guid]::NewGuid().ToString();topic='Procedimientos almacenados en SQL Server';goal='Comprender parámetros e IF EXISTS';level='Beginner';directReview=$false} 201
    $s=Turn-Probe $student $s 'DiagnosticQuestion'
    $exercise=$s.learning.exercises[-1]
    $answer=if($exercise.options.Count){$exercise.options[0]}else{'Un procedimiento almacenado agrupa instrucciones SQL. Un parámetro aporta un valor y IF EXISTS comprueba si una consulta devuelve filas.'}
    $s=Turn-Probe $student $s 'SubmitAttempt' $answer $exercise.id
    $s=Turn-Probe $student $s 'AnalyzeDiagnosis'
    $s=Turn-Probe $student $s 'GeneratePlan'
    $s=Turn-Probe $student $s 'ActivatePlan'
    $s=Turn-Probe $student $s 'Hint'
    $s=Turn-Probe $student $s 'Simplify'
    $s=Request-Probe $student POST "/api/tutoring-sessions/$($s.id)/complete" @{revision=$s.revision}
    $s=Turn-Probe $student $s 'FinalReport'
    $state=Get-Content $probeStatePath -Raw|ConvertFrom-Json
    $state|Add-Member lessonId $lesson.id;$state|Add-Member tutoringId $s.id
    $state|ConvertTo-Json|Set-Content $probeStatePath -Encoding utf8
    $probeSuccess=$true
    Write-Host "Groq probe completed: $probeCalls real calls, at least 65 seconds apart."
} finally {
    $probeResults|ConvertTo-Json -Depth 5|Set-Content "$probeDir/results.json" -Encoding utf8
    $env:ConnectionStrings__AulaTutor=$probeConnection;$env:Ai__MaxRetries=$probeRetries
    Remove-Item Env:BootstrapAdmin__Email,Env:BootstrapAdmin__Password,Env:BootstrapAdmin__DisplayName -ErrorAction SilentlyContinue
    if(-not($KeepRunning -and $probeApi -and (Test-Path -LiteralPath $probeStatePath))) {
        if($probeApi -and -not $probeApi.HasExited){Stop-Process -Id $probeApi.Id -Force}
        if($probeCreated -and $probeDb -match '^aulatutor_http_[a-f0-9]{32}$') {docker compose --env-file "$probeRoot/.env" -f "$probeRoot/deploy/compose.yaml" exec -T postgres psql -U $localValues['POSTGRES_USER'] -d postgres -c "DROP DATABASE `"$probeDb`" WITH (FORCE);"|Out-Null}
        Remove-Item -LiteralPath $probeStatePath -ErrorAction SilentlyContinue
    }
}

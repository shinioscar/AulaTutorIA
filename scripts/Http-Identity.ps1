param([switch]$KeepRunning)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Initialize-Environment.ps1"
. "$PSScriptRoot/Load-LocalConfiguration.ps1"
$projectRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $projectRoot 'artifacts/http-identity'
New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null
$databaseName = 'aulatutor_http_' + [Guid]::NewGuid().ToString('N')
$apiProcess = $null
$createdDatabase = $false
$success = $false
$originalConnection = $env:ConnectionStrings__AulaTutor
$originalProvider = $env:Ai__Provider
$originalGroqKey = $env:Ai__Groq__ApiKey
$originalGeminiKey = $env:Ai__Gemini__ApiKey
$apiDll = Join-Path $projectRoot 'src/AulaTutor.Api/bin/Release/net10.0/AulaTutor.Api.dll'
$apiRoot = 'https://localhost:7080'
$qaPassword = 'Aa1!' + [Guid]::NewGuid().ToString('N')
$checks = [System.Collections.Generic.List[string]]::new()

function New-ApiSession { @{ Web = [Microsoft.PowerShell.Commands.WebRequestSession]::new(); Token = $null } }
function Invoke-Api($session, [string]$method, [string]$path, $body = $null, [switch]$WithoutCsrf) {
    # PowerShell persists supplied headers in WebRequestSession; clear before negative checks.
    $session.Web.Headers.Remove('X-XSRF-TOKEN')
    $parameters = @{ Uri = "$apiRoot$path"; Method = $method; WebSession = $session.Web; SkipCertificateCheck = $true; SkipHttpErrorCheck = $true }
    if ($method -ne 'GET' -and -not $WithoutCsrf) { $parameters.Headers = @{ 'X-XSRF-TOKEN' = $session.Token } }
    if ($null -ne $body) { $parameters.ContentType = 'application/json'; $parameters.Body = ConvertTo-Json -InputObject $body -Depth 5 }
    Invoke-WebRequest @parameters
}
function Assert-Status($response, [int]$expected, [string]$label) {
    if ([int]$response.StatusCode -ne $expected) { throw "$label : expected $expected, got $($response.StatusCode)." }
    $checks.Add($label)
}
function Refresh-Csrf($session) { $session.Token = (Invoke-Api $session GET '/api/auth/csrf').Content | ConvertFrom-Json | Select-Object -ExpandProperty requestToken }
function Login($session, [string]$email) {
    Refresh-Csrf $session
    Assert-Status (Invoke-Api $session POST '/api/auth/login' @{email=$email;password=$qaPassword}) 204 'HTTPS login'
    Refresh-Csrf $session
}
function Register([string]$email, [string]$role, [string]$name) {
    $session = New-ApiSession
    Refresh-Csrf $session
    Assert-Status (Invoke-Api $session POST '/api/auth/register' @{email=$email;password=$qaPassword;displayName=$name;requestedRole=$role}) 202 "Register $role"
    Login $session $email
    @{ Session=$session; User=((Invoke-Api $session GET '/api/me').Content | ConvertFrom-Json) }
}
try {
    $env:Ai__Provider = 'Mock'
    $env:Ai__Groq__ApiKey = $null
    $env:Ai__Gemini__ApiKey = $null
    foreach ($port in 5080,7080) { if (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue) { throw "Port $port is occupied; stop only the project API before running HTTP validation." } }
    docker compose --env-file "$projectRoot/.env" -f "$projectRoot/deploy/compose.yaml" exec -T postgres psql -U $localValues['POSTGRES_USER'] -d postgres -c "CREATE DATABASE `"$databaseName`";"
    if ($LASTEXITCODE -ne 0) { throw 'Could not create isolated HTTP test database.' }
    $createdDatabase = $true
    $env:ConnectionStrings__AulaTutor = $originalConnection + ';Database=' + $databaseName
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:Logging__LogLevel__Default = 'Warning'
    $env:BootstrapAdmin__Email = 'admin@example.invalid'
    $env:BootstrapAdmin__DisplayName = 'Administración ficticia'
    $env:BootstrapAdmin__Password = $qaPassword
    dotnet $apiDll --migrate --bootstrap-admin *> "$artifactDir/maintenance.log"
    if ($LASTEXITCODE -ne 0) { throw 'Migration/bootstrap command failed; inspect maintenance.log.' }
    dotnet $apiDll --bootstrap-admin *> "$artifactDir/bootstrap-repeat.log"
    if ($LASTEXITCODE -ne 0) { throw 'Idempotent bootstrap failed.' }
    Remove-Item Env:BootstrapAdmin__Email,Env:BootstrapAdmin__Password,Env:BootstrapAdmin__DisplayName
    $apiProcess = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @($apiDll,'--urls','https://localhost:7080;http://127.0.0.1:5080') -WorkingDirectory "$projectRoot/src/AulaTutor.Api" -WindowStyle Hidden -PassThru -RedirectStandardOutput "$artifactDir/api.log" -RedirectStandardError "$artifactDir/api-error.log"
    $ready = $false
    for ($attempt=0; $attempt -lt 60; $attempt++) {
        if ($apiProcess.HasExited) { throw 'HTTP API exited before startup.' }
        try { Invoke-WebRequest "$apiRoot/api/health" -SkipCertificateCheck -TimeoutSec 2 | Out-Null; $ready=$true; break } catch { Start-Sleep -Milliseconds 500 }
    }
    if (-not $ready) { throw 'HTTPS API did not start.' }
    $anonymous = New-ApiSession
    Assert-Status (Invoke-Api $anonymous GET '/api/me') 401 'Anonymous protected resource denied'
    Assert-Status (Invoke-Api $anonymous POST '/api/auth/login' @{email='admin@example.invalid';password=$qaPassword} -WithoutCsrf) 400 'Login CSRF enforced'
    $admin = New-ApiSession
    Login $admin 'admin@example.invalid'
    Assert-Status (Invoke-Api $admin GET '/api/admin/users') 200 'Admin authorized'
    Assert-Status (Invoke-Api $anonymous GET '/api/admin/ai/') 401 'Anonymous AI configuration denied'
    Assert-Status (Invoke-Api $anonymous POST '/api/admin/ai/test' @{}) 401 'Anonymous AI test denied'
    $aiConfiguration = Invoke-Api $admin GET '/api/admin/ai/'
    Assert-Status $aiConfiguration 200 'Admin AI configuration'
    if (($aiConfiguration.Content | ConvertFrom-Json).provider -ne 'Mock') { throw 'HTTP QA requires Mock.' }
    Assert-Status (Invoke-Api $admin POST '/api/admin/ai/test' @{} -WithoutCsrf) 400 'AI CSRF enforced'
    $aiResult = Invoke-Api $admin POST '/api/admin/ai/test' @{}
    Assert-Status $aiResult 200 'Explicit Mock diagnostic'
    if (-not ($aiResult.Content | ConvertFrom-Json).success) { throw 'Mock diagnostic failed.' }
    $student = Register 'student-a@example.invalid' 'Student' 'Estudiante A'
    $other = Register 'student-b@example.invalid' 'Student' 'Estudiante B'
    $teacher = Register 'teacher@example.invalid' 'Teacher' 'Docente pendiente'
    Assert-Status (Invoke-Api $student.Session POST '/api/admin/ai/test' @{}) 403 'Student AI quota access denied'
    Assert-Status (Invoke-Api $teacher.Session GET '/api/admin/ai/') 403 'Teacher AI configuration denied'
    $search = Invoke-Api $admin GET '/api/admin/users?email=STUDENT-A'
    Assert-Status $search 200 'Email search'
    if (($search.Content | ConvertFrom-Json).items[0].email -ne 'student-a@example.invalid') { throw 'Email search did not match.' }
    Assert-Status (Invoke-Api $teacher.Session GET '/api/groups/') 403 'Pending teacher denied'
    Assert-Status (Invoke-Api $student.Session GET "/api/users/$($other.User.id)") 404 'Cross-student profile hidden'
    Assert-Status (Invoke-Api $admin POST "/api/admin/users/$($teacher.User.id)/teacher-decision" @{approve=$true}) 204 'Teacher approval'
    Assert-Status (Invoke-Api $teacher.Session GET '/api/me') 401 'Approval revokes old session'
    Login $teacher.Session 'teacher@example.invalid'
    $groupResponse = Invoke-Api $admin POST '/api/groups/' @{name='SQL-01'}
    Assert-Status $groupResponse 201 'Group creation'
    $group = $groupResponse.Content | ConvertFrom-Json
    $foreign = (Invoke-Api $admin POST '/api/groups/' @{name='SQL-02'}).Content | ConvertFrom-Json
    Assert-Status (Invoke-Api $admin PUT "/api/groups/$($group.id)/members" @{userId=$teacher.User.id;kind='Teacher'}) 204 'Teacher assignment'
    Assert-Status (Invoke-Api $admin PUT "/api/groups/$($group.id)/members" @{userId=$student.User.id;kind='Student'}) 204 'Student membership'
    Assert-Status (Invoke-Api $teacher.Session GET "/api/groups/$($group.id)/members") 200 'Assigned teacher roster authorized'
    Assert-Status (Invoke-Api $teacher.Session GET "/api/groups/$($foreign.id)") 404 'Foreign teacher group hidden'
    Assert-Status (Invoke-Api $student.Session GET "/api/groups/$($group.id)/members") 404 'Student roster denied'
    Assert-Status (Invoke-Api $admin POST '/api/groups/' @{name='No-CSRF'} -WithoutCsrf) 400 'Authenticated CSRF enforced'
    # Tutoring HTTP QA is always Mock and isolated, never the user's Groq environment.
    $tutoringRequest = @{requestId=[Guid]::NewGuid().ToString();topic='IF EXISTS';goal='Comprender condiciones';level='Beginner';directReview=$false}
    Assert-Status (Invoke-Api $anonymous POST '/api/tutoring-sessions/' $tutoringRequest) 401 'Anonymous tutoring denied'
    $createdTutoring = Invoke-Api $student.Session POST '/api/tutoring-sessions/' $tutoringRequest
    Assert-Status $createdTutoring 201 'Private tutoring creation'
    $tutoring = $createdTutoring.Content | ConvertFrom-Json
    Assert-Status (Invoke-Api $student.Session POST '/api/tutoring-sessions/' $tutoringRequest) 201 'Idempotent tutoring creation'
    Assert-Status (Invoke-Api $other.Session GET "/api/tutoring-sessions/$($tutoring.id)") 404 'Other student tutoring hidden'
    Assert-Status (Invoke-Api $teacher.Session GET "/api/tutoring-sessions/$($tutoring.id)") 404 'Assigned teacher tutoring hidden'
    Assert-Status (Invoke-Api $admin GET "/api/tutoring-sessions/$($tutoring.id)") 404 'Admin private tutoring hidden'
    $startTurn = @{requestId=[Guid]::NewGuid().ToString();revision=$tutoring.revision;action='Start';text=$null;retry=$false}
    Assert-Status (Invoke-Api $student.Session POST "/api/tutoring-sessions/$($tutoring.id)/messages" $startTurn -WithoutCsrf) 400 'Tutoring CSRF enforced'
    $startResponse = Invoke-Api $student.Session POST "/api/tutoring-sessions/$($tutoring.id)/messages" $startTurn
    Assert-Status $startResponse 200 'Mock tutoring diagnostic'
    $tutoring = $startResponse.Content | ConvertFrom-Json
    if ($tutoring.messages.Count -ne 2 -or $tutoring.providerId -ne 'Mock') { throw 'Unexpected initial tutoring turn.' }
    $replayed = Invoke-Api $student.Session POST "/api/tutoring-sessions/$($tutoring.id)/messages" $startTurn
    Assert-Status $replayed 200 'Idempotent turn replay'
    if (($replayed.Content | ConvertFrom-Json).messages.Count -ne 2) { throw 'Turn replay duplicated a message.' }
    foreach ($turnAction in 'Message','Hint','Simplify') {
        $turn = @{requestId=[Guid]::NewGuid().ToString();revision=$tutoring.revision;action=$turnAction;text=$(if($turnAction -eq 'Message'){'Creo que comprueba si hay una fila.'}else{$null});retry=$false}
        $turnResponse = Invoke-Api $student.Session POST "/api/tutoring-sessions/$($tutoring.id)/messages" $turn
        Assert-Status $turnResponse 200 "Tutoring $turnAction"
        $tutoring = $turnResponse.Content | ConvertFrom-Json
    }
    $paused = Invoke-Api $student.Session POST "/api/tutoring-sessions/$($tutoring.id)/pause" @{revision=$tutoring.revision}
    Assert-Status $paused 200 'Tutoring pause'
    $tutoring = $paused.Content | ConvertFrom-Json
    $resumed = Invoke-Api $student.Session POST "/api/tutoring-sessions/$($tutoring.id)/resume" @{revision=$tutoring.revision}
    Assert-Status $resumed 200 'Tutoring resume'
    $tutoring = $resumed.Content | ConvertFrom-Json
    Assert-Status (Invoke-Api $student.Session GET "/api/tutoring-sessions/$($tutoring.id)") 200 'Stored tutoring reopened'
    $completed = Invoke-Api $student.Session POST "/api/tutoring-sessions/$($tutoring.id)/complete" @{revision=$tutoring.revision}
    Assert-Status $completed 200 'Tutoring completion'
    if (($completed.Content | ConvertFrom-Json).summary -notmatch 'sin llamada adicional') { throw 'Descriptive summary missing.' }
    Assert-Status (Invoke-Api $student.Session POST '/api/auth/logout' @{}) 204 'Logout'
    Assert-Status (Invoke-Api $student.Session GET '/api/me') 401 'Logged-out access denied'
    $checks | ConvertTo-Json | Set-Content "$artifactDir/results.json" -Encoding utf8
    $success = $true
    if ($KeepRunning) {
        @{ database=$databaseName; processId=$apiProcess.Id; password=$qaPassword; adminEmail='admin@example.invalid'; teacherEmail='teacher@example.invalid'; studentEmail='student-a@example.invalid'; tutoringOwnerEmail='student-b@example.invalid'; completedTutoringId=$tutoring.id; groupId=$group.id; foreignGroupId=$foreign.id } | ConvertTo-Json | Set-Content "$projectRoot/.cache/http-qa.json" -Encoding utf8
        Write-Host 'HTTP checks passed. Isolated API retained for browser QA; private state in .cache/http-qa.json.'
    } else { Write-Host 'HTTP identity checks passed on real HTTPS API/PostgreSQL.' }
} finally {
    if (-not ($KeepRunning -and $success)) {
        if ($apiProcess -and -not $apiProcess.HasExited) { Stop-Process -Id $apiProcess.Id -Force }
        if ($createdDatabase -and $databaseName -match '^aulatutor_http_[a-f0-9]{32}$') {
            docker compose --env-file "$projectRoot/.env" -f "$projectRoot/deploy/compose.yaml" exec -T postgres psql -U $localValues['POSTGRES_USER'] -d postgres -c "DROP DATABASE `"$databaseName`" WITH (FORCE);"
        }
    }
    $env:ConnectionStrings__AulaTutor = $originalConnection
    $env:Ai__Provider = $originalProvider
    $env:Ai__Groq__ApiKey = $originalGroqKey
    $env:Ai__Gemini__ApiKey = $originalGeminiKey
    Remove-Item Env:BootstrapAdmin__Email,Env:BootstrapAdmin__Password,Env:BootstrapAdmin__DisplayName -ErrorAction SilentlyContinue
}

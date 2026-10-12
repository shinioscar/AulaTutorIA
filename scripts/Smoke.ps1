$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Initialize-Environment.ps1"
$projectRoot = Split-Path -Parent $PSScriptRoot
$artifactDir = Join-Path $projectRoot 'artifacts/smoke'
New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null
$apiProcess = $null
$webProcess = $null
$previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
$previousProvider = $env:Ai__Provider

function Wait-Endpoint([string]$url, $process) {
    for ($attempt = 0; $attempt -lt 60; $attempt++) {
        if ($process.HasExited) { throw "Process exited before $url was ready. See $artifactDir." }
        try { return Invoke-WebRequest -Uri $url -TimeoutSec 2 } catch { Start-Sleep -Milliseconds 500 }
    }
    throw "Timeout waiting for $url."
}

try {
    foreach ($port in 5080, 4200) {
        if (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue) {
            throw "Port $port is already occupied. Stop the existing server before running Smoke.ps1."
        }
    }
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:Ai__Provider = 'Mock'
    $apiDll = Join-Path $projectRoot 'src/AulaTutor.Api/bin/Release/net10.0/AulaTutor.Api.dll'
    $apiProcess = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @($apiDll, '--urls', 'http://127.0.0.1:5080') -WorkingDirectory "$projectRoot/src/AulaTutor.Api" -WindowStyle Hidden -PassThru -RedirectStandardOutput "$artifactDir/api.log" -RedirectStandardError "$artifactDir/api-error.log"
    $healthResponse = Wait-Endpoint 'http://127.0.0.1:5080/api/health' $apiProcess
    $health = $healthResponse.Content | ConvertFrom-Json
    if ($health.status -ne 'ok' -or $health.aiProvider -ne 'Mock' -or $health.iteration -ne 0) {
        throw 'Unexpected health response.'
    }
    $schema = Invoke-RestMethod 'http://127.0.0.1:5080/openapi/v1.json'
    if (@($schema.paths.PSObject.Properties).Count -ne 1) { throw 'Unexpected API scope.' }
    $webCli = Join-Path $projectRoot 'src/AulaTutor.Web/node_modules/@angular/cli/bin/ng.js'
    $webProcess = Start-Process -FilePath (Get-Command node).Source -ArgumentList @($webCli, 'serve', '--host', '127.0.0.1', '--port', '4200', '--proxy-config', 'proxy.conf.json') -WorkingDirectory "$projectRoot/src/AulaTutor.Web" -WindowStyle Hidden -PassThru -RedirectStandardOutput "$artifactDir/web.log" -RedirectStandardError "$artifactDir/web-error.log"
    $webResponse = Wait-Endpoint 'http://127.0.0.1:4200' $webProcess
    if ($webResponse.Content -notmatch '<app-root>') { throw 'Angular index was not served.' }
    $proxiedHealth = Invoke-RestMethod 'http://127.0.0.1:4200/api/health'
    if ($proxiedHealth.aiProvider -ne 'Mock') { throw 'Angular proxy is not connected to Mock API.' }
    $healthResponse.Content | Set-Content "$artifactDir/health.json" -Encoding utf8
    Write-Host 'Smoke passed: API, OpenAPI, Angular and same-origin proxy with Mock.'
} finally {
    foreach ($process in $webProcess, $apiProcess) {
        if ($null -ne $process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
        }
    }
    $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
    $env:Ai__Provider = $previousProvider
}

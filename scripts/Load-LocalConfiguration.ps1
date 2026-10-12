$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$localEnvPath = Join-Path $projectRoot '.env'
if (-not (Test-Path -LiteralPath $localEnvPath)) { throw 'Create .env from .env.example and choose a private local password.' }
$localValues = @{}
foreach ($line in Get-Content -LiteralPath $localEnvPath) {
    if ($line -match '^([A-Z_]+)=(.*)$') { $localValues[$matches[1]] = $matches[2] }
}
function Quote-ConnectionValue([string]$value) { '"' + $value.Replace('"', '""') + '"' }
$localDbPort = if ($localValues['POSTGRES_PORT']) { $localValues['POSTGRES_PORT'] } else { '15432' }
$env:ConnectionStrings__AulaTutor = 'Host=localhost;Port=' + $localDbPort + ';Database=' + (Quote-ConnectionValue $localValues['POSTGRES_DB']) + ';Username=' + (Quote-ConnectionValue $localValues['POSTGRES_USER']) + ';Password=' + (Quote-ConnectionValue $localValues['POSTGRES_PASSWORD'])
$env:DataProtection__KeyPath = Join-Path $projectRoot '.cache/data-protection'
$env:Kestrel__Certificates__Default__Path = Join-Path $projectRoot '.cache/certs/localhost.pem'
$env:Kestrel__Certificates__Default__KeyPath = Join-Path $projectRoot '.cache/certs/localhost.key'
if (-not $env:AULATUTOR_TEST_CONNECTION) { $env:AULATUTOR_TEST_CONNECTION = $env:ConnectionStrings__AulaTutor }

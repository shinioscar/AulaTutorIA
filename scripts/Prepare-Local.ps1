$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Initialize-Environment.ps1"
$projectRoot = Split-Path -Parent $PSScriptRoot
$certDirectory = Join-Path $projectRoot '.cache/certs'
New-Item -ItemType Directory -Path $certDirectory -Force | Out-Null
if (-not (Test-Path "$certDirectory/localhost.pem")) {
    dotnet dev-certs https --export-path "$certDirectory/localhost.pem" --format PEM --no-password
    if ($LASTEXITCODE -ne 0) { throw 'Could not export the local development certificate.' }
}
if (-not (Test-Path "$certDirectory/localhost.key")) { throw 'Development certificate key is missing.' }
Write-Host 'Local HTTPS certificate exported inside .cache. Never commit certificates or keys.'

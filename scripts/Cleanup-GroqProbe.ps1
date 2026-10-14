$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Initialize-Environment.ps1"
. "$PSScriptRoot/Load-LocalConfiguration.ps1"
$cleanupRoot = Split-Path -Parent $PSScriptRoot
$cleanupStatePath = Join-Path $cleanupRoot '.cache/groq-qa.json'
if (-not (Test-Path -LiteralPath $cleanupStatePath)) { Write-Host 'No retained Groq probe.'; return }
$cleanupState = Get-Content -LiteralPath $cleanupStatePath -Raw | ConvertFrom-Json
if ($cleanupState.database -notmatch '^aulatutor_http_[a-f0-9]{32}$') { throw 'Invalid probe database name; cleanup refused.' }
$cleanupProcess = Get-CimInstance Win32_Process -Filter "ProcessId=$([int]$cleanupState.processId)" -ErrorAction SilentlyContinue
$cleanupDll = Join-Path $cleanupRoot 'src/AulaTutor.Api/bin/Release/net10.0/AulaTutor.Api.dll'
if ($cleanupProcess) {
    if ($cleanupProcess.Name -ne 'dotnet.exe' -or -not $cleanupProcess.CommandLine.Contains($cleanupDll)) { throw 'Probe PID ownership changed; cleanup refused.' }
    if (-not $cleanupState.processStartedAt -or [Math]::Abs(($cleanupProcess.CreationDate.ToUniversalTime() - [DateTime]::Parse($cleanupState.processStartedAt).ToUniversalTime()).TotalSeconds) -gt 1) { throw 'Probe PID start time changed; cleanup refused.' }
    Stop-Process -Id $cleanupState.processId -Force
}
docker compose --env-file "$cleanupRoot/.env" -f "$cleanupRoot/deploy/compose.yaml" exec -T postgres psql -U $localValues['POSTGRES_USER'] -d postgres -c "DROP DATABASE `"$($cleanupState.database)`" WITH (FORCE);" | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not remove probe database; retain state for recovery.' }
Remove-Item -LiteralPath $cleanupStatePath
Write-Host 'Isolated Groq process, database and private state removed.'

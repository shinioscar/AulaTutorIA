param([switch]$Smoke)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Initialize-Environment.ps1"
$projectRoot = Split-Path -Parent $PSScriptRoot

function Assert-CommandExit([string]$label) {
    if ($LASTEXITCODE -ne 0) { throw "$label failed (exit $LASTEXITCODE)." }
}

Push-Location $projectRoot
try {
    dotnet restore AulaTutorIA.slnx --locked-mode
    Assert-CommandExit 'dotnet restore'
    dotnet build AulaTutorIA.slnx -c Release --no-restore
    Assert-CommandExit 'dotnet build'
    dotnet test AulaTutorIA.slnx -c Release --no-build --no-restore
    Assert-CommandExit 'dotnet test'
    Push-Location 'src/AulaTutor.Web'
    try {
        npm.cmd ci --no-fund
        Assert-CommandExit 'npm ci'
        npm.cmd run check
        Assert-CommandExit 'TypeScript check'
        npm.cmd run build
        Assert-CommandExit 'Angular production build'
    } finally { Pop-Location }
    # Configuration validation does not require a running Docker daemon.
    docker compose --env-file .env.example -f deploy/compose.yaml --profile app config --quiet
    Assert-CommandExit 'Compose config'
    git diff --check
    Assert-CommandExit 'git diff check'
    if ($Smoke) { & "$PSScriptRoot/Smoke.ps1" }
    Write-Host 'Verification passed.'
} finally { Pop-Location }

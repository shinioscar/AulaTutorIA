param([switch]$Smoke, [switch]$Http)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Initialize-Environment.ps1"
$projectRoot = Split-Path -Parent $PSScriptRoot
$previousAiProvider = $env:Ai__Provider
$previousGroqKey = $env:Ai__Groq__ApiKey
$previousGeminiKey = $env:Ai__Gemini__ApiKey

function Assert-CommandExit([string]$label) {
    if ($LASTEXITCODE -ne 0) { throw "$label failed (exit $LASTEXITCODE)." }
}

Push-Location $projectRoot
try {
    dotnet restore AulaTutorIA.slnx --locked-mode
    Assert-CommandExit 'dotnet restore'
    dotnet build AulaTutorIA.slnx -c Release --no-restore
    Assert-CommandExit 'dotnet build'
    dotnet tool restore
    Assert-CommandExit 'dotnet tool restore'
    . "$PSScriptRoot/Load-LocalConfiguration.ps1"
    # Never select an external provider while running automated checks.
    $env:Ai__Provider = 'Mock'
    $env:Ai__Groq__ApiKey = $null
    $env:Ai__Gemini__ApiKey = $null
    dotnet test AulaTutorIA.slnx -c Release --no-build --no-restore
    Assert-CommandExit 'dotnet test'
    dotnet ef migrations has-pending-model-changes --project src/AulaTutor.Infrastructure --startup-project src/AulaTutor.Api --no-build --configuration Release
    Assert-CommandExit 'EF migration/model consistency'
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
    if ($Smoke) { & "$PSScriptRoot/Prepare-Local.ps1"; & "$PSScriptRoot/Smoke.ps1" }
    if ($Http) { & "$PSScriptRoot/Prepare-Local.ps1"; & "$PSScriptRoot/Http-Identity.ps1" }
    Write-Host 'Verification passed.'
} finally { $env:Ai__Provider = $previousAiProvider; $env:Ai__Groq__ApiKey = $previousGroqKey; $env:Ai__Gemini__ApiKey = $previousGeminiKey; Pop-Location }

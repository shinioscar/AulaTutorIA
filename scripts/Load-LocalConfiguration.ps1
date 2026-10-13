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
$aiMappings = @{
    AI_PROVIDER='Ai__Provider'; GROQ_API_KEY='Ai__Groq__ApiKey'; GROQ_MODEL='Ai__Groq__Model'
    GEMINI_API_KEY='Ai__Gemini__ApiKey'; GEMINI_MODEL='Ai__Gemini__Model'
    OLLAMA_BASE_URL='Ai__Ollama__BaseUrl'; OLLAMA_MODEL='Ai__Ollama__Model'
    AI_TIMEOUT_SECONDS='Ai__TimeoutSeconds'; AI_MAX_CONCURRENCY='Ai__MaxConcurrency'
    AI_MAX_RETRIES='Ai__MaxRetries'; AI_MAX_RETRY_DELAY_SECONDS='Ai__MaxRetryDelaySeconds'
}
foreach ($entry in $aiMappings.GetEnumerator()) {
    if ($localValues.ContainsKey($entry.Key) -and -not [Environment]::GetEnvironmentVariable($entry.Value)) {
        [Environment]::SetEnvironmentVariable($entry.Value, $localValues[$entry.Key], 'Process')
    }
}

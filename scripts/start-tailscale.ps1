$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Initialize-Environment.ps1"
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskDll = Join-Path $taskRoot 'src/AulaTutor.Api/bin/Release/net10.0/AulaTutor.Api.dll'
if (-not (Test-Path -LiteralPath $taskDll)) {
    throw 'Compila la API Release antes de iniciar este modo; el script no compila ni migra.'
}
foreach ($taskPort in 5080,7080) {
    if (Get-NetTCPConnection -State Listen -LocalPort $taskPort -ErrorAction SilentlyContinue) {
        throw "El puerto $taskPort está ocupado. Detén únicamente la API local del proyecto antes de iniciar."
    }
}
$taskTailscale = (Get-Command tailscale -ErrorAction Stop).Source
$taskStatusJson = & $taskTailscale status --json
if ($LASTEXITCODE -ne 0) { throw 'No se pudo consultar Tailscale.' }
$taskStatus = $taskStatusJson | ConvertFrom-Json
$taskDns = ([string]$taskStatus.Self.DNSName).TrimEnd('.')
if ($taskStatus.BackendState -ne 'Running' -or $taskDns -notmatch '^[a-z0-9-]+\.[a-z0-9-]+\.ts\.net$') {
    throw 'Tailscale debe estar conectado y disponer de su nombre DNS HTTPS.'
}
$taskOrigin = "https://$taskDns"
$taskServeJson = & $taskTailscale serve status --json
if ($LASTEXITCODE -ne 0) { throw 'No se pudo consultar la configuración Serve existente.' }
$taskServe = $taskServeJson | ConvertFrom-Json
if ($taskServe.AllowFunnel.PSObject.Properties | Where-Object { $_.Name -eq "${taskDns}:443" -and $_.Value }) {
    throw 'El puerto 443 tiene Funnel habilitado. Este modo exige Serve privado; revisa esa configuración manualmente.'
}
$taskTcp = if ($taskServe.TCP) { $taskServe.TCP.PSObject.Properties['443'].Value } else { $null }
if ($taskTcp -and -not $taskTcp.HTTPS) { throw 'El puerto 443 de Tailscale ya tiene otro servicio TCP.' }
$taskHost = if ($taskServe.Web) { $taskServe.Web.PSObject.Properties["${taskDns}:443"].Value } else { $null }
$taskHandler = if ($taskHost.Handlers) { $taskHost.Handlers.PSObject.Properties['/'].Value } else { $null }
if ($taskHandler -and $taskHandler.Proxy -ne 'https+insecure://localhost:4200') {
    throw 'La raíz HTTPS de Tailscale ya tiene otro destino. No se reemplazará automáticamente.'
}
try {
    $taskWeb = Invoke-WebRequest 'https://localhost:4200/' -SkipCertificateCheck -TimeoutSec 5
    if ($taskWeb.Content -notmatch '<app-root') { throw 'No es la web esperada.' }
} catch {
    throw 'Inicia primero Angular en https://localhost:4200 desde otra terminal del proyecto.'
}

# Explicit local loader; credentials stay in the API process, never Angular or Serve arguments.
. "$PSScriptRoot/Load-LocalConfiguration.ps1"
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DOTNET_ENVIRONMENT = 'Development'
$env:Logging__LogLevel__Default = 'Warning'
$env:PersonalAi__PublicOrigin = $taskOrigin
$env:PersonalAi__AllowedOrigins__0 = $taskOrigin
& $taskTailscale serve --bg --https=443 https+insecure://localhost:4200
if ($LASTEXITCODE -ne 0) { throw 'No se pudo habilitar Serve privado; la API no se inició.' }
Write-Output "AulaTutor IA: $taskOrigin (solo tailnet). API en primer plano; Ctrl+C para detenerla."
Push-Location (Join-Path $taskRoot 'src/AulaTutor.Api')
try {
    & dotnet $taskDll --urls 'https://localhost:7080;http://127.0.0.1:5080'
    if ($LASTEXITCODE -ne 0) { throw 'La API terminó con error; revisa el diagnóstico local privado.' }
} finally {
    Pop-Location
}

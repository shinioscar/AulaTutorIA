$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/Initialize-Environment.ps1"
. "$PSScriptRoot/Load-LocalConfiguration.ps1"
$projectRoot = Split-Path -Parent $PSScriptRoot
$env:BootstrapAdmin__Email = Read-Host 'Email del administrador inicial'
$env:BootstrapAdmin__DisplayName = Read-Host 'Nombre del administrador inicial'
$adminPassword = Read-Host 'Contraseña (mínimo 5 caracteres; recomendamos longitud y variedad)' -AsSecureString
try {
    $env:BootstrapAdmin__Password = [System.Net.NetworkCredential]::new('', $adminPassword).Password
    dotnet run --project "$projectRoot/src/AulaTutor.Api" --no-launch-profile -- --bootstrap-admin
    if ($LASTEXITCODE -ne 0) { throw 'Bootstrap failed; no default administrator was created.' }
} finally {
    Remove-Item Env:BootstrapAdmin__Password,Env:BootstrapAdmin__Email,Env:BootstrapAdmin__DisplayName -ErrorAction SilentlyContinue
    $adminPassword.Dispose()
}

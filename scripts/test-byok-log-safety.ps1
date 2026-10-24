param(
    [string[]]$LogPaths=@('artifacts/http-identity/api.log','artifacts/http-identity/api-error.log','.cache/restart-qa.log','.cache/restart-qa-error.log'),
    [string]$FixturePassword,
    [string]$OutputPath='artifacts/byok-log-safety.json'
)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskRootPrefix=[IO.Path]::GetFullPath($taskRoot)+[IO.Path]::DirectorySeparatorChar
function Resolve-QaPath([string]$relative){
    $taskPath=[IO.Path]::GetFullPath((Join-Path $taskRoot $relative))
    if(-not $taskPath.StartsWith($taskRootPrefix,[StringComparison]::OrdinalIgnoreCase)){throw 'QA log paths must remain within the workspace.'}
    return $taskPath
}
$taskForbidden=@($FixturePassword,'qa_personal_fictitious_credential_never_real','qa_manual_fictitious_credential_only','qa_fictitious_one_time_code','code_verifier','__Host-AulaTutor.Session=')
$taskEmpty=0
foreach($taskLogPath in $LogPaths){
    # Shared reading permits a live redirected writer on Windows. Empty files can
    # yield AutomationNull; handle them before invoking string methods.
    $taskLogText=Get-Content -LiteralPath (Resolve-QaPath $taskLogPath) -Raw
    if([string]::IsNullOrEmpty($taskLogText)){$taskEmpty++;continue}
    foreach($taskValue in $taskForbidden){if($taskValue -and $taskLogText.Contains($taskValue)){throw 'A fictional secret or private protocol field appeared in QA logs; inspect privately.'}}
    if($taskLogText -match 'Aa1![a-f0-9]{32}'){throw 'A fictional fixture password appeared in QA logs; inspect privately.'}
}
@{status='passed';logsChecked=$LogPaths.Count;emptyLogs=$taskEmpty;fixtureSecretMatches=0;scope='Known fictional keys, callback code, fixture passwords and private protocol fields; values never printed'} | ConvertTo-Json | Set-Content -LiteralPath (Resolve-QaPath $OutputPath) -Encoding utf8
Write-Output 'BYOK log safety passed; no fixture secrets or private protocol fields detected.'

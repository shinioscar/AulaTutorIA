# Shares only the isolated HTTPS harness's fictional actors and helpers.
Assert-Status (Invoke-Api $anonymous GET '/api/admin/operations/readiness') 401 'Anonymous readiness denied'
Assert-Status (Invoke-Api $student.Session GET '/api/admin/operations/readiness') 403 'Student readiness denied'
Assert-Status (Invoke-Api $teacher.Session GET '/api/admin/operations/audit') 403 'Teacher operational audit denied'
Assert-Status (Invoke-Api $student.Session GET '/api/admin/operations/usage') 403 'Student aggregate usage denied'
$operationReady = Invoke-Api $admin GET '/api/admin/operations/readiness'
Assert-Status $operationReady 200 'Admin PostgreSQL readiness'
$operationBody = $operationReady.Content | ConvertFrom-Json
if (-not $operationBody.database -or $operationBody.aiAvailabilityChecked) { throw 'Readiness must check only PostgreSQL connectivity.' }
if (-not $operationReady.Headers.ContainsKey('X-Correlation-ID')) { throw 'Server correlation missing.' }
Assert-Status (Invoke-Api $admin GET '/api/admin/operations/audit?page=0') 400 'Audit page bounds enforced'
$operationUsage = Invoke-Api $admin GET '/api/admin/operations/usage'
Assert-Status $operationUsage 200 'Admin usage reads without IA call'
if (($operationUsage.Content | ConvertFrom-Json).providers.Count -ne 0) { throw 'Mock must not invent provider consumption.' }
$operationEvents = @()
for ($operationPage=1; $operationPage -le 10; $operationPage++) {
    $operationResponse = Invoke-Api $admin GET "/api/admin/operations/audit?page=$operationPage"
    Assert-Status $operationResponse 200 'Bounded operational audit page'
    $operationRows = @($operationResponse.Content | ConvertFrom-Json)
    if ($operationRows.Count -gt 50) { throw 'Audit response exceeded page bound.' }
    $operationEvents += $operationRows
    if ($operationRows.Count -lt 50) { break }
}
foreach ($operationType in 'TeacherApprovalChanged','RoleChanged','MembershipChanged','LessonPublicationChanged','LessonAssignmentChanged','ReportShareChanged','TeacherReviewRecorded','SessionsRevoked') {
    if (-not ($operationEvents | Where-Object type -eq $operationType)) { throw "Missing transactional audit category: $operationType" }
}
$operationAllowed = @('id','actorId','createdAt','type','resourceId','result','correlationId','change')
foreach ($operationEvent in $operationEvents) {
    if ($operationEvent.PSObject.Properties.Name | Where-Object { $_ -notin $operationAllowed }) { throw 'Audit exposed an unexpected field.' }
    if ($operationEvent.correlationId -notmatch '^[a-f0-9]{32}$') { throw 'Audit correlation must be server generated.' }
}
$checks.Add('Sensitive changes have minimal transactional audit without private academic payload')
if (-not ($operationEvents | Where-Object { $_.type -eq 'MembershipChanged' -and $_.change -eq 'Deleted' -and $_.actorId })) { throw 'Membership withdrawal is missing its authorized transactional audit.' }
$checks.Add('Membership withdrawal records a transactional deletion event')

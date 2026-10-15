# Invoked by Http-Identity.ps1; only its isolated Mock HTTPS/PostgreSQL fixture.
$assignmentBody = @{ requestId=[Guid]::NewGuid().ToString(); lessonVersionId=$versionId; groupId=$group.id; availableFrom=$null; availableUntil=$null }
Assert-Status (Invoke-Api $anonymous GET '/api/students/me/classes/') 401 'Anonymous assigned classes denied'
Assert-Status (Invoke-Api $anonymous POST '/api/teacher/assignments/' $assignmentBody) 401 'Anonymous assignments denied'
Assert-Status (Invoke-Api $student.Session POST '/api/teacher/assignments/' $assignmentBody) 403 'Student cannot assign classes'
Assert-Status (Invoke-Api $admin POST '/api/teacher/assignments/' $assignmentBody) 403 'Admin has no implicit teacher assignment authority'
Assert-Status (Invoke-Api $teacher.Session POST '/api/teacher/assignments/' $assignmentBody -WithoutCsrf) 400 'Assignment CSRF enforced'
Assert-Status (Invoke-Api $teacher.Session GET '/api/teacher/assignments/published-versions') 200 'Published version selector'
Assert-Status (Invoke-Api $teacher.Session GET '/api/teacher/assignments/authorized-groups') 200 'Authorized assignment group selector'
$foreignBody = $assignmentBody.Clone(); $foreignBody.groupId=$foreign.id
Assert-Status (Invoke-Api $teacher.Session POST '/api/teacher/assignments/' $foreignBody) 404 'Foreign assignment group hidden'
$response = Invoke-Api $teacher.Session POST '/api/teacher/assignments/' $assignmentBody
Assert-Status $response 201 'Published version assigned to group'
$assignment = $response.Content | ConvertFrom-Json
Assert-Status (Invoke-Api $teacher.Session GET "/api/teacher/assignments/$($assignment.id)") 200 'Created assignment resource can be queried'
if ($assignment.versionNumber -ne 1 -or $assignment.contentHash -ne $versionHash) { throw 'Assignment silently selected another version.' }
Assert-Status (Invoke-Api $teacher.Session POST '/api/teacher/assignments/' $assignmentBody) 201 'Assignment creation replay'
$duplicateBody = $assignmentBody.Clone(); $duplicateBody.requestId=[Guid]::NewGuid().ToString()
Assert-Status (Invoke-Api $teacher.Session POST '/api/teacher/assignments/' $duplicateBody) 409 'Active equivalent assignment rejected'
Assert-Status (Invoke-Api $student.Session GET '/api/students/me/classes/') 200 'Own assigned class list'
$response = Invoke-Api $student.Session GET "/api/students/me/classes/$($assignment.id)"
Assert-Status $response 200 'Assigned class detail without private rules'
if ($response.Content -match 'acceptedAnswers|referenceSolution') { throw 'Private academic rules leaked.' }
$startBody = @{requestId=[Guid]::NewGuid().ToString()}
Assert-Status (Invoke-Api $student.Session POST "/api/students/me/classes/$($assignment.id)/start" $startBody -WithoutCsrf) 400 'Assigned start CSRF'
$response = Invoke-Api $student.Session POST "/api/students/me/classes/$($assignment.id)/start" $startBody
Assert-Status $response 200 'Individual assigned session created'
$assignedA = $response.Content | ConvertFrom-Json
$response = Invoke-Api $student.Session POST "/api/students/me/classes/$($assignment.id)/start" @{requestId=[Guid]::NewGuid().ToString()}
Assert-Status $response 200 'Assigned start unique across keys'
if (($response.Content | ConvertFrom-Json).id -ne $assignedA.id -or $assignedA.messages.Count -ne 0) { throw 'Start duplicated session or generated implicitly.' }
$response = Invoke-Api $other.Session POST "/api/students/me/classes/$($assignment.id)/start" @{requestId=[Guid]::NewGuid().ToString()}
Assert-Status $response 200 'Second student independent assigned session'
$assignedB = $response.Content | ConvertFrom-Json
if ($assignedA.id -eq $assignedB.id) { throw 'Students share a learning session.' }
Assert-Status (Invoke-Api $other.Session GET "/api/tutoring-sessions/$($assignedA.id)") 404 'Other student assigned conversation hidden'
Assert-Status (Invoke-Api $teacher.Session GET "/api/tutoring-sessions/$($assignedA.id)") 404 'Assignment does not grant teacher chat access'
$assignedA = Invoke-LearningTurn $student.Session $assignedA 'Start'
$requiredA = $assignedA.learning.exercises | Where-Object required
Assert-Status (Invoke-Api $student.Session POST "/api/tutoring-sessions/$($assignedA.id)/complete" @{revision=$assignedA.revision}) 409 'Required activities block completion'
$assignedA = Invoke-LearningTurn $student.Session $assignedA 'Hint'
$assignedA = Invoke-LearningTurn $student.Session $assignedA 'SubmitAttempt' 'Verdadero' $requiredA.id
$assignedA = Invoke-LearningTurn $student.Session $assignedA 'SubmitAttempt' 'Falso' $requiredA.id
$assignedA = Invoke-LearningTurn $student.Session $assignedA 'GenerateExercise'
$complement = $assignedA.learning.exercises | Where-Object isComplementary
if (-not $complement -or $complement.required -or $complement.approvedExerciseId) { throw 'Complement changed the mandatory source.' }
$assignedA = Invoke-LearningTurn $student.Session $assignedA 'SubmitAttempt' 'Falso' $complement.id
$assignedA = Invoke-LearningTurn $student.Session $assignedA 'NextActivity'
$response = Invoke-Api $student.Session POST "/api/tutoring-sessions/$($assignedA.id)/pause" @{revision=$assignedA.revision}
Assert-Status $response 200 'Assigned class pause'
$assignedA = $response.Content | ConvertFrom-Json
Assert-Status (Invoke-Api $student.Session GET "/api/tutoring-sessions/$($assignedA.id)") 200 'Assigned class reload retains evidence'
$response = Invoke-Api $student.Session POST "/api/tutoring-sessions/$($assignedA.id)/resume" @{revision=$assignedA.revision}
Assert-Status $response 200 'Assigned class resume'
$assignedA = $response.Content | ConvertFrom-Json
$assignedB = Invoke-LearningTurn $other.Session $assignedB 'Start'
$requiredB = $assignedB.learning.exercises | Where-Object required
if ($requiredA.approvedExerciseId -ne $requiredB.approvedExerciseId -or $requiredA.id -eq $requiredB.id) { throw 'Approved core identity was not preserved per student.' }
$assignedB = Invoke-LearningTurn $other.Session $assignedB 'SubmitAttempt' 'Falso' $requiredB.id
if ($assignedA.learning.attempts[1].outcome -ne 'CorrectWithHelp' -or $assignedB.learning.attempts[0].outcome -ne 'CorrectWithoutHelp') { throw 'Different paths mixed student evidence.' }
foreach ($entry in @(@{Session=$student.Session;State=$assignedA},@{Session=$other.Session;State=$assignedB})) {
    $response = Invoke-Api $entry.Session POST "/api/tutoring-sessions/$($entry.State.id)/complete" @{revision=$entry.State.revision}
    Assert-Status $response 200 'Assigned completion and local evidence report'
    $completedClass = $response.Content | ConvertFrom-Json
    if ($completedClass.learning.reports[0].providerId -ne 'Application' -or $completedClass.learning.reports[0].content.summary -notmatch [regex]::Escape($versionId)) { throw 'Local report lost its approved source.' }
}
$change = @{requestId=[Guid]::NewGuid().ToString();revision=$assignment.revision}
$response = Invoke-Api $teacher.Session POST "/api/teacher/assignments/$($assignment.id)/deactivate" $change
Assert-Status $response 200 'Assignment deactivation preserves evidence'
$assignment = $response.Content | ConvertFrom-Json
Assert-Status (Invoke-Api $student.Session GET "/api/tutoring-sessions/$($assignedA.id)") 200 'Unavailable assigned history retained'
$change = @{requestId=[Guid]::NewGuid().ToString();revision=$assignment.revision}
$response = Invoke-Api $teacher.Session POST "/api/teacher/assignments/$($assignment.id)/withdraw" $change
Assert-Status $response 200 'Assignment withdrawal recorded'
Assert-Status (Invoke-Api $teacher.Session GET '/api/teacher/assignments/') 200 'Assignment history retained'
# A removed relationship revokes every subsequent resource read, without deleting rows.
Assert-Status (Invoke-Api $admin DELETE "/api/groups/$($group.id)/members/$($student.User.id)/Student") 204 'Assigned student relationship removed'
Assert-Status (Invoke-Api $student.Session GET "/api/tutoring-sessions/$($assignedA.id)") 404 'Removed member assigned history denied'
Assert-Status (Invoke-Api $student.Session GET "/api/tutoring-sessions/$($tutoring.id)") 200 'Membership removal leaves autonomous tutoring private and readable'

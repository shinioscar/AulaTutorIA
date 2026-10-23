# Runs only in the isolated Mock Kestrel/PostgreSQL fixture of Http-Identity.ps1.
$supervisionBase = '/api/teacher/supervision'
Assert-Status (Invoke-Api $anonymous GET "$supervisionBase/") 401 'Anonymous supervision denied'
Assert-Status (Invoke-Api $admin GET "$supervisionBase/") 403 'Admin has no general supervision permission'
Assert-Status (Invoke-Api $teacher.Session GET "$supervisionBase/assignments/$($assignment.id)") 404 'Withdrawn assignment ends teacher supervision'
Assert-Status (Invoke-Api $admin PUT "/api/groups/$($group.id)/members" @{userId=$student.User.id;kind='Student'}) 204 'Restore fictitious membership for supervision'
$response = Invoke-Api $teacher.Session POST '/api/teacher/assignments/' @{requestId=[Guid]::NewGuid().ToString();lessonVersionId=$versionId;groupId=$group.id}
Assert-Status $response 201 'Create supervised assignment of exact published version'
$supervisionAssignment = $response.Content | ConvertFrom-Json
$supervisionPath = "$supervisionBase/assignments/$($supervisionAssignment.id)"
$response = Invoke-Api $teacher.Session GET "$supervisionBase/?groupId=$($group.id)"
Assert-Status $response 200 'Teacher authorized group dashboard'
$dashboard = $response.Content | ConvertFrom-Json
if ($dashboard.groups[0].eligibleStudents -ne 2 -or $dashboard.assignments[0].counts.notOpened -ne 2) { throw 'Supervision cohort or participation denominator is wrong.' }
Assert-Status (Invoke-Api $teacher.Session GET "$supervisionBase/?groupId=$($foreign.id)") 404 'Foreign supervision group hidden'
$response = Invoke-Api $student.Session POST "/api/students/me/classes/$($supervisionAssignment.id)/start" @{requestId=[Guid]::NewGuid().ToString()}
Assert-Status $response 200 'Supervision student class opened'
$supervisionSession = $response.Content | ConvertFrom-Json
$response = Invoke-Api $teacher.Session GET $supervisionPath
Assert-Status $response 200 'Opened session is distinguished from conversation participation'
$classResults = $response.Content | ConvertFrom-Json
if ($classResults.assignment.counts.opened -ne 1 -or $classResults.assignment.counts.started -ne 0) { throw 'An unopened conversation was counted as started.' }
$supervisionSession = Invoke-LearningTurn $student.Session $supervisionSession 'Start'
$coreExercise = $supervisionSession.learning.exercises | Where-Object required
$supervisionSession = Invoke-LearningTurn $student.Session $supervisionSession 'SubmitAttempt' 'Falso' $coreExercise.id
$supervisionSession = Invoke-LearningTurn $student.Session $supervisionSession 'NextActivity'
$openExercise = $supervisionSession.learning.exercises | Where-Object {$_.id -eq $supervisionSession.assignedClass.currentExerciseId}
$supervisionSession = Invoke-LearningTurn $student.Session $supervisionSession 'SubmitAttempt' 'IF EXISTS: respuesta abierta ficticia para revisión docente' $openExercise.id
$individualPath = "$supervisionPath/students/$($student.User.id)"
$response = Invoke-Api $teacher.Session GET $individualPath
Assert-Status $response 200 'Teacher reads assigned academic evidence'
if ($response.Content -match 'acceptedAnswers|referenceSolution|contextJson|"messages"') { throw 'Supervision leaked chat or private rules.' }
$individual = $response.Content | ConvertFrom-Json
if ($individual.exercises[0].options.Count -ne 2) { throw 'Public exercise options missing from academic evidence.' }
if ($individual.exercises[1].attempts[0].outcome -ne 'NeedsReview' -or $individual.cases[0].pendingReview -ne $true -or $individual.objectives[0].worked -ne $true) { throw 'Open evidence or worked objective was mislabeled.' }
Assert-Status (Invoke-Api $admin GET $individualPath) 404 'Admin academic evidence hidden'
Assert-Status (Invoke-Api $other.Session GET $individualPath) 404 'Student B cannot read student A supervision'
Assert-Status (Invoke-Api $teacher.Session GET "/api/tutoring-sessions/$($supervisionSession.id)") 404 'Supervision still excludes conversation history'
$reviewBody = @{requestId=[Guid]::NewGuid().ToString();revision=0;evidenceRevision=$individual.evidenceRevision;exerciseId=$openExercise.id;state='InReview';attention='InProgress';outcome='NeedsReinforcement';flagged=$false;comment='Revisa la condición antes de continuar.';observation='El ejemplo necesita justificar IF EXISTS.';recommendation='Compara un caso con registro y otro sin registro.';evidenceIds=@($individual.exercises[1].attempts[0].id)}
Assert-Status (Invoke-Api $anonymous POST "$individualPath/reviews" $reviewBody) 401 'Anonymous review write denied'
Assert-Status (Invoke-Api $teacher.Session POST "$individualPath/reviews" $reviewBody -WithoutCsrf) 400 'Teacher review CSRF enforced'
$response = Invoke-Api $teacher.Session POST "$individualPath/reviews" $reviewBody
Assert-Status $response 200 'Teacher decision stored separately'
Assert-Status (Invoke-Api $teacher.Session POST "$individualPath/reviews" $reviewBody) 200 'Review duplicate replay without another decision'
$reuse = $reviewBody.Clone(); $reuse.comment='Otra decisión'
Assert-Status (Invoke-Api $teacher.Session POST "$individualPath/reviews" $reuse) 409 'Review key cannot change its decision'
$response = Invoke-Api $student.Session GET "/api/students/me/classes/$($supervisionAssignment.id)/feedback"
Assert-Status $response 200 'Correct student sees teacher feedback'
$studentFeedback = $response.Content | ConvertFrom-Json
if ($studentFeedback[0].history.Count -ne 1 -or $studentFeedback[0].history[0].authorId -ne $teacher.User.id) { throw 'Teacher feedback duplicated or lost attribution.' }
$response = Invoke-Api $other.Session GET "/api/students/me/classes/$($supervisionAssignment.id)/feedback"
Assert-Status $response 200 'Other student receives only own empty feedback'
if (($response.Content | ConvertFrom-Json).Count -ne 0) { throw 'Another student received feedback.' }
$reviewBody.requestId=[Guid]::NewGuid().ToString();$reviewBody.revision=1;$reviewBody.state='Reviewed';$reviewBody.attention='Closed'
Assert-Status (Invoke-Api $teacher.Session POST "$individualPath/reviews" $reviewBody) 200 'Human follow-up closed with a new historical decision'
$response = Invoke-Api $teacher.Session GET $individualPath
Assert-Status $response 200 'Human review history and unchanged primary evidence'
$individual = $response.Content | ConvertFrom-Json
if ($individual.reviews[0].history.Count -ne 2 -or $individual.exercises[1].attempts[0].outcome -ne 'NeedsReview' -or -not $individual.cases[0].resolved) { throw 'Review rewrote primary outcome or lost history.' }
$response = Invoke-Api $teacher.Session GET $supervisionPath
Assert-Status $response 200 'Group metrics separate review decisions from automatic results'
$classResults = $response.Content | ConvertFrom-Json
if ($classResults.objectives[0].workedStudents -ne 1 -or $classResults.objectives[0].attempts -ne 2 -or $classResults.objectives[0].verifiedWithoutHelp -ne 1 -or $classResults.objectives[0].needsReviewAttempts -ne 1 -or $classResults.assignment.counts.pendingReviews -ne 0) { throw 'Group objective aggregation changed automatic results or double counted.' }
Assert-Status (Invoke-Api $admin DELETE "/api/groups/$($group.id)/members/$($student.User.id)/Student") 204 'Supervised student relationship removed'
Assert-Status (Invoke-Api $teacher.Session GET $individualPath) 404 'Removed student evidence hidden from teacher'
Assert-Status (Invoke-Api $student.Session GET "/api/students/me/classes/$($supervisionAssignment.id)/feedback") 404 'Removed relationship blocks feedback reads'
Assert-Status (Invoke-Api $admin PUT "/api/groups/$($group.id)/members" @{userId=$student.User.id;kind='Student'}) 204 'Reinstatement preserves original supervision history'
Assert-Status (Invoke-Api $teacher.Session GET $individualPath) 200 'Historical review readable after legitimate reinstatement'

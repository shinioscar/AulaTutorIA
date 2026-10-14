# Invoked by Http-Identity.ps1 inside its isolated Mock HTTPS/PostgreSQL fixture.
function Invoke-LessonCommand($state, [string]$action, [string]$targetId = '') {
    $body = @{ requestId=[Guid]::NewGuid().ToString(); revision=$state.revision; targetId=$(if($targetId){$targetId}else{$null}) }
    $response = Invoke-Api $teacher.Session POST "/api/lessons/$($state.id)/$action" $body
    Assert-Status $response 200 "Lesson $action"
    $response.Content | ConvertFrom-Json
}
function Invoke-SimulationTurn($state, [string]$action, [string]$text = '') {
    $body = @{ requestId=[Guid]::NewGuid().ToString(); revision=$state.revision; action=$action; text=$(if($text){$text}else{$null}); retry=$false }
    for ($lessonWait=0; $lessonWait -lt 8; $lessonWait++) {
        $response = Invoke-Api $teacher.Session POST "/api/lessons/$($state.lessonId)/simulations/$($state.id)/messages" $body
        if ([int]$response.StatusCode -ne 429) { break }
        Start-Sleep -Seconds 10
    }
    Assert-Status $response 200 "Simulation $action"
    if ($response.Content -match 'acceptedAnswers|referenceSolution|contextJson') { throw 'Private rules leaked in simulation.' }
    $result = $response.Content | ConvertFrom-Json
    if ($result.operations[-1].state -ne 'Succeeded') { throw "Simulation $action failed." }
    $result
}
$lessonContent = @{title='Clase ficticia SQL';subject='SQL';description='';prerequisites='';level='Básico';durationMinutes=45;methodology='Explicar y practicar';instructions='';objectives=@();sections=@()}
$lessonCreate = @{requestId=[Guid]::NewGuid().ToString();content=$lessonContent}
Assert-Status (Invoke-Api $anonymous POST '/api/lessons/' $lessonCreate) 401 'Anonymous lessons denied'
Assert-Status (Invoke-Api $other.Session POST '/api/lessons/' $lessonCreate) 403 'Student lesson creation denied'
Assert-Status (Invoke-Api $admin POST '/api/lessons/' $lessonCreate) 403 'Admin has no teacher authority'
Assert-Status (Invoke-Api $teacher.Session POST '/api/lessons/' $lessonCreate -WithoutCsrf) 400 'Lesson CSRF enforced'
$response = Invoke-Api $teacher.Session POST '/api/lessons/' $lessonCreate
Assert-Status $response 201 'Incomplete lesson draft saved'
$lesson = $response.Content | ConvertFrom-Json
Assert-Status (Invoke-Api $teacher.Session POST '/api/lessons/' $lessonCreate) 201 'Idempotent lesson creation'
Assert-Status (Invoke-Api $other.Session GET "/api/lessons/$($lesson.id)") 404 'Student lesson contents hidden'
Assert-Status (Invoke-Api $admin GET "/api/lessons/$($lesson.id)") 404 'Admin lesson contents hidden'
$response = Invoke-Api $teacher.Session POST "/api/lessons/$($lesson.id)/design" @{requestId=[Guid]::NewGuid().ToString();revision=$lesson.revision;action='Plan';text='Preparar una clase básica de 45 minutos';retry=$false}
Assert-Status $response 200 'Structured Mock lesson proposal'
$lesson = $response.Content | ConvertFrom-Json
if ($lesson.proposals.Count -ne 1 -or $lesson.content.sections.Count -ne 0) { throw 'AI changed the draft without approval.' }
$lesson = Invoke-LessonCommand $lesson 'apply-proposal' $lesson.proposals[0].id
$oldRevision = $lesson.revision
$lesson.content.title = 'Clase revisada manualmente'
$lesson.content.sections = @($lesson.content.sections | Sort-Object id -Descending)
$response = Invoke-Api $teacher.Session PUT "/api/lessons/$($lesson.id)/draft" @{revision=$lesson.revision;content=$lesson.content}
Assert-Status $response 200 'Manual lesson edit and section reorder'
$lesson = $response.Content | ConvertFrom-Json
Assert-Status (Invoke-Api $teacher.Session PUT "/api/lessons/$($lesson.id)/draft" @{revision=$oldRevision;content=$lesson.content}) 409 'Stale lesson edit rejected'
# Restore order to exercise the required closed activity first.
$lesson.content.sections = @($lesson.content.sections | Sort-Object id)
$response = Invoke-Api $teacher.Session PUT "/api/lessons/$($lesson.id)/draft" @{revision=$lesson.revision;content=$lesson.content}
Assert-Status $response 200 'Lesson section order restored'
$lesson = $response.Content | ConvertFrom-Json
$response = Invoke-Api $teacher.Session POST "/api/lessons/$($lesson.id)/simulations" @{requestId=[Guid]::NewGuid().ToString();revision=$lesson.revision}
Assert-Status $response 200 'Isolated lesson simulation created'
$simulation = $response.Content | ConvertFrom-Json
Assert-Status (Invoke-Api $other.Session GET "/api/lessons/$($lesson.id)/simulations/$($simulation.id)") 404 'Simulation ownership enforced'
$simulation = Invoke-SimulationTurn $simulation 'Start'
$simulation = Invoke-SimulationTurn $simulation 'Answer' 'Verdadero'
if ($simulation.operations[-1].outcome -ne 'Incorrect') { throw 'Incorrect simulated response missing.' }
$simulation = Invoke-SimulationTurn $simulation 'Hint'
$simulation = Invoke-SimulationTurn $simulation 'Answer' 'Falso'
if ($simulation.operations[-1].outcome -ne 'CorrectWithHelp') { throw 'Correct simulated response missing.' }
$simulation = Invoke-SimulationTurn $simulation 'Next'
$response = Invoke-Api $teacher.Session POST "/api/lessons/$($lesson.id)/simulations/$($simulation.id)/stop" @{requestId=[Guid]::NewGuid().ToString();revision=$simulation.revision}
Assert-Status $response 200 'Simulation stopped'
$simulation = $response.Content | ConvertFrom-Json
Assert-Status (Invoke-Api $teacher.Session POST "/api/lessons/$($lesson.id)/simulations/$($simulation.id)/resume" @{requestId=[Guid]::NewGuid().ToString();revision=$simulation.revision}) 200 'Simulation reopened without generation'
$lesson = (Invoke-Api $teacher.Session GET "/api/lessons/$($lesson.id)").Content | ConvertFrom-Json
$lesson = Invoke-LessonCommand $lesson 'review'
$lesson = Invoke-LessonCommand $lesson 'approve'
if ($lesson.versions.Count -ne 1 -or $lesson.versions[0].publishedAt) { throw 'Approval publication boundary failed.' }
$versionId = $lesson.versions[0].id
$versionHash = $lesson.versions[0].contentHash
$lesson = Invoke-LessonCommand $lesson 'publish' $versionId
Assert-Status (Invoke-Api $teacher.Session PUT "/api/lessons/$($lesson.id)/draft" @{revision=$lesson.revision;content=$lesson.content}) 409 'Approved content edit rejected'
$lesson = Invoke-LessonCommand $lesson 'new-draft'
$lesson.content.title = 'Nueva revisión posterior a publicación'
$response = Invoke-Api $teacher.Session PUT "/api/lessons/$($lesson.id)/draft" @{revision=$lesson.revision;content=$lesson.content}
Assert-Status $response 200 'New draft after publication'
$lesson = $response.Content | ConvertFrom-Json
$lesson = Invoke-LessonCommand $lesson 'review'
$lesson = Invoke-LessonCommand $lesson 'approve'
$lesson = Invoke-LessonCommand $lesson 'publish' $lesson.versions[-1].id
if ($lesson.versions.Count -ne 2 -or $lesson.versions[0].contentHash -ne $versionHash -or $lesson.versions[0].content.title -ne 'Clase revisada manualmente') { throw 'Published version changed.' }
Assert-Status (Invoke-Api $teacher.Session GET '/api/lessons/') 200 'Teacher lesson list'

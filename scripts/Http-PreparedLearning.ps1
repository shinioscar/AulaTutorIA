# Dot-sourced exclusively by the isolated HTTPS harness; fictitious Student actors.
function Invoke-PreparedCard($session,$state,[string]$action,[string]$text='') {
    $taskBody=@{requestId=[Guid]::NewGuid().ToString();revision=$state.revision;action=$action;text=$(if($text){$text}else{$null});exerciseId=$(if($action -eq 'SubmitAttempt'){$state.prepared.currentExerciseId}else{$null})}
    $taskResponse=Invoke-Api $session POST "/api/tutoring-sessions/$($state.id)/cards" $taskBody
    Assert-Status $taskResponse 200 "Prepared local $action"
    if($taskResponse.Content -match 'acceptedAnswers|contextJson|REFERENCIA_PRIVADA_MOCK_NO_PROYECTAR'){throw 'Prepared private material leaked.'}
    $taskResponse.Content | ConvertFrom-Json
}
Assert-Status (Invoke-Api $anonymous GET '/api/tutoring-sessions/prepared-options') 401 'Prepared anonymous options denied'
Assert-Status (Invoke-Api $other.Session GET '/api/tutoring-sessions/prepared-options') 200 'Prepared compatible lengths read without inference'
foreach($taskExperience in 'Tutoring','Diagnostic') {
    $taskCreate=Invoke-Api $other.Session POST '/api/tutoring-sessions/' @{requestId=[Guid]::NewGuid().ToString();topic='Evidencia ficticia LEARN-1';goal='Contrastar varios ejemplos';level='Beginner';experience=$taskExperience;modality='Prepared';length='Short'}
    Assert-Status $taskCreate 201 "Prepared $taskExperience creation"
    $taskLearn=$taskCreate.Content | ConvertFrom-Json
    Assert-Status (Invoke-Api $teacher.Session GET "/api/tutoring-sessions/$($taskLearn.id)") 404 'Prepared Teacher private access denied'
    Assert-Status (Invoke-Api $admin GET "/api/tutoring-sessions/$($taskLearn.id)") 404 'Prepared Admin private access denied'
    $taskLearn=Invoke-LearningTurn $other.Session $taskLearn 'PrepareSession'
    if($taskLearn.prepared.generationState -ne 'Ready' -or $taskLearn.learning.exercises.Count -ne 0){throw 'Prepared script must be complete and private before start.'}
    $taskLearn=Invoke-PreparedCard $other.Session $taskLearn 'Start'
    if($taskExperience -eq 'Tutoring'){$taskLearn=Invoke-PreparedCard $other.Session $taskLearn 'Hint'}
    while($taskLearn.prepared.currentExerciseId) {
        $taskExercise=$taskLearn.learning.exercises | Where-Object id -eq $taskLearn.prepared.currentExerciseId
        $taskAnswer=if($taskExercise.kind -eq 'OpenResponse'){'Evidencia abierta ficticia.'}else{'Falso'}
        $taskLearn=Invoke-PreparedCard $other.Session $taskLearn 'SubmitAttempt' $taskAnswer
        if($taskExperience -eq 'Diagnostic' -and @($taskLearn.learning.attempts | Where-Object outcome -ne 'Recorded').Count){throw 'Diagnostic outcomes exposed before close.'}
        $taskLearn=Invoke-PreparedCard $other.Session $taskLearn 'NextActivity'
    }
    $taskClose=Invoke-Api $other.Session POST "/api/tutoring-sessions/$($taskLearn.id)/complete" @{revision=$taskLearn.revision}
    Assert-Status $taskClose 200 "Prepared $taskExperience closes locally"
    $taskLearn=$taskClose.Content | ConvertFrom-Json
    if($taskLearn.learning.reports.Count -ne 1){throw 'Prepared local deterministic report missing.'}
    $taskLearn=Invoke-LearningTurn $other.Session $taskLearn 'FinalReport'
    if($taskLearn.learning.reports.Count -ne 2 -or ($taskExperience -eq 'Diagnostic' -and -not $taskLearn.learning.plan)){throw 'Grouped prepared report/plan missing.'}
    Assert-Status (Invoke-Api $other.Session GET "/api/tutoring-sessions/$($taskLearn.id)") 200 'Prepared report read without regeneration'
}
if(((Invoke-Api $admin GET '/api/admin/operations/usage').Content | ConvertFrom-Json).providers.Count -ne 0){throw 'Mock prepared work invented provider usage.'}

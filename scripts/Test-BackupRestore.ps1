$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskId = [Guid]::NewGuid().ToString('N')
$taskDatabase = 'aulatutor_restore_' + $taskId
if ($taskDatabase -notmatch '^aulatutor_restore_[a-f0-9]{32}$') { throw 'Invalid isolated database name.' }
$taskCompose = Join-Path $taskRoot 'deploy/compose.yaml'
$taskEnv = Join-Path $taskRoot '.env'
$taskDump = '/tmp/aulatutor_restore_' + $taskId + '.dump'
$taskCreated = $false
foreach ($taskPort in 5080,7080) { if (Get-NetTCPConnection -State Listen -LocalPort $taskPort -ErrorAction SilentlyContinue) { throw 'Stop the project API before comparing a backup snapshot.' } }
$taskCountsSql = 'SELECT json_build_object(''users'',(SELECT count(*) FROM "AspNetUsers"),''versions'',(SELECT count(*) FROM "LessonVersions"),''attempts'',(SELECT count(*) FROM "ExerciseAttempts"),''messages'',(SELECT count(*) FROM "TutoringMessages"),''reviews'',(SELECT count(*) FROM "TeacherReviewEntries"),''memberships'',(SELECT count(*) FROM "GroupMemberships"))::text;'
function Assert-BackupCommand { if ($LASTEXITCODE -ne 0) { throw 'Backup/restore verification command failed. Inspect private local PostgreSQL diagnostics.' } }
try {
    $taskSourceCount = docker compose --env-file $taskEnv -f $taskCompose exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At -v ON_ERROR_STOP=1 -c ''SELECT count(*) FROM "__EFMigrationsHistory";'''
    Assert-BackupCommand
    $taskSourceRows = docker compose --env-file $taskEnv -f $taskCompose exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At -v ON_ERROR_STOP=1 -c "$1"' sh $taskCountsSql
    Assert-BackupCommand
    # The database source comes only from the existing container environment.
    # Never print environment values, credentials, dump contents or academic rows.
    docker compose --env-file $taskEnv -f $taskCompose exec -T postgres sh -c 'umask 077; pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --format=custom --file="$1"' sh $taskDump
    Assert-BackupCommand
    $taskMode = docker compose --env-file $taskEnv -f $taskCompose exec -T postgres stat -c '%a' -- $taskDump
    Assert-BackupCommand
    if ($taskMode.Trim() -ne '600') { throw 'Backup file permissions are not private.' }
    docker compose --env-file $taskEnv -f $taskCompose exec -T postgres sh -c 'createdb -U "$POSTGRES_USER" --template=template0 "$1"' sh $taskDatabase
    Assert-BackupCommand
    $taskCreated = $true
    docker compose --env-file $taskEnv -f $taskCompose exec -T postgres sh -c 'pg_restore -U "$POSTGRES_USER" --dbname="$1" --single-transaction --exit-on-error --no-owner --no-privileges "$2"' sh $taskDatabase $taskDump
    Assert-BackupCommand
    $taskCount = docker compose --env-file $taskEnv -f $taskCompose exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$1" -At -v ON_ERROR_STOP=1 -c ''SELECT count(*) FROM "__EFMigrationsHistory";''' sh $taskDatabase
    Assert-BackupCommand
    if ([int]$taskCount -lt 6 -or [int]$taskCount -ne [int]$taskSourceCount) { throw 'Restored migration history is incomplete.' }
    $taskRestoredRows = docker compose --env-file $taskEnv -f $taskCompose exec -T postgres sh -c 'psql -U "$POSTGRES_USER" -d "$1" -At -v ON_ERROR_STOP=1 -c "$2"' sh $taskDatabase $taskCountsSql
    Assert-BackupCommand
    if ($taskSourceRows -ne $taskRestoredRows) { throw 'Restored primary row counts differ from the controlled source snapshot.' }
    Write-Output "Isolated backup/restore passed; restored migrations: $([int]$taskCount). No primary database writes."
} finally {
    try {
        if ($taskCreated) {
            docker compose --env-file $taskEnv -f $taskCompose exec -T postgres sh -c 'dropdb -U "$POSTGRES_USER" --force "$1"' sh $taskDatabase
            Assert-BackupCommand
        }
    } finally {
        docker compose --env-file $taskEnv -f $taskCompose exec -T postgres rm -f -- $taskDump
        Assert-BackupCommand
    }
}

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using AulaTutor.Application.Ai;
using AulaTutor.Application.Identity;
using AulaTutor.Application.Tutoring;
using AulaTutor.Domain.Identity;
using AulaTutor.Domain.Tutoring;
using AulaTutor.Infrastructure.Ai;
using AulaTutor.Infrastructure.Persistence;
using AulaTutor.Infrastructure.Lessons;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AulaTutor.Infrastructure.Tutoring;

public sealed partial class TutoringService(AulaTutorDbContext db, ICurrentActor current,
    IAiProvider provider, TutoringOrchestrator orchestrator, TimeProvider clock,
    IOptions<AiProviderOptions> options, ILogger<TutoringService> logger, LearningWorkflow learning,
    AssignedClassAccess assignedAccess, AssignedClassWorkflow assigned, PreparedLearningWorkflow prepared) : ITutoringService
{
    private static AppException Conflict(string message = "La sesión cambió. Actualiza su estado antes de continuar.") => new(409, message);
    private static AppException Hidden() => new(404, "La tutoría no está disponible.");
    private static void ValidateText(string? text, int min, int max)
    {
        if (text is null || text.Trim().Length < min || text.Length > max || text.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t')))
            throw new AppException(400, "Revisa el texto y los límites de los campos.");
    }
    private async Task<Actor> StudentAsync(bool hide = false)
    {
        var actor = await current.GetAsync();
        if (!actor.IsStudent) throw hide ? Hidden() : new AppException(403, "Se requiere una cuenta de estudiante activa.");
        return actor;
    }
    private async Task RequireCurrentStudentAsync(Actor actor, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == actor.Id && u.IsActive && u.OrganizationId == actor.OrganizationId && (db.RequestSecurityStamp == null || u.SecurityStamp == db.RequestSecurityStamp), ct) ||
            !await db.UserRoles.AnyAsync(u => u.UserId == actor.Id && db.Roles.Any(r => r.Id == u.RoleId && r.Name == RoleNames.Student), ct)) throw Hidden();
    }
    private async Task<TutoringSession> OwnedAsync(Guid id, Actor actor, bool locked, CancellationToken ct)
    {
        var query = locked ? db.TutoringSessions.FromSqlInterpolated($"SELECT * FROM \"TutoringSessions\" WHERE \"Id\"={id} AND \"StudentId\"={actor.Id} AND \"OrganizationId\"={actor.OrganizationId} FOR UPDATE")
            : db.TutoringSessions.AsNoTracking().Where(s => s.Id == id && s.StudentId == actor.Id && s.OrganizationId == actor.OrganizationId);
        var session = await query.SingleOrDefaultAsync(ct) ?? throw Hidden();
        if (locked) await RequireCurrentStudentAsync(actor, ct);
        await assignedAccess.RequireReadAsync(session, ct); return session;
    }
    public async Task<TutoringSessionView> CreateAsync(CreateTutoringRequest request, CancellationToken ct)
    {
        var actor = await StudentAsync();
        ValidateText(request.Topic, 2, 160); ValidateText(request.Goal, 2, 500);
        if (request.RequestId == Guid.Empty || request.Level is not ("Beginner" or "Intermediate" or "Advanced")) throw new AppException(400, "Solicitud de tutoría no válida.");
        if (request.Experience is not ("Tutoring" or "Diagnostic") || request.Modality is not ("Conversational" or "Prepared") || request.Length is not ("Short" or "Medium" or "Long"))
            throw new AppException(400, "Modalidad o extensión no válida.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({actor.Id.ToString()}, 0))", ct);
        await RequireCurrentStudentAsync(actor, ct);
        var existing = await db.TutoringSessions.SingleOrDefaultAsync(s => s.StudentId == actor.Id && s.CreationKey == request.RequestId, ct);
        if (existing is not null)
        {
            if (existing.Mode == TutoringMode.AssignedClass) throw Conflict("Esta clave pertenece al inicio de una clase asignada.");
            if (existing.Topic != request.Topic.Trim() || existing.Goal != request.Goal.Trim() || existing.Level != request.Level || existing.DirectReview != request.DirectReview || existing.SourceRecommendationId != request.SourceRecommendationId ||
                existing.LearningExperience != request.Experience || existing.LearningModality != request.Modality || request.Modality == "Prepared" && (await prepared.FindAsync(existing.Id, ct))?.Length != request.Length) throw Conflict("La clave de creación ya se usó con otros datos.");
            await tx.CommitAsync(ct); return await ViewAsync(existing, ct);
        }
        if (await db.TutoringSessions.CountAsync(s => s.StudentId == actor.Id, ct) >= 100) throw Conflict("Se alcanzó el límite de 100 tutorías del piloto.");
        var health = await provider.CheckHealthAsync(ct); // Configuration only, never generates.
        LearningRecommendation? source = null;
        if (request.SourceRecommendationId is { } sourceId)
        {
            source = await db.LearningRecommendations.SingleOrDefaultAsync(r => r.Id == sourceId && db.TutoringSessions.Any(s => s.Id == r.SessionId && s.StudentId == actor.Id && s.OrganizationId == actor.OrganizationId), ct) ?? throw Hidden();
            var origin = await db.TutoringSessions.SingleAsync(s => s.Id == source.SessionId, ct);
                await assignedAccess.RequireReadAsync(origin, ct);
            if (origin.State != TutoringState.Completed || request.Goal.Trim() != source.Goal || request.Topic.Trim() != origin.Topic) throw Conflict("La recomendación no corresponde a los datos de la nueva tutoría.");
        }
        var session = new TutoringSession { StudentId = actor.Id, OrganizationId = actor.OrganizationId,
            CreationKey = request.RequestId, Topic = request.Topic.Trim(), Goal = request.Goal.Trim(),
            Level = request.Level, DirectReview = request.DirectReview, PolicyVersion = TutoringOrchestrator.PolicyVersion,
            ProviderId = health.ProviderId, ModelId = health.ModelId, AiBindingId = health.BindingId, CreatedAt = clock.GetUtcNow(), LastActivityAt = clock.GetUtcNow(),
            SourceRecommendationId = source?.Id, SourceReportId = source?.ReportId,
            LearningExperience = request.Experience, LearningModality = request.Modality };
        if (request.Modality == "Prepared") { session.PolicyVersion = PreparedRules.Version; await prepared.AddAsync(session, request.Length, health, ct); }
        db.TutoringSessions.Add(session); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await ViewAsync(session, ct);
    }
    public async Task<TutoringSessionItem[]> ListAsync(int page, CancellationToken ct)
    {
        var actor = await StudentAsync();
        if (page is < 1 or > 10000) throw new AppException(400, "Página no válida.");
        return await db.TutoringSessions.AsNoTracking().Where(s => s.StudentId == actor.Id && s.OrganizationId == actor.OrganizationId && s.Mode != TutoringMode.AssignedClass)
            .OrderByDescending(s => s.LastActivityAt).ThenBy(s => s.Id).Skip((page - 1) * 50).Take(50)
            .Select(s => new TutoringSessionItem(s.Id, s.Topic, s.Goal, s.State.ToString(), s.CreatedAt, s.LastActivityAt, s.CompletedAt, s.ProviderId, s.ModelId, s.LearningExperience, s.LearningModality)).ToArrayAsync(ct);
    }
    public async Task<TutoringSessionView> GetAsync(Guid id, CancellationToken ct)
    {
        var actor = await StudentAsync(true);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
        var view = await ViewAsync(await OwnedAsync(id, actor, false, ct), ct);
        await tx.CommitAsync(ct); return view;
    }
    private static string InputText(TutoringTurnRequest request, TutoringSession session) => request.Action switch
    {
        "Start" => session.DirectReview ? "Quiero comenzar con un repaso directo del tema." : "Quiero comenzar la tutoría con una pregunta diagnóstica breve.",
        "Hint" => "Necesito una pista, sin revelar todavía la solución completa.",
        "Simplify" => "Explícalo más sencillo y retoma una sola pregunta o ejercicio.",
        "Message" or "SubmitAttempt" => request.Text!.Trim(),
        "GeneratePlan" => "Generar propuesta de plan personalizado." + (string.IsNullOrWhiteSpace(request.Text) ? "" : " Ajuste solicitado: " + request.Text.Trim()),
        _ => request.Action switch {
            "DiagnosticQuestion" => "Solicito una pregunta diagnóstica estructurada.",
            "AnalyzeDiagnosis" => "Finalizo el diagnóstico y solicito su análisis orientativo.",
            "ActivatePlan" => "Acepto el plan e inicio la tutoría personalizada aquí.",
            "NextActivity" => "Continuar con la siguiente actividad.",
            "GenerateExercise" => "Solicito un ejercicio estructurado para practicar.",
            _ => "Solicito un reporte final orientativo basado en las evidencias guardadas." }
    };
    public async Task<TutoringSessionView> SendAsync(Guid id, TutoringTurnRequest request, CancellationToken ct)
    {
        var actor = await StudentAsync(true);
        if (request.RequestId == Guid.Empty || request.Revision < 0 || !Enum.GetNames<TutoringAction>().Contains(request.Action)) throw new AppException(400, "Turno no válido.");
        if (request.Action is "Message" or "SubmitAttempt") ValidateText(request.Text, 1, 2000);
        else if (request.Action == "GeneratePlan" && request.Text is not null) ValidateText(request.Text, 1, 500);
        else if (!string.IsNullOrEmpty(request.Text)) throw new AppException(400, "Esta acción no acepta texto adicional.");
        if (request.Action == "SubmitAttempt" ? request.ExerciseId is null || request.ExerciseId == Guid.Empty : request.ExerciseId is not null)
            throw new AppException(400, "Referencia de ejercicio no válida para esta acción.");
        TutoringSession session; TutoringTurn turn; Guid lease;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            session = await OwnedAsync(id, actor, true, ct);
            await assignedAccess.RequireWorkAsync(session, ct);
            var preparedSession = PreparedRules.IsPrepared(session) ? await prepared.FindAsync(id, ct) ?? throw Hidden() : null;
            if (preparedSession is null && request.Action == "PrepareSession") throw Conflict("Esta sesión conserva su modalidad conversacional.");
            if (preparedSession is not null && request.Action is not ("PrepareSession" or "Start" or "Hint" or "Simplify" or "SubmitAttempt" or "NextActivity" or "FinalReport"))
                throw Conflict("El recorrido preparado no admite conversación libre.");
            var preloaded = await assigned.PreloadedAsync(session, ct);
            if (preloaded is not null && (request.Action == "FinalReport" ? session.State != TutoringState.Completed : request.Action is not ("Start" or "Hint" or "Simplify" or "SubmitAttempt" or "NextActivity")))
                throw Conflict("La clase precargada usa actividades y ayudas locales. La evaluación IA es opcional después de finalizar; no hay conversación libre.");
            var input = InputText(request, session);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Action + "\n" + input + (request.ExerciseId is null ? "" : "\n" + request.ExerciseId))));
            turn = (await db.TutoringTurns.SingleOrDefaultAsync(t => t.SessionId == id && t.RequestId == request.RequestId, ct))!;
            if (turn is not null)
            {
                if (turn.InputHash != hash) throw Conflict("La clave del turno ya se usó con otro contenido.");
                if (!request.Retry || turn.State is TutoringTurnState.Succeeded or TutoringTurnState.Cancelled ||
                    turn.State == TutoringTurnState.Pending && turn.LeaseExpiresAt > clock.GetUtcNow())
                { await tx.CommitAsync(ct); return await ViewAsync(session, ct); }
            }
            if (session.State == TutoringState.Paused || session.State == TutoringState.Completed && request.Action != "FinalReport") throw Conflict("La tutoría está pausada o finalizada.");
            if (session.Revision != request.Revision) throw Conflict();
            if (session.PendingRequestId.HasValue && session.PendingRequestId != request.RequestId) throw Conflict("Hay un turno pendiente. Recupéralo o cancélalo antes de otro envío.");
            if (turn is null)
            {
                if (request.Retry) throw Conflict("No existe un turno previo para reintentar.");
                if (session.State == TutoringState.Created && request.Action is not ("Start" or "DiagnosticQuestion" or "AnalyzeDiagnosis" or "PrepareSession") || session.State != TutoringState.Created && request.Action == "Start") throw Conflict("Inicia la conversación una sola vez.");
                if (preparedSession is null && preloaded is null && request.Action != "FinalReport" && session.NextSequence + 2 > TutoringOrchestrator.MaxMessages) throw Conflict("Se alcanzó el límite del historial. Finaliza esta tutoría y crea otra.");
                turn = new TutoringTurn { SessionId = id, RequestId = request.RequestId, Action = Enum.Parse<TutoringAction>(request.Action), InputHash = hash, CreatedAt = clock.GetUtcNow(), RequestText = request.Text?.Trim() };
                db.TutoringTurns.Add(turn);
                if (preparedSession is not null)
                {
                    if (turn.Action is TutoringAction.PrepareSession or TutoringAction.FinalReport) await prepared.PrepareRemoteAsync(session, preparedSession, turn, ct);
                }
                else await learning.PrepareAsync(session, turn, request, ct, preloaded is not null && request.Action != "FinalReport");
                if (preparedSession is null && preloaded is null && turn.Action != TutoringAction.FinalReport)
                    db.TutoringMessages.Add(new TutoringMessage { SessionId = id, RequestId = turn.RequestId,
                        Sequence = session.NextSequence++, Role = TutoringMessageRole.Student, Text = input, CreatedAt = clock.GetUtcNow() });
            }
            if (preparedSession is not null && request.Action is not ("PrepareSession" or "FinalReport"))
            {
                await prepared.LocalAsync(session, preparedSession, turn, request, ct); Touch(session);
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await GetAsync(id, ct);
            }
            if (preloaded is not null && request.Action != "FinalReport")
            {
                await CompleteLocalAsync(session, turn, preloaded, ct);
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await GetAsync(id, ct);
            }
            if (turn.Attempts >= 3) throw Conflict("Se agotaron los tres intentos de este turno. Cancélalo para continuar.");
            if (turn.RetryNotBefore > clock.GetUtcNow()) throw new AppException(429, "El proveedor pidió esperar. No reintentes todavía.");
            var health = await AiBinding.BindAsync(provider, session.AiBindingId, ct);
            if (health.ProviderId != session.ProviderId || health.ModelId != session.ModelId) throw Conflict("El proveedor o modelo de esta tutoría cambió. Restaura su configuración o crea otra tutoría.");
            lease = Guid.NewGuid(); turn.LeaseToken = lease; turn.LeaseExpiresAt = clock.GetUtcNow().AddSeconds(options.Value.TimeoutSeconds + 30);
            turn.State = TutoringTurnState.Pending; turn.Attempts++; turn.ErrorCode = null; turn.RetryNotBefore = null;
            session.PendingRequestId = turn.RequestId; if (session.State != TutoringState.Completed) session.State = TutoringState.Active; Touch(session);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        // No database transaction/row lock is held while waiting on the provider.
        var timer = Stopwatch.StartNew();
        try
        {
            var context = await db.TutoringMessages.AsNoTracking().Where(m => m.SessionId == id &&
                (m.RequestId == request.RequestId || db.TutoringTurns.Any(t => t.SessionId == id && t.RequestId == m.RequestId && t.State == TutoringTurnState.Succeeded)))
                .OrderBy(m => m.Sequence).ToArrayAsync(ct);
            using var generationDeadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            generationDeadline.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
            var reply = await (PreparedRules.IsPrepared(session)
                ? prepared.GenerateAsync(session, (await prepared.FindAsync(session.Id, generationDeadline.Token))!, turn, generationDeadline.Token)
                : LearningOrchestrator.IsLearning(turn.Action)
                ? learning.GenerateAsync(session, turn, generationDeadline.Token)
                : orchestrator.GenerateAsync(session, context, turn.Action, request.RequestId, generationDeadline.Token,
                    session.Mode == TutoringMode.AssignedClass ? turn.ContextJson : await learning.GuidanceAsync(id, generationDeadline.Token))).WaitAsync(generationDeadline.Token);
            using var persistenceDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await FinishAsync(id, request.RequestId, actor, lease, reply, null, null, timer.ElapsedMilliseconds, persistenceDeadline.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        { await RecordFailureAsync(id, request.RequestId, actor, lease, "Canceled", null, timer.ElapsedMilliseconds); throw; }
        catch (OperationCanceledException)
        { await RecordFailureAsync(id, request.RequestId, actor, lease, "Timeout", null, timer.ElapsedMilliseconds); }
        catch (AiProviderException error)
        { await RecordFailureAsync(id, request.RequestId, actor, lease, error.Code.ToString(), error.RetryAfter, timer.ElapsedMilliseconds); }
        catch (AppException) { throw; }
        catch (Exception)
        {
            await RecordFailureAsync(id, request.RequestId, actor, lease, "Unavailable", null, timer.ElapsedMilliseconds);
            throw new AppException(503, "No se completó la respuesta. Tu intento está guardado; actualiza el estado para recuperarlo.");
        }
        return await GetAsync(id, ct);
    }
    private async Task RecordFailureAsync(Guid id, Guid requestId, Actor actor, Guid lease, string code, TimeSpan? retry, long duration)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await FinishAsync(id, requestId, actor, lease, null, code, retry, duration, deadline.Token);
    }
    private async Task FinishAsync(Guid id, Guid requestId, Actor actor, Guid lease, TutoringReply? reply,
        string? error, TimeSpan? retry, long duration, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Finish must cancel durably even when the student's group relationship was revoked.
        var session = await db.TutoringSessions.FromSqlInterpolated($"SELECT * FROM \"TutoringSessions\" WHERE \"Id\"={id} AND \"StudentId\"={actor.Id} AND \"OrganizationId\"={actor.OrganizationId} FOR UPDATE").SingleOrDefaultAsync(ct) ?? throw Hidden();
        var turn = await db.TutoringTurns.SingleAsync(t => t.SessionId == id && t.RequestId == requestId, ct);
        if (session.AiBindingId.HasValue) await PersonalAiSession.LockAsync(db, ct);
        if (turn.LeaseToken != lease || turn.State != TutoringTurnState.Pending || session.PendingRequestId != requestId || turn.LeaseExpiresAt <= clock.GetUtcNow())
        { await tx.CommitAsync(ct); return; } // Fencing discards late results after cancel/recovery.
        var preparedAccount = PreparedRules.IsPrepared(session);
        var allowed = await db.Users.AnyAsync(u => u.Id == actor.Id && u.IsActive && u.OrganizationId == actor.OrganizationId && (!preparedAccount || db.RequestSecurityStamp == null || u.SecurityStamp == db.RequestSecurityStamp), ct)
            && await db.UserRoles.AnyAsync(u => u.UserId == actor.Id && db.Roles.Any(r => r.Id == u.RoleId && r.Name == RoleNames.Student), ct);
        turn.DurationMs = duration;
        var academicAllowed = await assignedAccess.CanWorkAsync(session, ct);
        var credentialAllowed = await PersonalAiProvider.CredentialActiveAsync(db, session.AiBindingId, actor.Id, ct);
        if (!credentialAllowed) { reply = null; error = "Authentication"; }
        var accountAllowed = allowed; allowed = allowed && academicAllowed;
        if (!allowed) { turn.State = TutoringTurnState.Cancelled; turn.ErrorCode = "Canceled"; session.PendingRequestId = null; }
        else if (reply is null)
        {
            turn.State = TutoringTurnState.Failed; turn.ErrorCode = error;
            if (retry.HasValue)
            {
                var now = clock.GetUtcNow();
                turn.RetryNotBefore = retry.Value > DateTimeOffset.MaxValue - now ? DateTimeOffset.MaxValue : now + retry.Value;
            }
        }
        else
        {
            if (PreparedRules.IsPrepared(session)) await prepared.ApplyRemoteAsync(session, (await prepared.FindAsync(id, ct))!, turn, reply, ct);
            else await learning.ApplyAsync(session, turn, reply, ct);
            if (!PreparedRules.IsPrepared(session) && turn.Action != TutoringAction.FinalReport)
                db.TutoringMessages.Add(new TutoringMessage { SessionId = id, RequestId = requestId,
                    Sequence = session.NextSequence++, Role = TutoringMessageRole.Tutor, Text = reply.Text,
                    CreatedAt = clock.GetUtcNow(), ProviderId = reply.Result.ProviderId, ModelId = reply.Result.ModelId,
                    PromptVersion = session.Mode == TutoringMode.AssignedClass ? AulaTutor.Application.Lessons.AssignedClassPolicy.Version : LearningOrchestrator.IsLearning(turn.Action) ? LearningOrchestrator.PromptVersion : session.PolicyVersion, InputTokens = reply.Result.Usage?.InputTokens, OutputTokens = reply.Result.Usage?.OutputTokens });
            turn.State = TutoringTurnState.Succeeded; turn.InputTokens = reply.Result.Usage?.InputTokens; turn.OutputTokens = reply.Result.Usage?.OutputTokens;
            session.PendingRequestId = null; if (turn.Action != TutoringAction.FinalReport) { session.Activity = reply.Activity; session.CurrentFocus = reply.Focus; }
        }
        Touch(session); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        logger.LogInformation("Tutoring call {SessionId} {RequestId} {Provider} {Model} {PolicyVersion} {DurationMs}ms {State} {InputTokens} {OutputTokens}",
            id, requestId, session.ProviderId, session.ModelId, PreparedRules.IsPrepared(session) ? turn.Action == TutoringAction.FinalReport ? PreparedRules.EvaluationVersion : PreparedRules.Version : session.Mode == TutoringMode.AssignedClass ? AulaTutor.Application.Lessons.AssignedClassPolicy.Version : LearningOrchestrator.IsLearning(turn.Action) ? LearningOrchestrator.PromptVersion : session.PolicyVersion, duration, turn.State, turn.InputTokens, turn.OutputTokens);
        if (!accountAllowed) throw new AppException(401, "La cuenta ya no tiene acceso activo de estudiante.");
        if (!academicAllowed) throw new AppException(409, "La relación o disponibilidad de la clase cambió. Se descartó la respuesta tardía y se conserva el intento.");
    }
    public async Task<TutoringSessionView> TransitionAsync(Guid id, string action, long revision, CancellationToken ct)
    {
        var actor = await StudentAsync(true);
        if (action is not ("pause" or "resume" or "complete") || revision < 0) throw new AppException(400, "Transición no válida.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var session = await OwnedAsync(id, actor, true, ct);
        if (action != "pause") await assignedAccess.RequireWorkAsync(session, ct);
        var target = action switch { "pause" => TutoringState.Paused, "resume" => TutoringState.Active, _ => TutoringState.Completed };
        if (session.State == target) { await tx.CommitAsync(ct); return await ViewAsync(session, ct); }
        if (session.State == TutoringState.Completed || session.Revision != revision) throw Conflict();
        if (action == "complete" && session.Mode == TutoringMode.AssignedClass) await assigned.EnsureCompleteAsync(session, ct);
        if (action == "complete" && PreparedRules.IsPrepared(session)) await prepared.EnsureCompleteAsync(session, (await prepared.FindAsync(id, ct))!, ct);
        if (action == "resume" && session.State != TutoringState.Paused || action == "pause" && session.State != TutoringState.Active) throw Conflict("Esta transición no está permitida en el estado actual.");
        if (session.PendingRequestId is { } pending)
        {
            var turn = await db.TutoringTurns.SingleAsync(t => t.SessionId == id && t.RequestId == pending, ct);
            if (turn.State == TutoringTurnState.Pending && turn.LeaseExpiresAt > clock.GetUtcNow()) throw Conflict("Espera o cancela la generación antes de cambiar el estado.");
            if (action == "complete") { turn.State = TutoringTurnState.Cancelled; session.PendingRequestId = null; }
        }
        session.State = target;
        if (target == TutoringState.Completed)
        {
            session.CompletedAt = clock.GetUtcNow();
            session.Summary = TutoringOrchestrator.Summarize(session, await db.TutoringTurns.Where(t => t.SessionId == id).ToArrayAsync(ct));
            if (PreparedRules.IsPrepared(session)) await prepared.LocalReportAsync(session, ct);
            await RecordPlanWorkAsync(session, ct);
            if (session.Mode == TutoringMode.AssignedClass)
            {
                var v = await db.LessonVersions.AsNoTracking().SingleAsync(v => v.Id == session.LessonVersionId, ct);
                var stats = await learning.StatisticsAsync(id, ct);
                var summary = $"Clase entregada: {session.Topic}. Versión {v.Number} ({v.Id}). Huella SHA-256: {v.ContentHash}. " +
                    $"Se registraron {stats.Attempts} intentos. {stats.NeedsReview} respuestas requieren revisión humana. " +
                    "Actividades obligatorias entregadas.";
                session.Summary = summary;
                db.LearningReports.Add(new() { SessionId = id, Version = await db.LearningReports.CountAsync(r => r.SessionId == id, ct) + 1,
                    EvidenceRevision = session.EvidenceRevision, StatisticsJson = LearningJson.Write(stats),
                    ContentJson = LearningJson.Write(new ReportContent(summary, [], ["Dominio académico y respuestas abiertas no verificados."],
                        [new RecommendationProposal("Revisa tus intentos y consulta al docente si necesitas contraste académico.", [])])),
                    ProviderId = "Application", ModelId = "deterministic", PromptVersion = "assigned-summary-v1", GeneratedAt = clock.GetUtcNow() });
            }
        }
        await RequireCurrentStudentAsync(actor, ct);
        Touch(session); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await ViewAsync(session, ct);
    }
    public async Task<TutoringSessionView> CancelAsync(Guid id, Guid requestId, long revision, CancellationToken ct)
    {
        var actor = await StudentAsync(true); await using var tx = await db.Database.BeginTransactionAsync(ct);
        var session = await OwnedAsync(id, actor, true, ct);
        var turn = await db.TutoringTurns.SingleOrDefaultAsync(t => t.SessionId == id && t.RequestId == requestId, ct) ?? throw Hidden();
        if (turn.State is TutoringTurnState.Succeeded or TutoringTurnState.Cancelled) { await tx.CommitAsync(ct); return await ViewAsync(session, ct); }
        if (revision < 0 || session.PendingRequestId != requestId) throw Conflict();
        turn.State = TutoringTurnState.Cancelled; turn.ErrorCode = "Canceled"; session.PendingRequestId = null;
        Touch(session); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await ViewAsync(session, ct);
    }
    private void Touch(TutoringSession session) { session.Revision++; session.LastActivityAt = clock.GetUtcNow(); }
    private async Task<TutoringSessionView> ViewAsync(TutoringSession s, CancellationToken ct)
    {
        var messages = await db.TutoringMessages.AsNoTracking().Where(m => m.SessionId == s.Id).OrderBy(m => m.Sequence).ToArrayAsync(ct);
        var turns = await db.TutoringTurns.AsNoTracking().Where(t => t.SessionId == s.Id).OrderBy(t => t.CreatedAt).ThenBy(t => t.RequestId).ToArrayAsync(ct);
        var assignedView = await assigned.ViewAsync(s, ct);
        return new(s.Id, s.Topic, s.Goal, s.Level, s.DirectReview, s.State.ToString(), s.Revision, s.CreatedAt, s.LastActivityAt, s.CompletedAt,
            s.ProviderId, s.ModelId, s.PolicyVersion, s.Activity, s.CurrentFocus,
            messages.Select(m => new TutoringMessageView(m.Id, m.RequestId, m.Sequence, m.Role.ToString(), m.Text, m.CreatedAt,
                m.ProviderId, m.ModelId, m.PromptVersion, m.InputTokens.HasValue && m.OutputTokens.HasValue ? new AiTokenUsage(m.InputTokens.Value, m.OutputTokens.Value, false) : null)).ToArray(),
            turns.Select(t =>
            {
                var expired = t.State == TutoringTurnState.Pending && t.LeaseExpiresAt <= clock.GetUtcNow();
                var code = expired ? "Interrupted" : t.ErrorCode;
                var error = code switch { null => null, "Canceled" => "La respuesta se canceló; el mensaje se conserva.", "Interrupted" => "La generación quedó interrumpida. Puedes recuperar el mismo turno.",
                    _ when Enum.TryParse<AiErrorCode>(code, out var parsed) => new AiProviderException(parsed).Message,
                    _ => "No se completó la respuesta. El intento está guardado." };
                return new TutoringTurnView(t.RequestId, t.Action.ToString(), t.State.ToString(), t.Attempts,
                    t.State == TutoringTurnState.Pending && !expired,
                    (t.State == TutoringTurnState.Failed || expired) && t.Attempts < 3 && (s.State == TutoringState.Active || s.State == TutoringState.Completed && t.Action == TutoringAction.FinalReport) && t.RetryNotBefore.GetValueOrDefault() <= clock.GetUtcNow() && (assignedView?.Class.Available ?? true),
                    code, error, t.RetryNotBefore, t.DurationMs, t.ExerciseId, t.RequestText);
            }).ToArray(), s.PendingRequestId, s.Summary, await learning.ViewAsync(s, ct), s.Mode.ToString(), assignedView,
            await prepared.ViewAsync(s, ct), s.LearningExperience, s.LearningModality);
    }
}

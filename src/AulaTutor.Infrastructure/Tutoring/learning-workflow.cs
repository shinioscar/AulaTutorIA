using AulaTutor.Application.Ai;
using AulaTutor.Application.Identity;
using AulaTutor.Application.Tutoring;
using AulaTutor.Domain.Tutoring;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AulaTutor.Infrastructure.Tutoring;

// All mutations run inside the same short owner-row transaction as TutoringService.
public sealed class LearningWorkflow(AulaTutorDbContext db, LearningOrchestrator engine,
    IOptions<LearningPolicy> policy, TimeProvider clock, AssignedClassWorkflow assigned)
{
    private static AppException Conflict(string text) => new(409, text);
    private Task<PersonalizedLearningPlan?> LatestPlan(Guid id, CancellationToken ct) => db.LearningPlans.Where(p => p.SessionId == id).OrderByDescending(p => p.Version).FirstOrDefaultAsync(ct);
    private Task<DiagnosticAssessment?> LatestDiagnosis(Guid id, CancellationToken ct) => db.DiagnosticAssessments.Where(p => p.SessionId == id).OrderByDescending(p => p.Version).FirstOrDefaultAsync(ct);
    public async Task PrepareAsync(TutoringSession s, TutoringTurn turn, TutoringTurnRequest request, CancellationToken ct, bool local = false)
    {
        turn.ExerciseId = request.ExerciseId;
        var assignedExercise = s.Mode == TutoringMode.AssignedClass ? await assigned.PrepareAsync(s, turn, request, ct) : null;
        if (!local && s.Mode == TutoringMode.AssignedClass && !LearningOrchestrator.IsLearning(turn.Action))
            turn.ContextJson = await assigned.GuidanceAsync(s, assignedExercise, ct);
        if (!LearningOrchestrator.IsLearning(turn.Action)) return;
        var exercises = await db.LearningExercises.Where(e => e.SessionId == s.Id).OrderBy(e => e.CreatedAt).ToArrayAsync(ct);
        var plan = await LatestPlan(s.Id, ct);
        var diagnosis = await LatestDiagnosis(s.Id, ct);
        LearningExercise? exercise = assignedExercise;
        switch (turn.Action)
        {
            case TutoringAction.DiagnosticQuestion:
                if (plan?.State == LearningPlanState.Active || diagnosis is not null) throw Conflict("El diagnóstico ya se cerró. Continúa en modo libre o con el plan.");
                if (exercises.Count(e => e.IsDiagnostic) >= policy.Value.MaxDiagnosticQuestions) throw Conflict("Se alcanzó el límite diagnóstico; analiza las evidencias disponibles.");
                var previous = exercises.LastOrDefault(e => e.IsDiagnostic);
                if (previous is not null && !await db.ExerciseAttempts.AnyAsync(a => a.ExerciseId == previous.Id, ct)) throw Conflict("Responde la pregunta diagnóstica actual o finaliza el diagnóstico.");
                break;
            case TutoringAction.AnalyzeDiagnosis:
                if (diagnosis?.EvidenceRevision == s.EvidenceRevision) throw Conflict("El diagnóstico vigente ya está generado.");
                if (plan?.State == LearningPlanState.Active) throw Conflict("El plan está activo; conserva su diagnóstico de origen.");
                break;
            case TutoringAction.GeneratePlan:
                if (diagnosis is null || diagnosis.EvidenceRevision != s.EvidenceRevision) throw Conflict("Primero genera un diagnóstico vigente.");
                if (plan?.State is LearningPlanState.Active or LearningPlanState.Completed) throw Conflict("El plan iniciado es inmutable.");
                if (plan is not null && string.IsNullOrWhiteSpace(request.Text)) throw Conflict("Ya existe una propuesta. Explica el ajuste para generar otra versión.");
                if ((plan?.Version ?? 0) >= 3) throw Conflict("Máximo tres versiones de propuesta por tutoría.");
                break;
            case TutoringAction.ActivatePlan:
                if (plan?.State != LearningPlanState.Proposed || diagnosis?.Id != plan.DiagnosticId || diagnosis.EvidenceRevision != s.EvidenceRevision) throw Conflict("No hay un plan vigente disponible para iniciar.");
                exercise = exercises.Single(e => e.PlanId == plan.Id && e.Order == 0);
                break;
            case TutoringAction.NextActivity:
                if (s.Mode == TutoringMode.AssignedClass) break;
                if (plan?.State != LearningPlanState.Active) throw Conflict("No hay plan activo.");
                var currentExercise = exercises.Single(e => e.PlanId == plan.Id && e.Order == plan.CurrentActivity);
                var attempts = await db.ExerciseAttempts.Where(a => a.ExerciseId == currentExercise.Id).ToArrayAsync(ct);
                if (!attempts.Any(a => a.Outcome is AttemptOutcome.CorrectWithHelp or AttemptOutcome.CorrectWithoutHelp or AttemptOutcome.NeedsReview or AttemptOutcome.NotEvaluable))
                    throw Conflict("Presenta una respuesta antes de avanzar; los errores verificados requieren otro intento.");
                exercise = exercises.SingleOrDefault(e => e.PlanId == plan.Id && e.Order == plan.CurrentActivity + 1);
                break;
            case TutoringAction.SubmitAttempt:
                exercise = exercises.SingleOrDefault(e => e.Id == request.ExerciseId && e.IsPresented) ?? throw new AppException(404, "Ejercicio no disponible.");
                if (exercise.PlanId.HasValue && (plan?.Id != exercise.PlanId || plan.State != LearningPlanState.Active || plan.CurrentActivity != exercise.Order)) throw Conflict("Responde la actividad vigente.");
                if (exercise.IsDiagnostic && diagnosis is not null) throw Conflict("El diagnóstico ya se cerró.");
                var number = await db.ExerciseAttempts.CountAsync(a => a.ExerciseId == exercise.Id, ct) + 1;
                if (number > 10) throw Conflict("Se alcanzaron diez intentos por ejercicio.");
                var validation = ExerciseValidators.Validate(exercise.Kind, request.Text!, LearningJson.Read<string[]>(exercise.AcceptedAnswersJson), exercise.HintCount + exercise.SimplificationCount);
                db.ExerciseAttempts.Add(new ExerciseAttempt { SessionId = s.Id, StudentId = s.StudentId, ExerciseId = exercise.Id,
                    ExerciseVersion = exercise.Version, RequestId = turn.RequestId, Answer = request.Text!.Trim(), Number = number,
                    HintsUsed = exercise.HintCount, SimplificationsUsed = exercise.SimplificationCount, Outcome = validation.Outcome, ValidationType = validation.Type,
                    Feedback = validation.Feedback, SubmittedAt = clock.GetUtcNow() });
                s.EvidenceRevision++;
                break;
            case TutoringAction.GenerateExercise:
                if (plan?.State == LearningPlanState.Active) throw Conflict("La actividad del plan sigue vigente; puedes preguntar en el chat.");
                if (exercises.Count(e => !e.IsDiagnostic && e.PlanId == null && e.ApprovedExerciseId == null) >= 10) throw Conflict("Máximo diez ejercicios libres por sesión.");
                break;
            case TutoringAction.FinalReport:
                if (await db.LearningReports.AnyAsync(r => r.SessionId == s.Id && r.EvidenceRevision == s.EvidenceRevision && (s.PolicyVersion != "preloaded-class-v1" || r.PromptVersion != "assigned-summary-v1"), ct)) throw Conflict("El reporte vigente ya existe; consultar no necesita regenerarlo.");
                var reportLimit = s.Mode == TutoringMode.AssignedClass ? 9 : 10; // Reserve the final local class report.
                if (await db.LearningReports.CountAsync(r => r.SessionId == s.Id, ct) >= reportLimit)
                    throw Conflict(s.Mode == TutoringMode.AssignedClass ? "Se alcanzó el límite de reportes IA; se reserva el reporte local de cierre de la clase." : "Máximo diez revisiones de reporte por sesión.");
                break;
        }
        if (local) return;
        if (s.PolicyVersion == "preloaded-class-v1" && turn.Action == TutoringAction.FinalReport)
        {
            var allAttempts = await db.ExerciseAttempts.AsNoTracking().Where(a => a.SessionId == s.Id).OrderBy(a => a.SubmittedAt).ThenBy(a => a.Id).ToArrayAsync(ct);
            var version = await db.LessonVersions.AsNoTracking().SingleAsync(v => v.Id == s.LessonVersionId, ct);
            var content = LearningJson.Read<AulaTutor.Application.Lessons.LessonContent>(version.ContentJson);
            var groupedEvidence = allAttempts.Select(a => new { a.Id, a.ExerciseId, a.Answer, a.Number, a.HintsUsed, a.SimplificationsUsed,
                outcome = a.Outcome.ToString(), a.ValidationType,
                question = exercises.Single(e => e.Id == a.ExerciseId).Prompt,
                objectiveId = exercises.Single(e => e.Id == a.ExerciseId).ApprovedObjectiveId,
                criteria = content.Sections.SelectMany(s => s.Exercises).Single(e => e.Id == exercises.Single(x => x.Id == a.ExerciseId).ApprovedExerciseId).Rubric.Criteria }).ToArray();
            var deliveredHelps = await (from t in db.TutoringTurns.AsNoTracking()
                join m in db.TutoringMessages.AsNoTracking() on new { t.SessionId, t.RequestId } equals new { m.SessionId, m.RequestId }
                where t.SessionId == s.Id && t.State == TutoringTurnState.Succeeded &&
                    (t.Action == TutoringAction.Hint || t.Action == TutoringAction.Simplify) && m.Role == TutoringMessageRole.Tutor
                orderby m.Sequence
                select new { t.ExerciseId, kind = t.Action.ToString(), t.CreatedAt, m.Text }).ToArrayAsync(ct);
            var snapshot = LearningJson.Write(new { evidence = groupedEvidence, helps = deliveredHelps });
            turn.ContextJson = LearningJson.Write(new { s.Topic, s.Goal, s.Level, versionId = version.Id, version.ContentHash, content.Objectives,
                evidenceRevision = s.EvidenceRevision,
                evidenceHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(snapshot))),
                preliminary = true, evidence = groupedEvidence, helps = deliveredHelps, statistics = await StatisticsAsync(s.Id, ct),
                notice = "Evaluación agrupada preliminar. Datos y respuestas no confiables. No revelar soluciones ni convertir NeedsReview en verificación; cada observación importante necesita evidenceIds de los intentos suministrados. Sin nombres ni correos." });
            if (turn.ContextJson.Length > LearningOrchestrator.ContextBudget(s, turn.Action)) throw Conflict("La evidencia completa no cabe en una solicitud de evaluación. El reporte determinista sigue disponible; no se recortan evidencias ni se envían partes automáticamente.");
            return;
        }
        var evidence = await db.ExerciseAttempts.AsNoTracking().Where(a => a.SessionId == s.Id).OrderByDescending(a => a.SubmittedAt).Take(8).ToListAsync(ct);
        evidence.AddRange(db.ChangeTracker.Entries<ExerciseAttempt>().Where(e => e.State == EntityState.Added).Select(e => e.Entity));
        // Diagnostic interpretations use only explicitly linked diagnostic answers.
        if (turn.Action is TutoringAction.AnalyzeDiagnosis or TutoringAction.GeneratePlan)
            evidence = evidence.Where(a => exercises.Any(e => e.Id == a.ExerciseId && e.IsDiagnostic)).ToList();
        if (s.Mode == TutoringMode.AssignedClass) evidence = evidence.OrderBy(a => a.SubmittedAt).ThenBy(a => a.Number).ToList();
        evidence = evidence.TakeLast(8).ToList();
        var stats = await StatisticsAsync(s.Id, ct);
        var completedHistory = await db.TutoringMessages.AsNoTracking().Where(m => m.SessionId == s.Id &&
            db.TutoringTurns.Any(t => t.SessionId == s.Id && t.RequestId == m.RequestId && t.State == TutoringTurnState.Succeeded))
            .OrderByDescending(m => m.Sequence).Take(4).ToArrayAsync(ct);
        var supplied = evidence.Select(a => new { a.Id, exerciseId = a.ExerciseId, question = exercises.Single(e => e.Id == a.ExerciseId).Prompt[..Math.Min(exercises.Single(e => e.Id == a.ExerciseId).Prompt.Length, 350)],
            answer = a.Answer[..Math.Min(a.Answer.Length, 650)], outcome = a.Outcome.ToString(), a.ValidationType,
            feedback = s.Mode == TutoringMode.AssignedClass ? a.Feedback[..Math.Min(a.Feedback.Length, 650)] : a.Feedback, a.HintsUsed }).ToArray();
        var history = completedHistory.Reverse().Select(m => m.Text[..Math.Min(m.Text.Length, 500)]).ToArray();
        var academic = s.Mode == TutoringMode.AssignedClass ? await assigned.GuidanceAsync(s, exercise, ct) : null;
        string Context() => LearningJson.Write(new { s.Topic, s.Goal, s.Level, action = turn.Action.ToString(),
            adjustment = request.Text, evidenceRevision = s.EvidenceRevision,
            preliminary = supplied.Select(e => e.exerciseId).Distinct().Count() < policy.Value.MinimumEvidence,
            evidence = supplied, statistics = stats,
            diagnosis = diagnosis is null ? null : LearningJson.Read<AssessmentContent>(diagnosis.ContentJson),
            plan = plan is null ? null : PublicPlan(plan, exercises),
            exercise = exercise is null ? null : PublicExercise(exercise),
            assignedClass = academic, history });
        var budget = s.Mode == TutoringMode.AssignedClass ? LearningOrchestrator.ContextBudget(s, turn.Action) : 28000;
        turn.ContextJson = Context();
        // Academic material stays intact; shorten older secondary context, never persisted evidence.
        while (s.Mode == TutoringMode.AssignedClass && turn.ContextJson.Length > budget && (history.Length > 0 || supplied.Length > 1))
        {
            if (history.Length > 0) history = history[1..]; else supplied = supplied[1..];
            turn.ContextJson = Context();
        }
        if (turn.ContextJson.Length > budget) throw Conflict("El contexto de aprendizaje excede el límite del piloto.");
    }
    public Task<TutoringReply> GenerateAsync(TutoringSession s, TutoringTurn turn, CancellationToken ct) => engine.GenerateAsync(s, turn, ct);
    public async Task<string?> GuidanceAsync(Guid id, CancellationToken ct)
    {
        var plan = await LatestPlan(id, ct); if (plan?.State != LearningPlanState.Active) return null;
        var exercises = await db.LearningExercises.AsNoTracking().Where(e => e.PlanId == plan.Id).ToArrayAsync(ct);
        var exercise = exercises.Single(e => e.Order == plan.CurrentActivity);
        return LearningJson.Write(new { plan = PublicPlan(plan, exercises), exercise = PublicExercise(exercise),
            notice = "Conserva esta actividad al responder dudas; no declares avance ni reveles claves de respuesta." });
    }
    public async Task ApplyAsync(TutoringSession s, TutoringTurn turn, TutoringReply reply, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        if (s.Mode == TutoringMode.AssignedClass) await assigned.ApplyAsync(s, turn, ct);
        if (!LearningOrchestrator.IsLearning(turn.Action))
        {
            if (s.Mode == TutoringMode.AssignedClass) return;
            if (turn.Action is TutoringAction.Hint or TutoringAction.Simplify)
            {
                var plan = await LatestPlan(s.Id, ct);
                var exercise = plan?.State == LearningPlanState.Active
                    ? await db.LearningExercises.SingleAsync(e => e.PlanId == plan.Id && e.Order == plan.CurrentActivity, ct)
                    : await db.LearningExercises.Where(e => e.SessionId == s.Id && e.IsPresented && !e.Completed && e.PlanId == null).OrderByDescending(e => e.CreatedAt).FirstOrDefaultAsync(ct);
                if (exercise is not null) { if (turn.Action == TutoringAction.Hint) exercise.HintCount++; else exercise.SimplificationCount++; s.EvidenceRevision++; }
            }
            return;
        }
        using var context = System.Text.Json.JsonDocument.Parse(turn.ContextJson!);
        var preliminary = context.RootElement.GetProperty("preliminary").GetBoolean();
        switch (turn.Action)
        {
            case TutoringAction.DiagnosticQuestion:
            case TutoringAction.GenerateExercise:
                var generated = NewExercise(s.Id, turn.RequestId, LearningJson.Read<ExerciseProposal>(reply.LearningPayload!), turn.Action == TutoringAction.DiagnosticQuestion, now);
                generated.IsComplementary = s.Mode == TutoringMode.AssignedClass; db.LearningExercises.Add(generated);
                s.EvidenceRevision++; break;
            case TutoringAction.AnalyzeDiagnosis:
                db.DiagnosticAssessments.Add(new DiagnosticAssessment { SessionId = s.Id, Version = (await LatestDiagnosis(s.Id, ct))?.Version + 1 ?? 1,
                    EvidenceRevision = s.EvidenceRevision, Preliminary = preliminary, ContentJson = reply.LearningPayload!, GeneratedAt = now,
                    ProviderId = s.ProviderId, ModelId = s.ModelId, PromptVersion = LearningOrchestrator.PromptVersion }); break;
            case TutoringAction.GeneratePlan:
                var diagnosis = (await LatestDiagnosis(s.Id, ct))!; var prior = await LatestPlan(s.Id, ct);
                if (prior is not null) prior.State = LearningPlanState.Superseded;
                var plan = new PersonalizedLearningPlan { SessionId = s.Id, StudentId = s.StudentId, DiagnosticId = diagnosis.Id,
                    DiagnosticVersion = diagnosis.Version, Version = (prior?.Version ?? 0) + 1, ContentJson = reply.LearningPayload!,
                    ProviderId = s.ProviderId, ModelId = s.ModelId, PromptVersion = LearningOrchestrator.PromptVersion, GeneratedAt = now };
                db.LearningPlans.Add(plan); var content = LearningJson.Read<PlanContent>(plan.ContentJson); var order = 0;
                for (var i = 0; i < content.Objectives.Length; i++) foreach (var activity in content.Objectives[i].Activities)
                { var exercise = NewExercise(s.Id, turn.RequestId, activity.Exercise, false, now); exercise.PlanId = plan.Id; exercise.ObjectiveOrder = i; exercise.Order = order++; exercise.IsPresented = false; db.LearningExercises.Add(exercise); }
                break;
            case TutoringAction.ActivatePlan:
                s.Mode = TutoringMode.Personalized;
                var active = (await LatestPlan(s.Id, ct))!; active.State = LearningPlanState.Active; active.AcceptedAt = now;
                (await db.LearningExercises.SingleAsync(e => e.PlanId == active.Id && e.Order == 0, ct)).IsPresented = true; s.EvidenceRevision++; break;
            case TutoringAction.NextActivity:
                if (s.Mode == TutoringMode.AssignedClass) break;
                var next = (await LatestPlan(s.Id, ct))!;
                (await db.LearningExercises.SingleAsync(e => e.PlanId == next.Id && e.Order == next.CurrentActivity, ct)).Completed = true;
                next.CurrentActivity++;
                var following = await db.LearningExercises.SingleOrDefaultAsync(e => e.PlanId == next.Id && e.Order == next.CurrentActivity, ct);
                if (following is null) { next.State = LearningPlanState.Completed; next.CompletedAt = now; }
                else following.IsPresented = true;
                s.EvidenceRevision++; break;
            case TutoringAction.SubmitAttempt:
                var attempt = await db.ExerciseAttempts.SingleAsync(a => a.SessionId == s.Id && a.RequestId == turn.RequestId, ct);
                attempt.Feedback += "\n\nInterpretación IA (no verificada): " + reply.Text;
                attempt.ProviderId = s.ProviderId; attempt.ModelId = s.ModelId; attempt.PromptVersion = s.Mode == TutoringMode.AssignedClass ? AulaTutor.Application.Lessons.AssignedClassPolicy.Version : LearningOrchestrator.PromptVersion;
                break;
            case TutoringAction.FinalReport:
                var report = new LearningReport { SessionId = s.Id, Version = (await db.LearningReports.CountAsync(r => r.SessionId == s.Id, ct)) + 1,
                    EvidenceRevision = s.EvidenceRevision, ContentJson = reply.LearningPayload!, StatisticsJson = LearningJson.Write(await StatisticsAsync(s.Id, ct)),
                    ProviderId = s.ProviderId, ModelId = s.ModelId, PromptVersion = s.PolicyVersion == "preloaded-class-v1" ? "preloaded-evaluation-v1" : LearningOrchestrator.PromptVersion, GeneratedAt = now };
                db.LearningReports.Add(report);
                foreach (var recommendation in LearningJson.Read<ReportContent>(report.ContentJson).Recommendations)
                    db.LearningRecommendations.Add(new LearningRecommendation { SessionId = s.Id, ReportId = report.Id, Goal = recommendation.Goal,
                        IsGeneral = preliminary || recommendation.EvidenceIds.Length == 0, EvidenceIdsJson = LearningJson.Write(recommendation.EvidenceIds) });
                break;
        }
    }
    private static LearningExercise NewExercise(Guid session, Guid request, ExerciseProposal p, bool diagnostic, DateTimeOffset now) =>
        new() { SessionId = session, OriginRequestId = request, Prompt = p.Prompt, Kind = Enum.Parse<ExerciseKind>(p.Kind),
            OptionsJson = LearningJson.Write(p.Options), AcceptedAnswersJson = LearningJson.Write(p.AcceptedAnswers), Rubric = p.Rubric,
            IsDiagnostic = diagnostic, IsPresented = true, CreatedAt = now };
    public async Task<LearningStatistics> StatisticsAsync(Guid id, CancellationToken ct)
    {
        var attempts = await db.ExerciseAttempts.AsNoTracking().Where(a => a.SessionId == id).ToArrayAsync(ct);
        var exercises = await db.LearningExercises.AsNoTracking().Where(e => e.SessionId == id && e.IsPresented).ToArrayAsync(ct);
        var plan = await LatestPlan(id, ct);
        var worked = plan is null ? [] : LearningJson.Read<PlanContent>(plan.ContentJson).Objectives
            .Where((_, i) => exercises.Any(e => e.PlanId == plan.Id && e.ObjectiveOrder == i && attempts.Any(a => a.ExerciseId == e.Id)))
            .Select(o => o.Title).ToArray();
        var session = await db.TutoringSessions.AsNoTracking().SingleAsync(s => s.Id == id, ct);
        if (session.Mode == TutoringMode.AssignedClass)
        {
            var content = LearningJson.Read<AulaTutor.Application.Lessons.LessonContent>((await db.LessonVersions.AsNoTracking().SingleAsync(v => v.Id == session.LessonVersionId, ct)).ContentJson);
            worked = content.Objectives.Where(o => exercises.Any(e => e.ApprovedObjectiveId == o.Id && attempts.Any(a => a.ExerciseId == e.Id))).Select(o => o.Text).ToArray();
        }
        // Counts of successful exercises, not counts of chat messages or repeated correct attempts.
        return new(exercises.Length, attempts.Length,
            attempts.Where(a => a.Outcome == AttemptOutcome.CorrectWithoutHelp).Select(a => a.ExerciseId).Distinct().Count(),
            attempts.Where(a => a.Outcome == AttemptOutcome.CorrectWithHelp && !attempts.Any(b => b.ExerciseId == a.ExerciseId && b.Outcome == AttemptOutcome.CorrectWithoutHelp)).Select(a => a.ExerciseId).Distinct().Count(),
            attempts.Count(a => a.Outcome == AttemptOutcome.Incorrect), attempts.Count(a => a.Outcome == AttemptOutcome.NeedsReview),
            attempts.Count(a => a.Outcome == AttemptOutcome.NotEvaluable), exercises.Sum(e => e.HintCount), worked);
    }
    public static ExerciseView PublicExercise(LearningExercise e) => new(e.Id, e.Version, e.IsDiagnostic, e.Kind.ToString(), e.Prompt,
        LearningJson.Read<string[]>(e.OptionsJson), e.Rubric, e.HintCount, e.Completed, e.ObjectiveOrder, e.Order, e.SimplificationCount,
        e.LessonVersionId, e.ApprovedExerciseId, e.ApprovedObjectiveId, e.Required, e.IsComplementary);
    public static PlanView PublicPlan(PersonalizedLearningPlan p, IReadOnlyList<LearningExercise> exercises)
    {
        var content = LearningJson.Read<PlanContent>(p.ContentJson);
        return new(p.Id, p.Version, p.DiagnosticId, p.DiagnosticVersion, p.State.ToString(), p.CurrentActivity,
            new(content.MainGoal, content.Progression, content.Objectives.Select((o, i) => new ObjectiveView(o.Title, o.EvidenceIds,
                o.Activities.Select(a => new ActivityView(a.Explanation, a.Difficulty, a.EstimatedMinutes)).ToArray(),
                exercises.Where(e => e.PlanId == p.Id && e.ObjectiveOrder == i).All(e => e.Completed) ? "Worked" :
                exercises.Any(e => e.PlanId == p.Id && e.ObjectiveOrder == i && e.IsPresented) ? "Current" : "Pending")).ToArray()), p.AcceptedAt, p.CompletedAt);
    }
    public static ReportView PublicReport(LearningReport r, TutoringSession s) => new(r.Id, r.Version, r.EvidenceRevision != s.EvidenceRevision,
        LearningJson.Read<LearningStatistics>(r.StatisticsJson), LearningJson.Read<ReportContent>(r.ContentJson), r.ProviderId, r.ModelId, r.PromptVersion, r.GeneratedAt, s.Topic, s.Goal);
    public async Task<LearningView> ViewAsync(TutoringSession s, CancellationToken ct)
    {
        var exercises = await db.LearningExercises.AsNoTracking().Where(e => e.SessionId == s.Id).OrderBy(e => e.CreatedAt).ThenBy(e => e.Order).ToArrayAsync(ct);
        var attempts = await db.ExerciseAttempts.AsNoTracking().Where(e => e.SessionId == s.Id).OrderBy(e => e.SubmittedAt).ToArrayAsync(ct);
        var diagnosis = await LatestDiagnosis(s.Id, ct); var plan = await LatestPlan(s.Id, ct);
        var reports = await db.LearningReports.AsNoTracking().Where(r => r.SessionId == s.Id).OrderByDescending(r => r.Version).ToArrayAsync(ct);
        var reportId = reports.FirstOrDefault()?.Id;
        var recommendations = await db.LearningRecommendations.AsNoTracking().Where(r => r.SessionId == s.Id && r.ReportId == reportId).ToArrayAsync(ct);
        var shares = await db.ReportShares.AsNoTracking().Where(r => r.SessionId == s.Id).Join(db.Users, sh => sh.RecipientId, u => u.Id,
            (sh, u) => new ShareView(sh.Id, sh.ReportId, sh.RecipientId, u.DisplayName, sh.GrantedAt, sh.RevokedAt)).ToArrayAsync(ct);
        return new(s.EvidenceRevision, policy.Value.MaxDiagnosticQuestions, policy.Value.MinimumEvidence,
            exercises.Where(e => e.IsPresented).Select(PublicExercise).ToArray(),
            attempts.Select(a => new AttemptView(a.Id, a.RequestId, a.ExerciseId, a.ExerciseVersion, a.Answer, a.Number, a.HintsUsed,
                a.Outcome.ToString(), a.ValidationType, a.Feedback, a.SubmittedAt, a.ProviderId, a.ModelId, a.SimplificationsUsed)).ToArray(),
            diagnosis is null ? null : new(diagnosis.Id, diagnosis.Version, diagnosis.Preliminary, diagnosis.EvidenceRevision != s.EvidenceRevision,
                LearningJson.Read<AssessmentContent>(diagnosis.ContentJson), diagnosis.ProviderId, diagnosis.ModelId, diagnosis.PromptVersion),
            plan is null ? null : PublicPlan(plan, exercises), reports.Select(r => PublicReport(r, s)).ToArray(),
            recommendations.Select(r => new RecommendationView(r.Id, r.ReportId, r.Goal, r.IsGeneral)).ToArray(), shares);
    }
}

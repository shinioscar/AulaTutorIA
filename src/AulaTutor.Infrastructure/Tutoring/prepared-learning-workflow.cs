using AulaTutor.Application.Ai;
using AulaTutor.Application.Identity;
using AulaTutor.Application.Lessons;
using AulaTutor.Application.Tutoring;
using AulaTutor.Domain.Tutoring;
using AulaTutor.Infrastructure.Ai;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AulaTutor.Infrastructure.Tutoring;

// Runs under the existing owner session lock. Remote generation stays in the orchestrator.
public sealed class PreparedLearningWorkflow(AulaTutorDbContext db, IAiProvider provider, PreparedOrchestrator engine,
    LearningWorkflow learning, TimeProvider clock)
{
    private static AppException Conflict(string message) => new(409, message);
    public Task<PreparedLearning?> FindAsync(Guid id, CancellationToken ct) => db.PreparedLearning.SingleOrDefaultAsync(p => p.SessionId == id, ct);
    public async Task<(int Context, int Output)> CapacityAsync(AiHealthResult health, CancellationToken ct)
    {
        if (health.BindingId is { } id)
        {
            var binding = db.AiInferenceBindings.Local.SingleOrDefault(b => b.Id == id) ?? await db.AiInferenceBindings.AsNoTracking().SingleAsync(b => b.Id == id, ct);
            var model = await db.PersonalAiModels.AsNoTracking().SingleOrDefaultAsync(m => m.Provider == binding.Provider && m.Id == binding.Model && m.Compatible && m.ExpiresAt > clock.GetUtcNow(), ct);
            return model is null ? (0, 0) : (model.ContextWindow, model.MaxOutputTokens);
        }
        // Conservative application ceilings for existing reviewed adapters, not claimed vendor maxima.
        if (health.ProviderId == "Mock") return (32768, 12288);
        return health.ProviderId == "Groq" && health.ModelId is "openai/gpt-oss-20b" or "openai/gpt-oss-120b" ||
            health.ProviderId == "Gemini" && health.ModelId is "gemini-2.5-flash" or "gemini-2.5-flash-lite" or "gemini-2.5-pro"
            ? (32768, 4096) : (0, 0);
    }
    public async Task<PreparedOptionsView> OptionsAsync(CancellationToken ct)
    {
        var health = await provider.CheckHealthAsync(ct); var capacity = await CapacityAsync(health, ct);
        PreparedLengthOption[] Options(string experience) => new[] { "Short", "Medium", "Long" }.Select(length =>
        {
            var context = LearningJson.Write(new { topic = new string('t', 160), goal = new string('g', 500), level = "Intermediate", experience, count = PreparedRules.Count(experience, length) });
            var output = PreparedRules.Output(experience, length);
            return new PreparedLengthOption(length, PreparedRules.Count(experience, length), output,
                PreparedRules.Fits(PreparedOrchestrator.Instructions(true, experience == "Diagnostic") + context, output, capacity.Context, capacity.Output));
        }).ToArray();
        return new(health.ProviderId, health.ModelId, Options("Tutoring"), Options("Diagnostic"), capacity.Output < 4096 ? "Este modelo no tiene capacidad suficiente conocida. Elige otro en Proveedor de IA y vuelve a consultar." : null);
    }
    public async Task AddAsync(TutoringSession s, string length, AiHealthResult health, CancellationToken ct)
    {
        var capacity = await CapacityAsync(health, ct);
        var context = GenerationContext(s, length);
        if (!PreparedRules.Fits(PreparedOrchestrator.Instructions(true, s.LearningExperience == "Diagnostic") + context,
            PreparedRules.Output(s.LearningExperience, length), capacity.Context, capacity.Output))
            throw Conflict("La extensión no cabe en la capacidad conocida del modelo. Reduce la extensión o elige otro en Proveedor de IA.");
        db.PreparedLearning.Add(new() { SessionId = s.Id, Length = length, ContextWindow = capacity.Context, MaxOutputTokens = capacity.Output });
    }
    private static string GenerationContext(TutoringSession s, string length) => LearningJson.Write(new
    { s.Topic, s.Goal, s.Level, experience = s.LearningExperience, length, count = PreparedRules.Count(s.LearningExperience, length), notice = "Datos no confiables. Preparar toda la sesión sin partes ni reparación." });
    public Task<TutoringReply> GenerateAsync(TutoringSession s, PreparedLearning p, TutoringTurn turn, CancellationToken ct) => engine.GenerateAsync(s, p, turn, ct);
    public async Task PrepareRemoteAsync(TutoringSession s, PreparedLearning p, TutoringTurn turn, CancellationToken ct)
    {
        if (turn.Action == TutoringAction.PrepareSession)
        {
            if (p.ContentJson is not null) throw Conflict("El contenido de esta sesión ya está preparado y es inmutable.");
            turn.ContextJson = GenerationContext(s, p.Length); return;
        }
        if (turn.Action != TutoringAction.FinalReport || s.State != TutoringState.Completed || p.ContentJson is null)
            throw Conflict("Genera el contenido, inicia el recorrido y finalízalo antes de solicitar la evaluación.");
        if (await db.LearningReports.AnyAsync(r => r.SessionId == s.Id && r.PromptVersion == PreparedRules.EvaluationVersion, ct))
            throw Conflict("La evaluación ya está guardada; puedes consultarla sin regenerarla.");
        var c = LearningJson.Read<PreparedContent>(p.ContentJson);
        var exercises = await db.LearningExercises.AsNoTracking().Where(e => e.SessionId == s.Id && e.PlanId == null).ToArrayAsync(ct);
        var attempts = await db.ExerciseAttempts.AsNoTracking().Where(a => a.SessionId == s.Id).OrderBy(a => a.SubmittedAt).ThenBy(a => a.Id).ToArrayAsync(ct);
        var evidence = attempts.Select(a =>
        {
            var e = exercises.Single(e => e.Id == a.ExerciseId); var activity = c.Activities[e.Order];
            return new { a.Id, a.ExerciseId, a.Answer, a.Number, a.HintsUsed, a.SimplificationsUsed, outcome = a.Outcome.ToString(),
                a.ValidationType, question = e.Prompt, objectiveId = activity.ObjectiveId, criteria = activity.Criteria };
        }).ToArray();
        var helps = await (from t in db.TutoringTurns.AsNoTracking() join m in db.TutoringMessages.AsNoTracking()
            on new { t.SessionId, t.RequestId } equals new { m.SessionId, m.RequestId }
            where t.SessionId == s.Id && t.State == TutoringTurnState.Succeeded && (t.Action == TutoringAction.Hint || t.Action == TutoringAction.Simplify) && m.Role == TutoringMessageRole.Tutor
            orderby m.Sequence select new { t.ExerciseId, kind = t.Action.ToString(), t.CreatedAt, m.Text }).ToArrayAsync(ct);
        turn.ContextJson = LearningJson.Write(new { s.Topic, s.Goal, s.Level, c.Objectives, p.ContentHash, contract = p.ContractVersion,
            evidenceRevision = s.EvidenceRevision, evidenceHash = PreparedRules.Hash(LearningJson.Write(new { evidence, helps })),
            preliminary = attempts.Select(a => a.ExerciseId).Distinct().Count() < 3, evidence, helps, statistics = await learning.StatisticsAsync(s.Id, ct) });
        if (turn.ContextJson.Length > 24000 || !PreparedRules.Fits(PreparedOrchestrator.Instructions(false, s.LearningExperience == "Diagnostic") + turn.ContextJson, 4096, p.ContextWindow, p.MaxOutputTokens))
            throw Conflict("La evidencia íntegra excede el contexto del modelo. Tu reporte local se conserva; no se recortan respuestas ni se envían partes.");
    }
    public async Task ApplyRemoteAsync(TutoringSession s, PreparedLearning p, TutoringTurn turn, TutoringReply reply, CancellationToken ct)
    {
        if (turn.Action == TutoringAction.PrepareSession)
        {
            if (p.ContentJson is not null) throw Conflict("El contenido ya está congelado.");
            var content = LearningJson.Read<PreparedContent>(reply.LearningPayload!);
            PreparedRules.Validate(content, s.LearningExperience, p.Length);
            p.ContentJson = LearningJson.Write(content); p.ContentHash = PreparedRules.Hash(p.ContentJson);
            for (var i = 0; i < content.Activities.Length; i++)
            {
                var a = content.Activities[i];
                db.LearningExercises.Add(new() { SessionId = s.Id, OriginRequestId = turn.RequestId, Order = i,
                    ObjectiveOrder = Array.FindIndex(content.Objectives, o => o.Id == a.ObjectiveId), IsDiagnostic = s.LearningExperience == "Diagnostic",
                    Kind = Enum.Parse<ExerciseKind>(a.Exercise.Kind), Prompt = a.Exercise.Prompt, Rubric = a.Exercise.Rubric,
                    OptionsJson = LearningJson.Write(a.Exercise.Options), AcceptedAnswersJson = LearningJson.Write(a.Exercise.AcceptedAnswers), CreatedAt = clock.GetUtcNow() });
            }
            s.State = TutoringState.Created; s.EvidenceRevision++; return;
        }
        var diagnostic = s.LearningExperience == "Diagnostic";
        using (var frozen = System.Text.Json.JsonDocument.Parse(turn.ContextJson!))
        { p.EvaluationJson = reply.LearningPayload!; p.EvaluationEvidenceHash = frozen.RootElement.GetProperty("evidenceHash").GetString(); }
        var evaluation = diagnostic ? LearningJson.Read<PreparedDiagnosticEvaluation>(reply.LearningPayload!) : null;
        var reportContent = evaluation?.Report ?? LearningJson.Read<PreparedEvaluation>(reply.LearningPayload!).Report;
        var report = new LearningReport { SessionId = s.Id, Version = await db.LearningReports.CountAsync(r => r.SessionId == s.Id, ct) + 1,
            EvidenceRevision = s.EvidenceRevision, ContentJson = LearningJson.Write(reportContent), StatisticsJson = LearningJson.Write(await learning.StatisticsAsync(s.Id, ct)),
            ProviderId = s.ProviderId, ModelId = s.ModelId, PromptVersion = PreparedRules.EvaluationVersion, GeneratedAt = clock.GetUtcNow() };
        db.LearningReports.Add(report);
        foreach (var rec in reportContent.Recommendations) db.LearningRecommendations.Add(new() { SessionId = s.Id, ReportId = report.Id,
            Goal = rec.Goal, EvidenceIdsJson = LearningJson.Write(rec.EvidenceIds), IsGeneral = rec.EvidenceIds.Length == 0 });
        if (evaluation is not null)
        {
            using var context = System.Text.Json.JsonDocument.Parse(turn.ContextJson!);
            var diagnosis = new DiagnosticAssessment { SessionId = s.Id, Version = 1, EvidenceRevision = s.EvidenceRevision,
                Preliminary = context.RootElement.GetProperty("preliminary").GetBoolean(), ContentJson = LearningJson.Write(evaluation.Assessment),
                ProviderId = s.ProviderId, ModelId = s.ModelId, PromptVersion = PreparedRules.EvaluationVersion, GeneratedAt = clock.GetUtcNow() };
            db.DiagnosticAssessments.Add(diagnosis);
            var plan = new PersonalizedLearningPlan { SessionId = s.Id, StudentId = s.StudentId, DiagnosticId = diagnosis.Id, DiagnosticVersion = 1,
                Version = 1, ContentJson = LearningJson.Write(evaluation.Plan.ToPlan()), ProviderId = s.ProviderId, ModelId = s.ModelId,
                PromptVersion = PreparedRules.EvaluationVersion, GeneratedAt = clock.GetUtcNow() };
            db.LearningPlans.Add(plan);
            var order = 0;
            foreach (var o in evaluation.Plan.ToPlan().Objectives.Select((value, index) => (value, index)))
                foreach (var a in o.value.Activities) db.LearningExercises.Add(new() { SessionId = s.Id, PlanId = plan.Id, Order = order++, ObjectiveOrder = o.index,
                    OriginRequestId = turn.RequestId, Prompt = a.Exercise.Prompt, Rubric = a.Exercise.Rubric, Kind = Enum.Parse<ExerciseKind>(a.Exercise.Kind),
                    OptionsJson = LearningJson.Write(a.Exercise.Options), AcceptedAnswersJson = LearningJson.Write(a.Exercise.AcceptedAnswers), CreatedAt = clock.GetUtcNow() });
        }
    }
    private Task<LearningExercise?> CurrentAsync(TutoringSession s, PreparedLearning p, CancellationToken ct) =>
        db.LearningExercises.SingleOrDefaultAsync(e => e.SessionId == s.Id && e.PlanId == null && e.Order == p.CurrentActivity, ct);
    public async Task LocalAsync(TutoringSession s, PreparedLearning p, TutoringTurn turn, TutoringTurnRequest request, CancellationToken ct)
    {
        if (p.ContentJson is null) throw Conflict("Prepara y valida el contenido antes de comenzar.");
        if (await db.TutoringTurns.CountAsync(t => t.SessionId == s.Id, ct) >= 490) throw Conflict("Se alcanzó el límite de acciones locales.");
        var c = LearningJson.Read<PreparedContent>(p.ContentJson); var e = await CurrentAsync(s, p, ct);
        var diagnostic = s.LearningExperience == "Diagnostic"; var a = e is null ? null : c.Activities[e.Order]; string text;
        switch (turn.Action)
        {
            case TutoringAction.Start:
                if (e is null) throw Conflict("No hay una actividad preparada.");
                e.IsPresented = true; text = diagnostic ? "Diagnóstico preparado sin ayudas. Cada pregunta admite una respuesta. Los resultados se muestran al finalizar." : c.Introduction; break;
            case TutoringAction.Hint:
            case TutoringAction.Simplify:
                if (diagnostic || e is null) throw Conflict("El diagnóstico preparado mantiene condiciones sin ayudas.");
                var hint = turn.Action == TutoringAction.Hint;
                var index = hint ? e.HintCount : e.SimplificationCount;
                text = PreloadedRules.Help(hint ? a!.Aids.Hints : a!.Aids.Alternatives, index);
                if (hint) e.HintCount++; else e.SimplificationCount++;
                turn.ExerciseId = e.Id; turn.ContextJson = LearningJson.Write(new { local = true, exerciseId = e.Id, helpNumber = index + 1, kind = turn.Action.ToString() }); break;
            case TutoringAction.SubmitAttempt:
                if (e is null || e.Id != request.ExerciseId || !e.IsPresented) throw Conflict("Responde únicamente la actividad actual.");
                var number = await db.ExerciseAttempts.CountAsync(x => x.ExerciseId == e.Id, ct) + 1;
                if (number > (diagnostic ? 1 : 10)) throw Conflict(diagnostic ? "Esta pregunta ya tiene una respuesta registrada." : "Se alcanzaron diez intentos para esta actividad.");
                var result = ExerciseValidators.Validate(e.Kind, request.Text!, LearningJson.Read<string[]>(e.AcceptedAnswersJson), e.HintCount + e.SimplificationCount);
                text = diagnostic ? "Respuesta registrada. Podrás consultar los resultados al finalizar." : PreloadedRules.Feedback(a!.Aids, result.Outcome.ToString());
                turn.ExerciseId = e.Id;
                db.ExerciseAttempts.Add(new() { SessionId = s.Id, StudentId = s.StudentId, ExerciseId = e.Id, RequestId = turn.RequestId,
                    ExerciseVersion = e.Version, Answer = request.Text!.Trim(), Number = number, HintsUsed = e.HintCount, SimplificationsUsed = e.SimplificationCount,
                    Outcome = result.Outcome, ValidationType = result.Type, Feedback = diagnostic ? result.Feedback : result.Feedback + "\n" + text, SubmittedAt = clock.GetUtcNow() }); break;
            case TutoringAction.NextActivity:
                if (e is null) throw Conflict("Ya recorriste todas las actividades.");
                var attempts = await db.ExerciseAttempts.Where(x => x.ExerciseId == e.Id).ToArrayAsync(ct);
                if (!attempts.Any(x => diagnostic || x.Outcome is AttemptOutcome.CorrectWithHelp or AttemptOutcome.CorrectWithoutHelp or AttemptOutcome.NeedsReview or AttemptOutcome.NotEvaluable))
                    throw Conflict("Registra una respuesta antes de avanzar. En tutoría, revisa los errores comprobados.");
                e.Completed = true; p.CurrentActivity++; var next = await CurrentAsync(s, p, ct); if (next is not null) next.IsPresented = true;
                text = next is null ? "Recorrido trabajado. Puedes finalizar y consultar tus resultados." : "Siguiente actividad disponible."; break;
            default: throw Conflict("Esta modalidad usa actividades preparadas; no admite conversación libre.");
        }
        s.State = TutoringState.Active; s.Activity = diagnostic ? "PreparedDiagnostic" : "PreparedTutoring"; s.EvidenceRevision++;
        turn.State = TutoringTurnState.Succeeded; turn.Attempts = 1;
        db.TutoringMessages.Add(new() { SessionId = s.Id, RequestId = turn.RequestId, Sequence = s.NextSequence++, Role = TutoringMessageRole.Tutor,
            Text = text, CreatedAt = clock.GetUtcNow(), ProviderId = "Application", ModelId = "prepared-local-v1", PromptVersion = PreparedRules.Version });
    }
    public async Task EnsureCompleteAsync(TutoringSession s, PreparedLearning p, CancellationToken ct)
    {
        if (p.ContentJson is null || s.State == TutoringState.Created) throw Conflict("Primero inicia el contenido preparado.");
        var exercises = await db.LearningExercises.Where(e => e.SessionId == s.Id && e.PlanId == null).ToArrayAsync(ct);
        var attempts = await db.ExerciseAttempts.Where(a => a.SessionId == s.Id).ToArrayAsync(ct);
        if (exercises.Any(e => !e.IsPresented || !attempts.Any(a => a.ExerciseId == e.Id && (s.LearningExperience == "Diagnostic" || a.Outcome is AttemptOutcome.CorrectWithHelp or AttemptOutcome.CorrectWithoutHelp or AttemptOutcome.NeedsReview or AttemptOutcome.NotEvaluable)))) throw Conflict("Hay actividades pendientes de respuesta o de revisar un error comprobado.");
        foreach (var e in exercises) e.Completed = true;
    }
    public async Task LocalReportAsync(TutoringSession s, CancellationToken ct)
    {
        var stats = await learning.StatisticsAsync(s.Id, ct);
        s.Summary = $"Recorrido finalizado: {stats.Exercises} actividades y {stats.Attempts} respuestas registradas. " +
            $"Coinciden con reglas: {stats.CorrectWithoutHelp + stats.CorrectWithHelp}. Respuestas pendientes de revisión: {stats.NeedsReview}.";
        db.LearningReports.Add(new() { SessionId = s.Id, Version = 1, EvidenceRevision = s.EvidenceRevision,
            ContentJson = LearningJson.Write(new ReportContent(s.Summary, [], ["Dominio y respuestas abiertas no verificados."], [new("Revisar los resultados y practicar los objetivos pendientes.", [])])),
            StatisticsJson = LearningJson.Write(stats), ProviderId = "Application", ModelId = "deterministic", PromptVersion = "prepared-summary-v1", GeneratedAt = clock.GetUtcNow() });
    }
    public async Task<PreparedLearningView?> ViewAsync(TutoringSession s, CancellationToken ct)
    {
        var p = await db.PreparedLearning.AsNoTracking().SingleOrDefaultAsync(p => p.SessionId == s.Id, ct); if (p is null) return null;
        var c = p.ContentJson is null ? null : LearningJson.Read<PreparedContent>(p.ContentJson);
        var e = await CurrentAsync(s, p, ct); var a = e is null || !e.IsPresented ? null : c?.Activities[e.Order];
        var delivered = await db.LearningExercises.CountAsync(e => e.SessionId == s.Id && e.PlanId == null && db.ExerciseAttempts.Any(x => x.ExerciseId == e.Id), ct);
        var diagnostic = s.LearningExperience == "Diagnostic";
        var eligible = await db.LearningExercises.CountAsync(e => e.SessionId == s.Id && e.PlanId == null && e.IsPresented &&
            db.ExerciseAttempts.Any(a => a.ExerciseId == e.Id && (diagnostic || a.Outcome == AttemptOutcome.CorrectWithHelp || a.Outcome == AttemptOutcome.CorrectWithoutHelp || a.Outcome == AttemptOutcome.NeedsReview || a.Outcome == AttemptOutcome.NotEvaluable)), ct);
        var latest = await db.TutoringTurns.AsNoTracking().Where(t => t.SessionId == s.Id && t.Action == TutoringAction.PrepareSession).OrderByDescending(t => t.CreatedAt).FirstOrDefaultAsync(ct);
        var state = c is not null ? "Ready" : latest?.State == TutoringTurnState.Pending ? latest.LeaseExpiresAt > clock.GetUtcNow() ? "Generating" : "Interrupted" : latest?.State.ToString() ?? "Requested";
        return new(state, p.Length, p.ContractVersion, p.ContentHash, p.CurrentActivity, c?.Activities.Length ?? PreparedRules.Count(s.LearningExperience, p.Length), delivered,
            e?.IsPresented == true ? e.Id : null, a is null ? null : c!.Objectives.Single(o => o.Id == a.ObjectiveId).Title,
            a?.Explanation, a?.Example, c is not null && s.State != TutoringState.Created ? c.Introduction : null,
            c is not null && eligible == c.Activities.Length && s.State != TutoringState.Created,
            a is null ? 0 : a.Aids.Hints.Length - e!.HintCount, a is null ? 0 : a.Aids.Alternatives.Length - e!.SimplificationCount);
    }
}

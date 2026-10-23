using AulaTutor.Application.Identity;
using AulaTutor.Application.Lessons;
using AulaTutor.Application.Tutoring;
using AulaTutor.Domain.Tutoring;
using Microsoft.EntityFrameworkCore;

namespace AulaTutor.Infrastructure.Tutoring;

public sealed partial class TutoringService
{
    public async Task<TutoringSessionView> SendCardAsync(Guid id, TutoringTurnRequest request, CancellationToken ct)
    {
        var actor = await StudentAsync(true); var s = await OwnedAsync(id, actor, false, ct);
        if (await assigned.PreloadedAsync(s, ct) is null) throw Hidden();
        if (request.Action == "FinalReport") throw new AppException(400, "Solicita la evaluación final mediante la acción IA explícita.");
        return await SendAsync(id, request, ct);
    }
    private async Task CompleteLocalAsync(TutoringSession s, TutoringTurn turn, LessonContent content, CancellationToken ct)
    {
        if (await db.TutoringTurns.CountAsync(t => t.SessionId == s.Id, ct) >= 500) throw Conflict("Límite de 500 acciones locales de la clase.");
        var currentExercise = await assigned.CurrentAsync(s, ct);
        var aid = currentExercise is null ? null : PreloadedRules.Aid(content, currentExercise.ApprovedExerciseId!);
        string text;
        if (turn.Action is TutoringAction.Hint or TutoringAction.Simplify)
        {
            if (currentExercise is null) throw Conflict("No hay una actividad actual.");
            var index = turn.Action == TutoringAction.Hint ? currentExercise.HintCount : currentExercise.SimplificationCount;
            text = PreloadedRules.Help(turn.Action == TutoringAction.Hint ? aid!.Hints : aid!.Alternatives, index);
            turn.ExerciseId = currentExercise.Id;
            turn.ContextJson = LearningJson.Write(new { local = true, exerciseId = currentExercise.Id, helpNumber = index + 1, kind = turn.Action.ToString() });
        }
        else if (turn.Action == TutoringAction.SubmitAttempt)
        {
            var attempt = db.ChangeTracker.Entries<ExerciseAttempt>().Single(e => e.State == EntityState.Added && e.Entity.RequestId == turn.RequestId).Entity;
            text = PreloadedRules.Feedback(aid!, attempt.Outcome.ToString());
        }
        else text = "";
        await assigned.ApplyAsync(s, turn, ct);
        if (turn.Action is TutoringAction.Start or TutoringAction.NextActivity)
        {
            var exercise = await assigned.CurrentAsync(s, ct);
            var section = content.Sections.FirstOrDefault(sec => sec.Exercises.Any(e => e.Id == exercise?.ApprovedExerciseId));
            text = (turn.Action == TutoringAction.Start ? content.Preloaded!.Introduction + "\n" : "") + (section?.Content ?? "Recorrido trabajado. Puedes finalizar al cumplir los requisitos.");
        }
        await assignedAccess.RequireWorkAsync(s, ct);
        turn.State = TutoringTurnState.Succeeded; turn.Attempts = 1;
        s.State = TutoringState.Active; s.Activity = "PreloadedExercise"; Touch(s);
        db.TutoringMessages.Add(new() { SessionId = s.Id, RequestId = turn.RequestId, Sequence = s.NextSequence++, Role = TutoringMessageRole.Tutor,
            Text = text, CreatedAt = clock.GetUtcNow(), ProviderId = "Application", ModelId = "preloaded-local-v1", PromptVersion = "preloaded-class-v1" });
    }
}

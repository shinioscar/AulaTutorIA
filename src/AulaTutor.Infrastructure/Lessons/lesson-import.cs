using AulaTutor.Application.Lessons;

namespace AulaTutor.Infrastructure.Lessons;

public sealed partial class LessonService
{
    public async Task<string> ExternalPromptAsync(LessonExternalOptions input, CancellationToken ct)
    { await TeacherAsync(); return LessonImport.Prompt(input); }

    public async Task<LessonImportPreview> PreviewImportAsync(LessonImportInput input, CancellationToken ct)
    { await TeacherAsync(); return LessonImport.Preview(input.Json); }

    public async Task<LessonView> ImportAsync(CreateImportedLessonRequest request, CancellationToken ct)
    {
        await TeacherAsync();
        // Always validate again at commit, then reuse serialized, idempotent creation and revisions.
        return await CreateCoreAsync(new(request.RequestId, LessonImport.RequireContent(request.Json)), true, ct);
    }
}

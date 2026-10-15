using AulaTutor.Application.Ai;
using AulaTutor.Application.Lessons;
using AulaTutor.Application.Tutoring;
using AulaTutor.Domain.Tutoring;

namespace AulaTutor.UnitTests;

public sealed class AssignedContextTests
{
    private sealed class CapturingProvider : IAiProvider
    {
        public string ProviderId => "Mock";
        public AiCapabilities Capabilities => new(false, false, false, false);
        public List<AiChatRequest> Requests { get; } = [];
        public Task<AiHealthResult> CheckHealthAsync(CancellationToken ct) => Task.FromResult(new AiHealthResult(true, ProviderId, "controlled"));
        public Task<AiChatResult> GenerateAsync(AiChatRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(new AiChatResult(request.UseCase.StartsWith("learning_", StringComparison.Ordinal) ? "{\"text\":\"Orientación controlada\"}" : "Orientación controlada",
                ProviderId, "controlled", null, null, "stop"));
        }
    }
    private static TutoringSession Session() => new() { Mode = TutoringMode.AssignedClass, Topic = "Tema", Goal = "Meta", ProviderId = "Mock", ModelId = "controlled" };

    [Fact]
    public async Task LargeApprovedContextReservesSpaceWithoutDroppingCoreOrCurrentQuestion()
    {
        var provider = new CapturingProvider(); var engine = new TutoringOrchestrator(provider);
        var history = Enumerable.Range(0, 10).Select(i => new TutoringMessage
        { Role = i % 2 == 0 ? TutoringMessageRole.Student : TutoringMessageRole.Tutor, Text = new string('h', 3000) }).ToList();
        var current = "Duda actual " + new string('c', 1900); history.Add(new() { Role = TutoringMessageRole.Student, Text = current });
        var core = "NÚCLEO APROBADO " + new string('a', 20000);
        await engine.GenerateAsync(Session(), history, TutoringAction.Message, Guid.NewGuid(), CancellationToken.None, core);
        var request = Assert.Single(provider.Requests);
        Assert.InRange(request.Messages.Sum(m => m.Text.Length), 1, 32000);
        Assert.Equal(core, request.Messages.Last().Text);
        Assert.Contains(request.Messages, m => m.Text == current);
        Assert.Contains(AssignedClassPolicy.Instructions, request.Messages.First().Text);
        Assert.Equal(AssignedClassPolicy.Version, request.PromptVersion);
    }

    [Fact]
    public async Task LearningContextFitsTransportBudgetAndRejectsOversizeBeforeCallingProvider()
    {
        var provider = new CapturingProvider(); var engine = new LearningOrchestrator(provider); var session = Session();
        var baseContext = LearningJson.Write(new { evidence = Array.Empty<object>(), preliminary = true, padding = "" });
        var padding = new string('x', LearningOrchestrator.ContextBudget(session, TutoringAction.SubmitAttempt) - baseContext.Length);
        var context = LearningJson.Write(new { evidence = Array.Empty<object>(), preliminary = true, padding });
        var turn = new TutoringTurn { Action = TutoringAction.SubmitAttempt, RequestId = Guid.NewGuid(), ContextJson = context };
        await engine.GenerateAsync(session, turn, CancellationToken.None);
        Assert.InRange(Assert.Single(provider.Requests).Messages.Sum(m => m.Text.Length), 1, 32000);
        turn.ContextJson = context + " ";
        var error = await Assert.ThrowsAsync<AiProviderException>(() => engine.GenerateAsync(session, turn, CancellationToken.None));
        Assert.Equal(AiErrorCode.InvalidRequest, error.Code); Assert.Single(provider.Requests);
    }
}

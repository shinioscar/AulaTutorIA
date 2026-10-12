using AulaTutor.Application.Ai;
using AulaTutor.Infrastructure;
using AulaTutor.Infrastructure.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AulaTutor.UnitTests;

public sealed class MockAiProviderTests
{
    private static AiChatRequest Request(string format = "text") => new(
        "foundation_smoke", [new AiMessage("user", "fictitious input")],
        new AiResponseContract(format), "smoke-0");

    [Fact]
    public async Task StubDoesNotEchoInputOrClaimTokenUsage()
    {
        var provider = new MockAiProvider();
        var result = await provider.GenerateAsync(Request(), CancellationToken.None);
        Assert.Equal("Mock", result.ProviderId);
        Assert.Equal(MockAiProvider.ModelId, result.ModelId);
        Assert.DoesNotContain("fictitious input", result.Text);
        Assert.Null(result.Usage);
        Assert.Null(result.StructuredPayload);
    }

    [Fact]
    public async Task CancellationIsHonoredForGenerationAndHealth()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var provider = new MockAiProvider();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.GenerateAsync(Request(), cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.CheckHealthAsync(cts.Token));
    }

    [Fact]
    public async Task UnsupportedStructuredOutputIsRejected()
    {
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            new MockAiProvider().GenerateAsync(Request("json"), CancellationToken.None));
    }

    [Theory]
    [InlineData("Groq")]
    [InlineData("Gemini")]
    [InlineData("Ollama")]
    [InlineData("OpenAI")]
    [InlineData("")]
    public void UnimplementedProviderCannotSilentlyFallBack(string provider)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Ai:Provider"] = provider }).Build();
        using var services = new ServiceCollection().AddInfrastructure(config).BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() =>
            services.GetRequiredService<IOptions<AiProviderOptions>>().Value);
    }
}

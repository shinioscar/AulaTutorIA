using System.Text.RegularExpressions;

namespace AulaTutor.Infrastructure.Ai;

public sealed class AiProviderOptions
{
    public const string SectionName = "Ai";
    public string Provider { get; set; } = "Mock";
    public RemoteAiOptions Groq { get; set; } = new();
    public RemoteAiOptions Gemini { get; set; } = new();
    public OllamaOptions Ollama { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxConcurrency { get; set; } = 2;
    public int MaxRetries { get; set; } = 0;
    public int MaxRetryDelaySeconds { get; set; } = 5;
    public bool IsValid()
    {
        if (TimeoutSeconds is < 1 or > 120 || MaxConcurrency is < 1 or > 8 ||
            MaxRetries is < 0 or > 1 || MaxRetryDelaySeconds is < 1 or > 10) return false;
        return Provider switch
        {
            "Mock" => true,
            "Groq" => ValidRemote(Groq),
            "Gemini" => ValidRemote(Gemini),
            "Ollama" => ValidModel(Ollama.Model) && ValidLocalUri(Ollama.BaseUrl),
            _ => false
        };
    }
    private static bool ValidRemote(RemoteAiOptions o) => ValidModel(o.Model) &&
        o.ApiKey.Length is >= 8 and <= 512 && !o.ApiKey.Any(char.IsWhiteSpace) && !o.ApiKey.Any(char.IsControl);
    private static bool ValidModel(string model) => Regex.IsMatch(model, @"\A[a-zA-Z0-9][a-zA-Z0-9._:/-]{0,119}\z") && !model.Contains("..");
    public static bool ValidLocalUri(string url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo) &&
        uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) &&
        uri.Host is "localhost" or "127.0.0.1" or "[::1]" or "host.docker.internal";
}
public sealed class RemoteAiOptions
{
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "";
}
public sealed class OllamaOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "";
}

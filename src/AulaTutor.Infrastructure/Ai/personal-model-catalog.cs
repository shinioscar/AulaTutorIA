using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AulaTutor.Application.Ai;
using AulaTutor.Application.Identity;
using AulaTutor.Domain.Identity;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AulaTutor.Infrastructure.Ai;

public sealed class PersonalModelCatalog(AulaTutorDbContext db, IHttpClientFactory clients)
{
    public static bool ValidId(string? id) => id is { Length: > 0 and <= 120 } && Regex.IsMatch(id, @"\A[a-zA-Z0-9][a-zA-Z0-9._:/-]*\z") && !id.Contains("..");
    public async Task<PersonalAiModel[]> GetAsync(string provider, bool refresh, CancellationToken ct)
    {
        if (provider is not ("OpenRouter" or "Gemini" or "Groq" or "Anthropic")) throw new AppException(400, "Proveedor no disponible.");
        var now = DateTimeOffset.UtcNow;
        var stored = await db.PersonalAiModels.AsNoTracking().Where(m => m.Provider == provider && m.ExpiresAt > now).OrderByDescending(m => m.Free).ThenBy(m => m.Name).ToArrayAsync(ct);
        if (stored.Length > 0 && stored.All(m => m.ExpiresAt > now) && (!refresh || stored.Max(m => m.CheckedAt) > now.AddMinutes(-1))) return stored;
        PersonalAiModel[] models;
        if (provider == "OpenRouter")
        {
            try
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(10));
                using var client = clients.CreateClient("AiMetadata");
                using var response = await client.GetAsync("https://openrouter.ai/api/v1/models", HttpCompletionOption.ResponseHeadersRead, deadline.Token);
                if (!response.IsSuccessStatusCode) throw new AppException(503, "No se pudo actualizar el catálogo. Inténtalo más tarde.");
                using var json = await ReadBoundedAsync(response.Content, 4_000_000, deadline.Token);
                models = ParseOpenRouter(json.RootElement, now);
            }
            catch (Exception e) when (e is HttpRequestException or IOException or JsonException or InvalidOperationException or FormatException or KeyNotFoundException)
            { throw new AppException(503, "No se pudo actualizar el catálogo. Inténtalo más tarde."); }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new AppException(503, "El catálogo tardó demasiado. Vuelve a intentarlo."); }
        }
        else models = LocalModels(provider);
        await using var tx = await db.Database.BeginTransactionAsync(ct); await PersonalAiSession.LockAsync(db, ct);
        var existing = await db.PersonalAiModels.Where(m => m.Provider == provider).ToArrayAsync(ct);
        foreach(var old in existing) old.ExpiresAt = now;
        foreach(var model in models)
        {
            var row = existing.SingleOrDefault(m => m.Id == model.Id);
            if (row is null) db.PersonalAiModels.Add(model);
            else db.Entry(row).CurrentValues.SetValues(model);
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return models.OrderByDescending(m => m.Free).ThenBy(m => m.Name).ToArray();
    }
    public async Task<PersonalAiModel> RequireStoredAsync(string provider, string? id, bool paidAccepted, CancellationToken ct)
    {
        if (!ValidId(id)) throw new AppException(400, "Selecciona un modelo del catálogo.");
        var model = await db.PersonalAiModels.AsNoTracking().SingleOrDefaultAsync(m => m.Provider == provider && m.Id == id, ct);
        if (model is null || !model.Compatible) throw new AppException(409, "Este modelo no es compatible. Elige otro del catálogo.");
        if (model.ExpiresAt <= DateTimeOffset.UtcNow) throw new AppException(409, "Actualiza el catálogo antes de continuar.");
        if (!model.Free && !paidAccepted) throw new AppException(400, "Autoriza los posibles cargos de este modelo antes de activarlo.");
        return model;
    }
    public static PersonalAiModel[] ParseOpenRouter(JsonElement root, DateTimeOffset now)
    {
        var result = new List<PersonalAiModel>();
        foreach(var m in root.GetProperty("data").EnumerateArray().Take(3000))
        {
            var id = m.GetProperty("id").GetString(); if (!ValidId(id)) continue;
            var name = m.TryGetProperty("name", out var n) ? n.GetString() : id;
            if (name is null || name.Length > 200 || name.Any(char.IsControl)) continue;
            var parameters = m.TryGetProperty("supported_parameters", out var p) && p.ValueKind == JsonValueKind.Array ? p.EnumerateArray().Select(x => x.GetString()).ToArray() : [];
            // Unknown limits must disable only this model, not fail the entire catalog.
            var context = m.TryGetProperty("context_length", out var c) && c.ValueKind == JsonValueKind.Number && c.TryGetInt32(out var length) ? length : 0;
            var output = m.TryGetProperty("top_provider", out var top) && top.ValueKind == JsonValueKind.Object && top.TryGetProperty("max_completion_tokens", out var o) && o.ValueKind == JsonValueKind.Number && o.TryGetInt32(out var max) ? max : 0;
            var text = m.TryGetProperty("architecture", out var arch) && arch.TryGetProperty("output_modalities", out var modes) && modes.ValueKind == JsonValueKind.Array &&
                modes.GetArrayLength() > 0 && modes.EnumerateArray().All(x => x.GetString() == "text");
            var textInput = arch.ValueKind == JsonValueKind.Object && arch.TryGetProperty("input_modalities", out var input) && input.ValueKind == JsonValueKind.Array && input.EnumerateArray().Any(x => x.GetString() == "text");
            var retired = m.TryGetProperty("expiration_date", out var expiry) && expiry.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(expiry.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var retiredAt) && retiredAt <= now;
            var known = false; var free = false;
            if (m.TryGetProperty("pricing", out var price) && price.ValueKind == JsonValueKind.Object && price.TryGetProperty("prompt", out _) && price.TryGetProperty("completion", out _))
            {
                var values = price.EnumerateObject().Select(x => decimal.TryParse(x.Value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value >= 0 ? (decimal?)value : null).ToArray();
                known = values.Length > 0 && values.All(x => x.HasValue); free = known && values.All(x => x == 0);
            }
            var compatible = text && textInput && !retired && context >= 32768 && output >= 4096 && parameters.Contains("response_format") && parameters.Contains("structured_outputs") &&
                !id!.StartsWith("openrouter/", StringComparison.Ordinal) && !id.EndsWith(":nitro", StringComparison.Ordinal) && !id.EndsWith(":floor", StringComparison.Ordinal);
            result.Add(new() { Provider = "OpenRouter", Id = id!, Name = name, Compatible = compatible, Free = free, PriceKnown = known,
                StrictSchema = compatible, Availability = retired ? "Retired" : "Metadata", CheckedAt = now, ExpiresAt = now.AddMinutes(15) });
        }
        return result.DistinctBy(m => m.Id).ToArray();
    }
    private static PersonalAiModel[] LocalModels(string provider)
    {
        var reviewed = new DateTimeOffset(2026, 10, 23, 0, 0, 0, TimeSpan.Zero);
        var models = provider switch {
            "Groq" => new[] { "openai/gpt-oss-20b", "openai/gpt-oss-120b" },
            "Gemini" => new[] { "gemini-3.8-flash", "gemini-2.5-flash", "gemini-2.5-flash-lite", "gemini-2.5-pro" },
            "Anthropic" => new[] { "claude-opus-5-5" }, _ => [] };
        return models.Select(id => new PersonalAiModel { Provider = provider, Id = id, Name = id, Compatible = true, Free = false, PriceKnown = false,
            StrictSchema = provider != "Gemini", Availability = "ReviewedCatalog", CheckedAt = reviewed, ExpiresAt = reviewed.AddDays(30) }).ToArray();
    }
    internal static async Task<JsonDocument> ReadBoundedAsync(HttpContent content, int limit, CancellationToken ct)
    {
        if (content.Headers.ContentLength > limit) throw new JsonException();
        await using var stream = await content.ReadAsStreamAsync(ct); using var buffer = new MemoryStream(); var bytes = new byte[8192]; int read;
        while((read = await stream.ReadAsync(bytes, ct)) > 0) { if (buffer.Length + read > limit) throw new JsonException(); buffer.Write(bytes, 0, read); }
        return JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 48 });
    }
}

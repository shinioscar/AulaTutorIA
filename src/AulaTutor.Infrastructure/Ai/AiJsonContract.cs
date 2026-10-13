using System.Text.Json;
using AulaTutor.Application.Ai;

namespace AulaTutor.Infrastructure.Ai;

// Portable subset only: reject unknown keywords rather than promise full JSON Schema.
internal static class AiJsonContract
{
    public static JsonElement? ValidateSchema(AiResponseContract contract)
    {
        if (contract.Format != "json-schema")
        {
            if (contract.JsonSchema is not null || contract.Strict) throw new AiProviderException(AiErrorCode.InvalidRequest);
            return null;
        }
        try
        {
            if (contract.JsonSchema is null || contract.JsonSchema.Length > 16000) throw new JsonException();
            using var doc = JsonDocument.Parse(contract.JsonSchema, new JsonDocumentOptions { MaxDepth = 16 });
            CheckSchema(doc.RootElement, contract.Strict);
            if (doc.RootElement.GetProperty("type").GetString() != "object") throw new JsonException();
            return doc.RootElement.Clone();
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException)
        { throw new AiProviderException(AiErrorCode.InvalidRequest); }
    }
    private static void CheckSchema(JsonElement schema, bool strict)
    {
        if (schema.ValueKind != JsonValueKind.Object) throw new JsonException();
        foreach (var property in schema.EnumerateObject())
            if (property.Name is not ("type" or "properties" or "required" or "additionalProperties" or "items" or "enum" or "description")) throw new JsonException();
        var type = schema.GetProperty("type").GetString();
        if (type is not ("object" or "array" or "string" or "integer" or "number" or "boolean" or "null")) throw new JsonException();
        if (schema.TryGetProperty("description", out var description) && description.ValueKind != JsonValueKind.String) throw new JsonException();
        if (schema.TryGetProperty("enum", out var choices) && (choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)) throw new JsonException();
        if (type == "object")
        {
            var props = schema.GetProperty("properties");
            if (props.ValueKind != JsonValueKind.Object || props.EnumerateObject().Count() > 64) throw new JsonException();
            var required = schema.GetProperty("required");
            if (required.ValueKind != JsonValueKind.Array) throw new JsonException();
            var names = required.EnumerateArray().Select(x => x.GetString() ?? throw new JsonException()).ToArray();
            if (names.Distinct().Count() != names.Length || names.Any(x => !props.TryGetProperty(x, out _))) throw new JsonException();
            if (schema.GetProperty("additionalProperties").ValueKind != JsonValueKind.False) throw new JsonException();
            if (strict && props.EnumerateObject().Any(x => !names.Contains(x.Name))) throw new JsonException();
            foreach (var p in props.EnumerateObject()) CheckSchema(p.Value, strict);
        }
        else if (schema.TryGetProperty("properties", out _) || schema.TryGetProperty("required", out _) || schema.TryGetProperty("additionalProperties", out _)) throw new JsonException();
        if (type == "array") CheckSchema(schema.GetProperty("items"), strict);
        else if (schema.TryGetProperty("items", out _)) throw new JsonException();
    }
    public static void ValidateOutput(string text, string format, JsonElement? schema)
    {
        if (format == "text") return;
        try
        {
            using var doc = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 32 });
            if (schema.HasValue) Match(doc.RootElement, schema.Value);
            else if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException();
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException)
        { throw new AiProviderException(AiErrorCode.InvalidResponse); }
    }
    private static void Match(JsonElement value, JsonElement schema)
    {
        var valid = schema.GetProperty("type").GetString() switch
        {
            "object" => value.ValueKind == JsonValueKind.Object,
            "array" => value.ValueKind == JsonValueKind.Array,
            "string" => value.ValueKind == JsonValueKind.String,
            "number" => value.ValueKind == JsonValueKind.Number,
            "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
            "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
            "null" => value.ValueKind == JsonValueKind.Null,
            _ => false
        };
        if (!valid) throw new JsonException();
        if (schema.TryGetProperty("enum", out var choices) && !choices.EnumerateArray().Any(x => JsonElement.DeepEquals(x, value))) throw new JsonException();
        if (value.ValueKind == JsonValueKind.Object)
        {
            var props = schema.GetProperty("properties");
            foreach (var required in schema.GetProperty("required").EnumerateArray())
                if (!value.TryGetProperty(required.GetString()!, out _)) throw new JsonException();
            foreach (var p in value.EnumerateObject())
            {
                if (!props.TryGetProperty(p.Name, out var child)) throw new JsonException();
                Match(p.Value, child);
            }
        }
        if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) Match(item, schema.GetProperty("items"));
    }
}

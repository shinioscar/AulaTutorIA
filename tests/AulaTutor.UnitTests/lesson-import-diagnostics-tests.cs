using System.Text.Json.Nodes;
using AulaTutor.Application.Lessons;
using AulaTutor.Application.Tutoring;

namespace AulaTutor.UnitTests;

public sealed class LessonImportDiagnosticsTests
{
    private static LessonImportV2 Example()
    {
        var lesson = new LessonDesignPayload("Borrador", "Clase ficticia", "Tema", "", "", "Intermedio", 40,
            "Practicar", "Revisar", [new("o1", "Explicar")],
            Enumerable.Range(1, 4).Select(i => new LessonDesignSection($"s{i}", "Paso", "Texto")).ToArray(),
            Enumerable.Range(1, 4).Select(i => new LessonDesignExercise($"e{i}", $"s{i}", "o1", true, "Code",
                "Explica", [], [], "Revisión humana", ["Razona"], "Referencia privada", "Orientar")).ToArray());
        return new(PreloadedRules.Format, lesson, new("Introducción",
            lesson.Exercises.Select(e => new PreloadedExercise(e.Id, ["Relaciona los conceptos"], ["Otro ejemplo"],
                "Registrado", "Revisa", "Pendiente de revisión", [], [])).ToArray()));
    }

    private static int Size(LessonImportV2 source) => LearningJson.Write(source.Lesson.ToProposal().Content with { Preloaded = source.Preloaded }).Length;

    [Theory]
    [InlineData(19999)] [InlineData(20000)] [InlineData(20038)]
    public void InternalEscapedSizeBoundaryIsExplainedWithoutChangingLimit(int target)
    {
        var source = Example();
        source.Lesson.Sections[0] = source.Lesson.Sections[0] with { Body = new string('á', 2000) };
        for (var i = 1; i < source.Lesson.Sections.Length && Size(source) < target; i++)
        {
            var section = source.Lesson.Sections[i];
            source.Lesson.Sections[i] = section with { Body = section.Body + new string('x', Math.Min(target - Size(source), 2500 - section.Body.Length)) };
        }
        Assert.Equal(target, Size(source));
        var result = LessonImport.Preview(LearningJson.Write(source));
        if (target <= 20000) { Assert.Empty(result.Errors); Assert.NotNull(result.Content); }
        else
        {
            Assert.Null(result.Content); var message = Assert.Single(result.Errors);
            Assert.Contains("20038", message); Assert.Contains("máximo 20000", message); Assert.Contains("exceso: 38", message);
            Assert.Contains("escapes Unicode", message); Assert.Contains(message, result.CorrectionInstructions);
            Assert.DoesNotContain("identificadores únicos", message);
        }
    }

    [Theory]
    [InlineData("title", "lesson.title", "160")]
    [InlineData("criteria", "criteria", "300")]
    [InlineData("hintRules", "hintRules", "600")]
    [InlineData("section", "lesson.sections[0].body", "2500")]
    [InlineData("aid", "preloaded.exercises[0].hints[0]", "600")]
    [InlineData("missing-aid", "preloaded.exercises", "una entrada")]
    [InlineData("orphan-aid", "preloaded.exercises[0].exerciseId", "existente")]
    [InlineData("orphan-section", "lesson.exercises[0].sectionId", "existente")]
    [InlineData("orphan-objective", "objectiveId", "existente")]
    [InlineData("duplicate-id", "id", "repetido")]
    [InlineData("answer", "acceptedAnswers", "idéntica")]
    [InlineData("first-hint", "preloaded.exercises[0].hints[0]", "privada literal")]
    public void V2ReturnsActionableSafeFieldDiagnostics(string problem, string field, string detail)
    {
        var root = JsonNode.Parse(LearningJson.Write(Example()))!;
        var lesson = root["lesson"]!; var exercise = lesson["exercises"]![0]!; var aid = root["preloaded"]!["exercises"]![0]!;
        switch (problem)
        {
            case "title": lesson["title"] = new string('x', 161); break;
            case "criteria": exercise["criteria"] = new JsonArray(new string('x', 301)); break;
            case "hintRules": exercise["hintRules"] = new string('x', 601); break;
            case "section": lesson["sections"]![0]!["body"] = new string('x', 2501); break;
            case "aid": aid["hints"] = new JsonArray(new string('x', 601)); break;
            case "missing-aid": root["preloaded"]!["exercises"] = new JsonArray(); break;
            case "orphan-aid": aid["exerciseId"] = "hostile-private-content"; break;
            case "orphan-section": exercise["sectionId"] = "hostile-private-content"; break;
            case "orphan-objective": exercise["objectiveId"] = "hostile-private-content"; break;
            case "duplicate-id": exercise["id"] = "s1"; break;
            case "answer":
            case "first-hint":
                exercise["kind"] = "MultipleChoice"; exercise["options"] = new JsonArray("Clave privada", "Otra");
                exercise["acceptedAnswers"] = new JsonArray(problem == "answer" ? "hostile-private-content" : "Clave privada");
                if (problem == "first-hint") aid["hints"] = new JsonArray("La respuesta es Clave privada");
                break;
        }
        var result = LessonImport.Preview(root.ToJsonString());
        Assert.Null(result.Content); var message = Assert.Single(result.Errors);
        Assert.Contains(field, message); Assert.Contains(detail, message);
        Assert.DoesNotContain("hostile-private-content", result.CorrectionInstructions);
        Assert.DoesNotContain("Clave privada", result.CorrectionInstructions);
        Assert.Contains("aulatutor-lesson-v2", result.CorrectionInstructions);
    }

    [Fact]
    public void V1AlsoRetainsSafeFieldDiagnostics()
    {
        var source = Example().Lesson with { Title = new string('x', 161) };
        var result = LessonImport.Preview(LearningJson.Write(new LessonImportV1(LessonImport.Format, source)));
        Assert.Null(result.Content); Assert.Contains("lesson.title", Assert.Single(result.Errors));
    }

    [Fact]
    public void BothPromptsIncludeActualFieldAndEscapedSizeConstraints()
    {
        foreach (var preloaded in new[] { false, true })
        {
            var prompt = LessonImport.Prompt(new("Tema", "Asignatura", "Intermedio", "", 4, "Gradual", "Variados", 2, 40, "", "", preloaded));
            Assert.Contains("title/subject", prompt); Assert.Contains("referenceSolution 1500", prompt);
            Assert.Contains("hintRules 600", prompt); Assert.Contains("32000", prompt); Assert.Contains("20000", prompt);
            Assert.Contains("escapes Unicode", prompt); Assert.Contains("No basta contar el JSON visible", prompt);
            Assert.Contains("12000", prompt); Assert.Contains(preloaded ? PreloadedRules.Schema : LessonImport.Schema, prompt);
        }
    }
}

using AulaTutor.Application.Lessons;
using AulaTutor.Application.Tutoring;
using System.Text.Json.Nodes;

namespace AulaTutor.UnitTests;

public sealed class LessonImportTests
{
    private static LessonImportV1 Example() => new(LessonImport.Format,
        new("Revisar", "Fracciones", "Matemáticas", "Datos ficticios", "", "Básico", 40, "Explicar y practicar", "",
            [new("o1", "Comparar fracciones")], [new("s1", "Inicio", "Explicación"), new("s2", "Práctica", "Comparación")],
            [new("e1", "s2", "o1", true, "TrueFalse", "¿1/2 es igual a 2/4?", ["Verdadero", "Falso"], ["Verdadero"],
                "Comparar", ["Justificar"], "Sí, al multiplicar por dos", "Compara dibujos antes de dar la solución")]));
    [Fact] public void PromptIsDeterministicAndIncludesAllOptionsAndSharedSchema()
    {
        var o = new LessonExternalOptions("Fracciones", "Matemáticas", "Básico", "Comparar", 3, "Gradual", "Variados", 2, 40, "Dibujos", "Sin notas");
        var prompt = LessonImport.Prompt(o); Assert.Equal(prompt, LessonImport.Prompt(o));
        Assert.Contains(LearningJson.Write(o), prompt); Assert.Contains(LessonImport.Schema, prompt);
        Assert.Contains("sin Markdown", prompt); Assert.Contains("no certifica", prompt);
    }
    [Fact] public void ValidImportPreservesOrderedSectionsAndPrivateContent()
    {
        var source = Example(); var result = LessonImport.Preview(LearningJson.Write(source)); Assert.Empty(result.Errors);
        Assert.Equal(LearningJson.Write(source.Lesson.ToProposal().Content), LearningJson.Write(result.Content));
        Assert.Equal("s1", result.Content!.Sections[0].Id); Assert.Equal("s2", result.Content.Sections[1].Id);
    }
    [Theory]
    [InlineData("invalid")][InlineData("version")][InlineData("missing")][InlineData("duplicate-property")]
    [InlineData("duplicate-id")][InlineData("orphan")][InlineData("options")][InlineData("solution")]
    [InlineData("unknown-kind")][InlineData("objectives")][InlineData("owner")][InlineData("depth")]
    [InlineData("size")][InlineData("null")][InlineData("rubric")][InlineData("hints")]
    public void InvalidImportReturnsSafeErrorsAndCorrection(string problem)
    {
        var root = JsonNode.Parse(LearningJson.Write(Example()))!; var lesson = root["lesson"]!; var exercise = lesson["exercises"]![0]!;
        switch(problem)
        {
            case "version": root["format"]="other"; break;
            case "missing": lesson.AsObject().Remove("title"); break;
            case "duplicate-id": exercise["id"]="s1"; break;
            case "orphan": exercise["sectionId"]="missing"; break;
            case "options": exercise["options"]=new JsonArray("Falso","Falso"); break;
            case "solution": exercise["acceptedAnswers"]=new JsonArray("otra"); break;
            case "unknown-kind": exercise["kind"]="ExecuteSQL"; break;
            case "objectives": lesson["objectives"]=new JsonArray(); break;
            case "owner": root["teacherId"]="hostile"; break;
            case "null": exercise["options"]=null; break;
            case "rubric": exercise["criteria"]=new JsonArray(); break;
            case "hints": exercise["hintRules"]=""; break;
        }
        var json = problem switch { "invalid" => "```json\n{}", "size" => new string('x',32001),
            "depth" => new string('[',20)+"0"+new string(']',20), "duplicate-property" => root.ToJsonString().Replace("\"format\":", "\"format\":\"duplicate\",\"format\":"), _ => root.ToJsonString() };
        var result=LessonImport.Preview(json);Assert.Null(result.Content);Assert.NotEmpty(result.Errors);
        Assert.Contains("conservando",result.CorrectionInstructions); Assert.DoesNotContain("hostile",result.CorrectionInstructions);
    }
    [Fact] public void MissingAnswerHasNumberedHumanMessage()
    {var wire=Example();wire.Lesson.Exercises[0]=wire.Lesson.Exercises[0] with {AcceptedAnswers=[]};Assert.Contains("El ejercicio 1 no tiene respuesta esperada.",LessonImport.Preview(LearningJson.Write(wire)).Errors);}
}

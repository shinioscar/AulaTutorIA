using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AulaTutor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PreparedLearning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TutoringTurn_Action",
                table: "TutoringTurns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TutoringSession_Sequence",
                table: "TutoringSessions");

            migrationBuilder.AddColumn<string>(
                name: "LearningExperience",
                table: "TutoringSessions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Tutoring");

            migrationBuilder.AddColumn<string>(
                name: "LearningModality",
                table: "TutoringSessions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Conversational");

            migrationBuilder.AddColumn<Guid>(
                name: "SourcePlanId",
                table: "TutoringSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourcePlanObjectiveOrder",
                table: "TutoringSessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContextWindow",
                table: "PersonalAiModels",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxOutputTokens",
                table: "PersonalAiModels",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "PreparedLearning",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Length = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ContractVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ContentJson = table.Column<string>(type: "character varying(64000)", maxLength: 64000, nullable: true),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CurrentActivity = table.Column<int>(type: "integer", nullable: false),
                    ContextWindow = table.Column<int>(type: "integer", nullable: false),
                    MaxOutputTokens = table.Column<int>(type: "integer", nullable: false),
                    EvaluationJson = table.Column<string>(type: "character varying(48000)", maxLength: 48000, nullable: true),
                    EvaluationEvidenceHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreparedLearning", x => x.SessionId);
                    table.CheckConstraint("CK_Prepared_Limits", "\"Length\" IN ('Short','Medium','Long') AND \"CurrentActivity\" >= 0 AND \"ContextWindow\" >= 0 AND \"MaxOutputTokens\" >= 0 AND ((\"ContentJson\" IS NULL AND \"ContentHash\" IS NULL) OR (\"ContentJson\" IS NOT NULL AND length(\"ContentHash\") = 64))");
                    table.ForeignKey(
                        name: "FK_PreparedLearning_TutoringSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "TutoringSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_TutoringTurn_Action",
                table: "TutoringTurns",
                sql: "\"Action\" BETWEEN 0 AND 12");

            migrationBuilder.CreateIndex(
                name: "IX_TutoringSessions_SourcePlanId_SourcePlanObjectiveOrder",
                table: "TutoringSessions",
                columns: new[] { "SourcePlanId", "SourcePlanObjectiveOrder" },
                unique: true,
                filter: "\"SourcePlanId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Session_Learning",
                table: "TutoringSessions",
                sql: "\"LearningExperience\" IN ('Tutoring','Diagnostic') AND \"LearningModality\" IN ('Conversational','Prepared') AND (\"LearningModality\" <> 'Prepared' OR \"Mode\" = 0)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TutoringSession_Sequence",
                table: "TutoringSessions",
                sql: "\"NextSequence\" BETWEEN 0 AND 500 AND (\"LearningModality\" = 'Prepared' OR \"NextSequence\" <= 100)");

            migrationBuilder.AddForeignKey(
                name: "FK_TutoringSessions_LearningPlans_SourcePlanId",
                table: "TutoringSessions",
                column: "SourcePlanId",
                principalTable: "LearningPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                CREATE FUNCTION guard_prepared_learning_source() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_OP='UPDATE' AND ROW(NEW."LearningExperience",NEW."LearningModality",NEW."SourcePlanId",NEW."SourcePlanObjectiveOrder") IS DISTINCT FROM
                    ROW(OLD."LearningExperience",OLD."LearningModality",OLD."SourcePlanId",OLD."SourcePlanObjectiveOrder") THEN
                    RAISE EXCEPTION 'Immutable learning modality and plan source'; END IF;
                  IF TG_OP='UPDATE' AND OLD."LearningModality"='Prepared' AND ROW(NEW."Topic",NEW."Goal",NEW."Level",NEW."DirectReview",NEW."PolicyVersion",NEW."ProviderId",NEW."ModelId") IS DISTINCT FROM
                    ROW(OLD."Topic",OLD."Goal",OLD."Level",OLD."DirectReview",OLD."PolicyVersion",OLD."ProviderId",OLD."ModelId") THEN
                    RAISE EXCEPTION 'Immutable prepared identity'; END IF;
                  IF (NEW."SourcePlanId" IS NULL) <> (NEW."SourcePlanObjectiveOrder" IS NULL) OR NEW."SourcePlanObjectiveOrder" < 0 THEN
                    RAISE EXCEPTION 'Invalid source objective'; END IF;
                  IF NEW."SourcePlanId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "LearningPlans" p JOIN "TutoringSessions" s ON s."Id"=p."SessionId"
                    WHERE p."Id"=NEW."SourcePlanId" AND p."StudentId"=NEW."StudentId" AND s."StudentId"=NEW."StudentId" AND s."OrganizationId"=NEW."OrganizationId"
                      AND s."LearningModality"='Prepared' AND s."LearningExperience"='Diagnostic' AND s."State"=3
                      AND p."ContentJson"::jsonb->'objectives'->NEW."SourcePlanObjectiveOrder"->>'title'=NEW."Goal") THEN
                    RAISE EXCEPTION 'Invalid private plan source'; END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER prepared_learning_source BEFORE INSERT OR UPDATE ON "TutoringSessions"
                  FOR EACH ROW EXECUTE FUNCTION guard_prepared_learning_source();
                CREATE FUNCTION guard_prepared_snapshot() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE modality text; execution_state integer;
                BEGIN
                  IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Prepared material is retained'; END IF;
                  SELECT "LearningModality","State" INTO modality,execution_state FROM "TutoringSessions" WHERE "Id"=NEW."SessionId";
                  IF modality IS DISTINCT FROM 'Prepared' THEN RAISE EXCEPTION 'Invalid prepared session'; END IF;
                  IF TG_OP='UPDATE' THEN
                    IF ROW(NEW."SessionId",NEW."Length",NEW."ContractVersion",NEW."ContextWindow",NEW."MaxOutputTokens") IS DISTINCT FROM
                      ROW(OLD."SessionId",OLD."Length",OLD."ContractVersion",OLD."ContextWindow",OLD."MaxOutputTokens") OR NEW."CurrentActivity" < OLD."CurrentActivity" THEN
                      RAISE EXCEPTION 'Immutable prepared source and forward progress'; END IF;
                    IF OLD."ContentJson" IS NOT NULL AND ROW(NEW."ContentJson",NEW."ContentHash") IS DISTINCT FROM ROW(OLD."ContentJson",OLD."ContentHash") THEN
                      RAISE EXCEPTION 'Immutable prepared script'; END IF;
                    IF OLD."EvaluationJson" IS NOT NULL AND ROW(NEW."EvaluationJson",NEW."EvaluationEvidenceHash") IS DISTINCT FROM ROW(OLD."EvaluationJson",OLD."EvaluationEvidenceHash") THEN
                      RAISE EXCEPTION 'Immutable grouped evaluation'; END IF;
                  END IF;
                  IF NEW."ContentJson" IS NOT NULL AND (NEW."ContentJson"::jsonb->>'format' IS DISTINCT FROM NEW."ContractVersion" OR
                    NEW."CurrentActivity" > jsonb_array_length(NEW."ContentJson"::jsonb->'activities')) THEN RAISE EXCEPTION 'Invalid prepared contract'; END IF;
                  IF NEW."EvaluationJson" IS NOT NULL AND (execution_state<>3 OR coalesce(length(NEW."EvaluationEvidenceHash"),0)<>64) THEN
                    RAISE EXCEPTION 'Invalid grouped evaluation source'; END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER prepared_snapshot BEFORE INSERT OR UPDATE OR DELETE ON "PreparedLearning"
                  FOR EACH ROW EXECUTE FUNCTION guard_prepared_snapshot();
                CREATE FUNCTION guard_prepared_exercise() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF EXISTS(SELECT 1 FROM "PreparedLearning" WHERE "SessionId"=OLD."SessionId") THEN
                    IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Prepared exercises are retained'; END IF;
                    IF ROW(NEW."Id",NEW."SessionId",NEW."PlanId",NEW."ObjectiveOrder",NEW."Order",NEW."Version",NEW."IsDiagnostic",NEW."Kind",NEW."Prompt",NEW."OptionsJson",NEW."AcceptedAnswersJson",NEW."Rubric",NEW."OriginRequestId",NEW."CreatedAt") IS DISTINCT FROM
                      ROW(OLD."Id",OLD."SessionId",OLD."PlanId",OLD."ObjectiveOrder",OLD."Order",OLD."Version",OLD."IsDiagnostic",OLD."Kind",OLD."Prompt",OLD."OptionsJson",OLD."AcceptedAnswersJson",OLD."Rubric",OLD."OriginRequestId",OLD."CreatedAt") THEN
                      RAISE EXCEPTION 'Immutable prepared exercise'; END IF;
                  END IF;
                  IF TG_OP='DELETE' THEN RETURN OLD; END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER prepared_exercise BEFORE UPDATE OR DELETE ON "LearningExercises"
                  FOR EACH ROW EXECUTE FUNCTION guard_prepared_exercise();
                CREATE FUNCTION guard_prepared_result() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF EXISTS(SELECT 1 FROM "PreparedLearning" WHERE "SessionId"=OLD."SessionId") THEN
                    IF TG_OP='DELETE' OR TG_TABLE_NAME<>'LearningPlans' THEN RAISE EXCEPTION 'Prepared results are immutable'; END IF;
                    IF ROW(NEW."Id",NEW."SessionId",NEW."StudentId",NEW."DiagnosticId",NEW."DiagnosticVersion",NEW."Version",NEW."ContentJson",NEW."ProviderId",NEW."ModelId",NEW."PromptVersion",NEW."GeneratedAt") IS DISTINCT FROM
                      ROW(OLD."Id",OLD."SessionId",OLD."StudentId",OLD."DiagnosticId",OLD."DiagnosticVersion",OLD."Version",OLD."ContentJson",OLD."ProviderId",OLD."ModelId",OLD."PromptVersion",OLD."GeneratedAt") THEN
                      RAISE EXCEPTION 'Immutable prepared plan proposal'; END IF;
                  END IF;
                  IF TG_OP='DELETE' THEN RETURN OLD; END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER prepared_report BEFORE UPDATE OR DELETE ON "LearningReports" FOR EACH ROW EXECUTE FUNCTION guard_prepared_result();
                CREATE TRIGGER prepared_diagnosis BEFORE UPDATE OR DELETE ON "DiagnosticAssessments" FOR EACH ROW EXECUTE FUNCTION guard_prepared_result();
                CREATE TRIGGER prepared_plan BEFORE UPDATE OR DELETE ON "LearningPlans" FOR EACH ROW EXECUTE FUNCTION guard_prepared_result();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER prepared_exercise ON "LearningExercises";
                DROP TRIGGER prepared_report ON "LearningReports";
                DROP TRIGGER prepared_diagnosis ON "DiagnosticAssessments";
                DROP TRIGGER prepared_plan ON "LearningPlans";
                DROP FUNCTION guard_prepared_result();
                DROP FUNCTION guard_prepared_exercise();
                DROP TRIGGER prepared_snapshot ON "PreparedLearning";
                DROP FUNCTION guard_prepared_snapshot();
                DROP TRIGGER prepared_learning_source ON "TutoringSessions";
                DROP FUNCTION guard_prepared_learning_source();
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_TutoringSessions_LearningPlans_SourcePlanId",
                table: "TutoringSessions");

            migrationBuilder.DropTable(
                name: "PreparedLearning");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TutoringTurn_Action",
                table: "TutoringTurns");

            migrationBuilder.DropIndex(
                name: "IX_TutoringSessions_SourcePlanId_SourcePlanObjectiveOrder",
                table: "TutoringSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Session_Learning",
                table: "TutoringSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TutoringSession_Sequence",
                table: "TutoringSessions");

            migrationBuilder.DropColumn(
                name: "LearningExperience",
                table: "TutoringSessions");

            migrationBuilder.DropColumn(
                name: "LearningModality",
                table: "TutoringSessions");

            migrationBuilder.DropColumn(
                name: "SourcePlanId",
                table: "TutoringSessions");

            migrationBuilder.DropColumn(
                name: "SourcePlanObjectiveOrder",
                table: "TutoringSessions");

            migrationBuilder.DropColumn(
                name: "ContextWindow",
                table: "PersonalAiModels");

            migrationBuilder.DropColumn(
                name: "MaxOutputTokens",
                table: "PersonalAiModels");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TutoringTurn_Action",
                table: "TutoringTurns",
                sql: "\"Action\" BETWEEN 0 AND 11");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TutoringSession_Sequence",
                table: "TutoringSessions",
                sql: "\"NextSequence\" BETWEEN 0 AND 100");
        }
    }
}

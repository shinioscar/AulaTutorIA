using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AulaTutor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AssignedClasses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignedActivity",
                table: "TutoringSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "AssignmentId",
                table: "TutoringSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LessonVersionId",
                table: "TutoringSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Mode",
                table: "TutoringSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedExerciseId",
                table: "LearningExercises",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedObjectiveId",
                table: "LearningExercises",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsComplementary",
                table: "LearningExercises",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "LessonVersionId",
                table: "LearningExercises",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Required",
                table: "LearningExercises",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_Lessons_Id_TeacherId_OrganizationId",
                table: "Lessons",
                columns: new[] { "Id", "TeacherId", "OrganizationId" });

            migrationBuilder.CreateTable(
                name: "LessonAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreationKey = table.Column<Guid>(type: "uuid", nullable: false),
                    CreationHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AvailableFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AvailableUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonAssignments", x => x.Id);
                    table.UniqueConstraint("AK_LessonAssignments_Id_LessonVersionId_OrganizationId", x => new { x.Id, x.LessonVersionId, x.OrganizationId });
                    table.CheckConstraint("CK_Assignment_StateDates", "\"State\" BETWEEN 0 AND 2 AND \"Revision\" >= 0 AND (\"AvailableFrom\" IS NULL OR \"AvailableUntil\" IS NULL OR \"AvailableUntil\" > \"AvailableFrom\")");
                    table.ForeignKey(
                        name: "FK_LessonAssignments_Groups_GroupId_OrganizationId",
                        columns: x => new { x.GroupId, x.OrganizationId },
                        principalTable: "Groups",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonAssignments_LessonPublications_LessonVersionId",
                        column: x => x.LessonVersionId,
                        principalTable: "LessonPublications",
                        principalColumn: "VersionId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonAssignments_LessonVersions_LessonVersionId_LessonId",
                        columns: x => new { x.LessonVersionId, x.LessonId },
                        principalTable: "LessonVersions",
                        principalColumns: new[] { "Id", "LessonId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonAssignments_Lessons_LessonId_TeacherId_OrganizationId",
                        columns: x => new { x.LessonId, x.TeacherId, x.OrganizationId },
                        principalTable: "Lessons",
                        principalColumns: new[] { "Id", "TeacherId", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AssignmentChanges",
                columns: table => new
                {
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    InputHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    AvailableFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AvailableUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentChanges", x => new { x.AssignmentId, x.RequestId });
                    table.ForeignKey(
                        name: "FK_AssignmentChanges_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AssignmentChanges_LessonAssignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "LessonAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TutoringSessions_AssignmentId_LessonVersionId_OrganizationId",
                table: "TutoringSessions",
                columns: new[] { "AssignmentId", "LessonVersionId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_TutoringSessions_AssignmentId_StudentId",
                table: "TutoringSessions",
                columns: new[] { "AssignmentId", "StudentId" },
                unique: true,
                filter: "\"AssignmentId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Session_ModeSource",
                table: "TutoringSessions",
                sql: "\"Mode\" BETWEEN 0 AND 2 AND \"AssignedActivity\" >= 0 AND ((\"Mode\" = 2 AND \"AssignmentId\" IS NOT NULL AND \"LessonVersionId\" IS NOT NULL) OR (\"Mode\" <> 2 AND \"AssignmentId\" IS NULL AND \"LessonVersionId\" IS NULL))");

            migrationBuilder.CreateIndex(
                name: "IX_LearningExercises_LessonVersionId",
                table: "LearningExercises",
                column: "LessonVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningExercises_SessionId_ApprovedExerciseId",
                table: "LearningExercises",
                columns: new[] { "SessionId", "ApprovedExerciseId" },
                unique: true,
                filter: "\"ApprovedExerciseId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Exercise_ApprovedSource",
                table: "LearningExercises",
                sql: "(\"ApprovedExerciseId\" IS NULL AND \"ApprovedObjectiveId\" IS NULL AND \"LessonVersionId\" IS NULL AND NOT \"Required\") OR (\"ApprovedExerciseId\" IS NOT NULL AND \"ApprovedObjectiveId\" IS NOT NULL AND \"LessonVersionId\" IS NOT NULL AND NOT \"IsComplementary\" AND NOT \"IsDiagnostic\" AND \"PlanId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentChanges_ActorId",
                table: "AssignmentChanges",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonAssignments_GroupId_LessonVersionId",
                table: "LessonAssignments",
                columns: new[] { "GroupId", "LessonVersionId" },
                unique: true,
                filter: "\"State\" = 0");

            migrationBuilder.CreateIndex(
                name: "IX_LessonAssignments_GroupId_OrganizationId",
                table: "LessonAssignments",
                columns: new[] { "GroupId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_LessonAssignments_LessonId_TeacherId_OrganizationId",
                table: "LessonAssignments",
                columns: new[] { "LessonId", "TeacherId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_LessonAssignments_LessonVersionId_LessonId",
                table: "LessonAssignments",
                columns: new[] { "LessonVersionId", "LessonId" });

            migrationBuilder.CreateIndex(
                name: "IX_LessonAssignments_TeacherId_CreationKey",
                table: "LessonAssignments",
                columns: new[] { "TeacherId", "CreationKey" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LearningExercises_LessonVersions_LessonVersionId",
                table: "LearningExercises",
                column: "LessonVersionId",
                principalTable: "LessonVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TutoringSessions_LessonAssignments_AssignmentId_LessonVersi~",
                table: "TutoringSessions",
                columns: new[] { "AssignmentId", "LessonVersionId", "OrganizationId" },
                principalTable: "LessonAssignments",
                principalColumns: new[] { "Id", "LessonVersionId", "OrganizationId" },
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                UPDATE "TutoringSessions" s SET "Mode"=1
                WHERE EXISTS (SELECT 1 FROM "LearningPlans" p WHERE p."SessionId"=s."Id" AND p."AcceptedAt" IS NOT NULL);
                CREATE FUNCTION protect_assignment_source() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF (NEW."LessonId",NEW."LessonVersionId",NEW."OrganizationId",NEW."GroupId",NEW."TeacherId",NEW."CreationKey",NEW."CreationHash",NEW."CreatedAt")
                     IS DISTINCT FROM (OLD."LessonId",OLD."LessonVersionId",OLD."OrganizationId",OLD."GroupId",OLD."TeacherId",OLD."CreationKey",OLD."CreationHash",OLD."CreatedAt")
                  THEN RAISE EXCEPTION 'Immutable assignment source'; END IF;
                  RETURN NEW;
                END; $$;
                CREATE TRIGGER assignment_source_immutable BEFORE UPDATE ON "LessonAssignments"
                FOR EACH ROW EXECUTE FUNCTION protect_assignment_source();
                CREATE TRIGGER assignment_history_immutable BEFORE UPDATE OR DELETE ON "AssignmentChanges"
                FOR EACH ROW EXECUTE FUNCTION reject_lesson_snapshot_mutation();
                CREATE FUNCTION protect_assigned_session() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF (NEW."AssignmentId",NEW."LessonVersionId",NEW."StudentId",NEW."OrganizationId",NEW."CreationKey")
                     IS DISTINCT FROM (OLD."AssignmentId",OLD."LessonVersionId",OLD."StudentId",OLD."OrganizationId",OLD."CreationKey")
                     OR (OLD."Mode"=2 AND NEW."Mode"<>2) OR (OLD."Mode"<>2 AND NEW."Mode"=2)
                  THEN RAISE EXCEPTION 'Immutable session source'; END IF;
                  RETURN NEW;
                END; $$;
                CREATE TRIGGER assigned_session_source_immutable BEFORE UPDATE ON "TutoringSessions"
                FOR EACH ROW EXECUTE FUNCTION protect_assigned_session();
                CREATE FUNCTION protect_approved_exercise() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE source_version uuid;
                BEGIN
                  IF TG_OP='UPDATE' AND (NEW."SessionId",NEW."LessonVersionId",NEW."ApprovedExerciseId",NEW."ApprovedObjectiveId",NEW."Required",NEW."IsComplementary")
                     IS DISTINCT FROM (OLD."SessionId",OLD."LessonVersionId",OLD."ApprovedExerciseId",OLD."ApprovedObjectiveId",OLD."Required",OLD."IsComplementary")
                  THEN RAISE EXCEPTION 'Immutable exercise origin'; END IF;
                  IF TG_OP='UPDATE' AND OLD."ApprovedExerciseId" IS NOT NULL AND
                     (NEW."Prompt",NEW."Kind",NEW."OptionsJson",NEW."AcceptedAnswersJson",NEW."Rubric",NEW."Version",NEW."Order",NEW."ObjectiveOrder",NEW."OriginRequestId",NEW."PlanId",NEW."IsDiagnostic")
                     IS DISTINCT FROM (OLD."Prompt",OLD."Kind",OLD."OptionsJson",OLD."AcceptedAnswersJson",OLD."Rubric",OLD."Version",OLD."Order",OLD."ObjectiveOrder",OLD."OriginRequestId",OLD."PlanId",OLD."IsDiagnostic")
                  THEN RAISE EXCEPTION 'Immutable approved exercise'; END IF;
                  IF NEW."ApprovedExerciseId" IS NOT NULL THEN
                    SELECT "LessonVersionId" INTO source_version FROM "TutoringSessions" WHERE "Id"=NEW."SessionId" AND "Mode"=2;
                    IF source_version IS NULL OR source_version IS DISTINCT FROM NEW."LessonVersionId"
                    THEN RAISE EXCEPTION 'Invalid approved exercise version'; END IF;
                  END IF;
                  RETURN NEW;
                END; $$;
                CREATE TRIGGER approved_exercise_immutable BEFORE INSERT OR UPDATE ON "LearningExercises"
                FOR EACH ROW EXECUTE FUNCTION protect_approved_exercise();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER approved_exercise_immutable ON "LearningExercises";
                DROP TRIGGER assigned_session_source_immutable ON "TutoringSessions";
                DROP FUNCTION protect_approved_exercise();
                DROP FUNCTION protect_assigned_session();
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_LearningExercises_LessonVersions_LessonVersionId",
                table: "LearningExercises");

            migrationBuilder.DropForeignKey(
                name: "FK_TutoringSessions_LessonAssignments_AssignmentId_LessonVersi~",
                table: "TutoringSessions");

            migrationBuilder.DropTable(
                name: "AssignmentChanges");

            migrationBuilder.DropTable(
                name: "LessonAssignments");
            migrationBuilder.Sql("DROP FUNCTION protect_assignment_source();");

            migrationBuilder.DropIndex(
                name: "IX_TutoringSessions_AssignmentId_LessonVersionId_OrganizationId",
                table: "TutoringSessions");

            migrationBuilder.DropIndex(
                name: "IX_TutoringSessions_AssignmentId_StudentId",
                table: "TutoringSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Session_ModeSource",
                table: "TutoringSessions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_Lessons_Id_TeacherId_OrganizationId",
                table: "Lessons");

            migrationBuilder.DropIndex(
                name: "IX_LearningExercises_LessonVersionId",
                table: "LearningExercises");

            migrationBuilder.DropIndex(
                name: "IX_LearningExercises_SessionId_ApprovedExerciseId",
                table: "LearningExercises");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Exercise_ApprovedSource",
                table: "LearningExercises");

            migrationBuilder.DropColumn(
                name: "AssignedActivity",
                table: "TutoringSessions");

            migrationBuilder.DropColumn(
                name: "AssignmentId",
                table: "TutoringSessions");

            migrationBuilder.DropColumn(
                name: "LessonVersionId",
                table: "TutoringSessions");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "TutoringSessions");

            migrationBuilder.DropColumn(
                name: "ApprovedExerciseId",
                table: "LearningExercises");

            migrationBuilder.DropColumn(
                name: "ApprovedObjectiveId",
                table: "LearningExercises");

            migrationBuilder.DropColumn(
                name: "IsComplementary",
                table: "LearningExercises");

            migrationBuilder.DropColumn(
                name: "LessonVersionId",
                table: "LearningExercises");

            migrationBuilder.DropColumn(
                name: "Required",
                table: "LearningExercises");
        }
    }
}

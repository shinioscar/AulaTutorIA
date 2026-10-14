using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AulaTutor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TeacherLessons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Lessons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreationKey = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lessons", x => x.Id);
                    table.CheckConstraint("CK_Lesson_Revision", "\"Revision\" >= 0");
                    table.CheckConstraint("CK_Lesson_State", "\"State\" BETWEEN 0 AND 5");
                    table.ForeignKey(
                        name: "FK_Lessons_AspNetUsers_TeacherId_OrganizationId",
                        columns: x => new { x.TeacherId, x.OrganizationId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LessonCommands",
                columns: table => new
                {
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    InputHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonCommands", x => new { x.LessonId, x.RequestId });
                    table.ForeignKey(
                        name: "FK_LessonCommands_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LessonDraftRevisions",
                columns: table => new
                {
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ContentJson = table.Column<string>(type: "jsonb", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonDraftRevisions", x => new { x.LessonId, x.Revision });
                    table.ForeignKey(
                        name: "FK_LessonDraftRevisions_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LessonDrafts",
                columns: table => new
                {
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ContentJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonDrafts", x => x.LessonId);
                    table.ForeignKey(
                        name: "FK_LessonDrafts_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LessonProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    DraftRevision = table.Column<long>(type: "bigint", nullable: false),
                    ContentJson = table.Column<string>(type: "jsonb", nullable: false),
                    Explanation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LessonProposals_LessonDraftRevisions_LessonId_DraftRevision",
                        columns: x => new { x.LessonId, x.DraftRevision },
                        principalTable: "LessonDraftRevisions",
                        principalColumns: new[] { "LessonId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LessonSimulations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    DraftRevision = table.Column<long>(type: "bigint", nullable: false),
                    ContentJson = table.Column<string>(type: "jsonb", nullable: false),
                    ProviderId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ModelId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    SectionIndex = table.Column<int>(type: "integer", nullable: false),
                    ExerciseIndex = table.Column<int>(type: "integer", nullable: false),
                    HintCount = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Answered = table.Column<bool>(type: "boolean", nullable: false),
                    PendingRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StoppedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonSimulations", x => x.Id);
                    table.UniqueConstraint("AK_LessonSimulations_Id_LessonId", x => new { x.Id, x.LessonId });
                    table.CheckConstraint("CK_LessonSimulation_State", "\"State\" IN ('Active','Stopped')");
                    table.ForeignKey(
                        name: "FK_LessonSimulations_LessonDraftRevisions_LessonId_DraftRevisi~",
                        columns: x => new { x.LessonId, x.DraftRevision },
                        principalTable: "LessonDraftRevisions",
                        principalColumns: new[] { "LessonId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LessonVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    DraftRevision = table.Column<long>(type: "bigint", nullable: false),
                    ContentJson = table.Column<string>(type: "jsonb", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonVersions", x => x.Id);
                    table.UniqueConstraint("AK_LessonVersions_Id_LessonId", x => new { x.Id, x.LessonId });
                    table.ForeignKey(
                        name: "FK_LessonVersions_AspNetUsers_ApprovedBy",
                        column: x => x.ApprovedBy,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonVersions_LessonDraftRevisions_LessonId_DraftRevision",
                        columns: x => new { x.LessonId, x.DraftRevision },
                        principalTable: "LessonDraftRevisions",
                        principalColumns: new[] { "LessonId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LessonOperations",
                columns: table => new
                {
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    SimulationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    InputHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DraftRevision = table.Column<long>(type: "bigint", nullable: false),
                    ContextJson = table.Column<string>(type: "jsonb", nullable: false),
                    ProviderId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ModelId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RetryNotBefore = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Reply = table.Column<string>(type: "character varying(6000)", maxLength: 6000, nullable: true),
                    Outcome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ValidationType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: true),
                    OutputTokens = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonOperations", x => new { x.LessonId, x.RequestId });
                    table.CheckConstraint("CK_LessonOperation_Attempts", "\"Attempts\" BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_LessonOperation_State", "\"State\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_LessonOperations_LessonSimulations_SimulationId_LessonId",
                        columns: x => new { x.SimulationId, x.LessonId },
                        principalTable: "LessonSimulations",
                        principalColumns: new[] { "Id", "LessonId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LessonOperations_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LessonPublications",
                columns: table => new
                {
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LessonPublications", x => x.VersionId);
                    table.ForeignKey(
                        name: "FK_LessonPublications_LessonVersions_VersionId_LessonId",
                        columns: x => new { x.VersionId, x.LessonId },
                        principalTable: "LessonVersions",
                        principalColumns: new[] { "Id", "LessonId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LessonOperations_SimulationId_LessonId",
                table: "LessonOperations",
                columns: new[] { "SimulationId", "LessonId" });

            migrationBuilder.CreateIndex(
                name: "IX_LessonProposals_LessonId_DraftRevision",
                table: "LessonProposals",
                columns: new[] { "LessonId", "DraftRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_LessonProposals_LessonId_RequestId",
                table: "LessonProposals",
                columns: new[] { "LessonId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LessonPublications_VersionId_LessonId",
                table: "LessonPublications",
                columns: new[] { "VersionId", "LessonId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_TeacherId_CreationKey",
                table: "Lessons",
                columns: new[] { "TeacherId", "CreationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lessons_TeacherId_OrganizationId",
                table: "Lessons",
                columns: new[] { "TeacherId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_LessonSimulations_LessonId_DraftRevision",
                table: "LessonSimulations",
                columns: new[] { "LessonId", "DraftRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_LessonVersions_ApprovedBy",
                table: "LessonVersions",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_LessonVersions_LessonId_DraftRevision",
                table: "LessonVersions",
                columns: new[] { "LessonId", "DraftRevision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LessonVersions_LessonId_Number",
                table: "LessonVersions",
                columns: new[] { "LessonId", "Number" },
                unique: true);
            // Defense in depth: even a direct persistence update cannot rewrite approved content.
            migrationBuilder.Sql("""
                CREATE FUNCTION reject_lesson_snapshot_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Immutable lesson snapshot'; END; $$;
                CREATE TRIGGER lesson_version_immutable BEFORE UPDATE OR DELETE ON "LessonVersions"
                FOR EACH ROW EXECUTE FUNCTION reject_lesson_snapshot_mutation();
                CREATE TRIGGER lesson_revision_immutable BEFORE UPDATE OR DELETE ON "LessonDraftRevisions"
                FOR EACH ROW EXECUTE FUNCTION reject_lesson_snapshot_mutation();
                CREATE FUNCTION protect_simulation_snapshot() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF NEW."ContentJson" IS DISTINCT FROM OLD."ContentJson" OR NEW."DraftRevision" IS DISTINCT FROM OLD."DraftRevision"
                     OR NEW."LessonId" IS DISTINCT FROM OLD."LessonId" OR NEW."ProviderId" IS DISTINCT FROM OLD."ProviderId" OR NEW."ModelId" IS DISTINCT FROM OLD."ModelId"
                  THEN RAISE EXCEPTION 'Immutable simulation source'; END IF;
                  RETURN NEW;
                END; $$;
                CREATE TRIGGER lesson_simulation_source_immutable BEFORE UPDATE ON "LessonSimulations"
                FOR EACH ROW EXECUTE FUNCTION protect_simulation_snapshot();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LessonCommands");

            migrationBuilder.DropTable(
                name: "LessonDrafts");

            migrationBuilder.DropTable(
                name: "LessonOperations");

            migrationBuilder.DropTable(
                name: "LessonProposals");

            migrationBuilder.DropTable(
                name: "LessonPublications");

            migrationBuilder.DropTable(
                name: "LessonSimulations");

            migrationBuilder.DropTable(
                name: "LessonVersions");

            migrationBuilder.DropTable(
                name: "LessonDraftRevisions");

            migrationBuilder.DropTable(
                name: "Lessons");
            migrationBuilder.Sql("DROP FUNCTION protect_simulation_snapshot(); DROP FUNCTION reject_lesson_snapshot_mutation();");
        }
    }
}

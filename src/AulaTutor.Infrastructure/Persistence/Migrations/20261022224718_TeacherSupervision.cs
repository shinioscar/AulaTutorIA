using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AulaTutor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TeacherSupervision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_ExerciseAttempts_Id_SessionId",
                table: "ExerciseAttempts",
                columns: new[] { "Id", "SessionId" });

            migrationBuilder.CreateTable(
                name: "TeacherReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherReviews", x => x.Id);
                    table.UniqueConstraint("AK_TeacherReviews_Id_SessionId", x => new { x.Id, x.SessionId });
                    table.CheckConstraint("CK_Review_Target", "\"Revision\" > 0 AND ((\"ExerciseId\" IS NULL AND \"TargetId\" = \"SessionId\") OR (\"ExerciseId\" IS NOT NULL AND \"TargetId\" = \"ExerciseId\"))");
                    table.ForeignKey(
                        name: "FK_TeacherReviews_LearningExercises_ExerciseId_SessionId",
                        columns: x => new { x.ExerciseId, x.SessionId },
                        principalTable: "LearningExercises",
                        principalColumns: new[] { "Id", "SessionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherReviews_TutoringSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "TutoringSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeacherReviewEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    InputHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    EvidenceRevision = table.Column<long>(type: "bigint", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Attention = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    Flagged = table.Column<bool>(type: "boolean", nullable: false),
                    Comment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Observation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Recommendation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherReviewEntries", x => x.Id);
                    table.UniqueConstraint("AK_TeacherReviewEntries_Id_SessionId", x => new { x.Id, x.SessionId });
                    table.CheckConstraint("CK_ReviewEntry_State", "\"Revision\" > 0 AND \"EvidenceRevision\" >= 0 AND \"State\" BETWEEN 0 AND 3 AND \"Attention\" BETWEEN 0 AND 2 AND \"Outcome\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_TeacherReviewEntries_AspNetUsers_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeacherReviewEntries_TeacherReviews_ReviewId_SessionId",
                        columns: x => new { x.ReviewId, x.SessionId },
                        principalTable: "TeacherReviews",
                        principalColumns: new[] { "Id", "SessionId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReviewEvidence",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewEvidence", x => new { x.EntryId, x.AttemptId });
                    table.ForeignKey(
                        name: "FK_ReviewEvidence_ExerciseAttempts_AttemptId_SessionId",
                        columns: x => new { x.AttemptId, x.SessionId },
                        principalTable: "ExerciseAttempts",
                        principalColumns: new[] { "Id", "SessionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReviewEvidence_TeacherReviewEntries_EntryId_SessionId",
                        columns: x => new { x.EntryId, x.SessionId },
                        principalTable: "TeacherReviewEntries",
                        principalColumns: new[] { "Id", "SessionId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewEvidence_AttemptId_SessionId",
                table: "ReviewEvidence",
                columns: new[] { "AttemptId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewEvidence_EntryId_SessionId",
                table: "ReviewEvidence",
                columns: new[] { "EntryId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherReviewEntries_AuthorId",
                table: "TeacherReviewEntries",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherReviewEntries_ReviewId_Revision",
                table: "TeacherReviewEntries",
                columns: new[] { "ReviewId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherReviewEntries_ReviewId_SessionId",
                table: "TeacherReviewEntries",
                columns: new[] { "ReviewId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherReviewEntries_SessionId_RequestId",
                table: "TeacherReviewEntries",
                columns: new[] { "SessionId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeacherReviews_ExerciseId_SessionId",
                table: "TeacherReviews",
                columns: new[] { "ExerciseId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherReviews_SessionId_TargetId",
                table: "TeacherReviews",
                columns: new[] { "SessionId", "TargetId" },
                unique: true);
            migrationBuilder.Sql("""
                CREATE FUNCTION protect_teacher_review_source() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_OP = 'UPDATE' AND ROW(OLD."Id", OLD."SessionId", OLD."ExerciseId", OLD."TargetId")
                    IS DISTINCT FROM ROW(NEW."Id", NEW."SessionId", NEW."ExerciseId", NEW."TargetId")
                  THEN RAISE EXCEPTION 'Immutable review source'; END IF;
                  IF NOT EXISTS (SELECT 1 FROM "TutoringSessions" s WHERE s."Id" = NEW."SessionId" AND s."Mode" = 2)
                  THEN RAISE EXCEPTION 'Only assigned class evidence is reviewable'; END IF;
                  RETURN NEW;
                END; $$;
                CREATE TRIGGER teacher_review_source BEFORE INSERT OR UPDATE ON "TeacherReviews"
                FOR EACH ROW EXECUTE FUNCTION protect_teacher_review_source();
                CREATE TRIGGER teacher_review_history BEFORE UPDATE OR DELETE ON "TeacherReviewEntries"
                FOR EACH ROW EXECUTE FUNCTION reject_lesson_snapshot_mutation();
                CREATE TRIGGER teacher_review_evidence_history BEFORE UPDATE OR DELETE ON "ReviewEvidence"
                FOR EACH ROW EXECUTE FUNCTION reject_lesson_snapshot_mutation();
                CREATE FUNCTION protect_review_evidence_target() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF EXISTS (SELECT 1 FROM "TeacherReviewEntries" e JOIN "TeacherReviews" r ON r."Id" = e."ReviewId"
                    JOIN "ExerciseAttempts" a ON a."Id" = NEW."AttemptId"
                    WHERE e."Id" = NEW."EntryId" AND r."ExerciseId" IS NOT NULL AND r."ExerciseId" <> a."ExerciseId")
                  THEN RAISE EXCEPTION 'Review evidence must belong to its exercise'; END IF;
                  RETURN NEW;
                END; $$;
                CREATE TRIGGER teacher_review_evidence_target BEFORE INSERT ON "ReviewEvidence"
                FOR EACH ROW EXECUTE FUNCTION protect_review_evidence_target();
                CREATE FUNCTION protect_primary_attempt() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF ROW(OLD."Id", OLD."SessionId", OLD."StudentId", OLD."ExerciseId", OLD."ExerciseVersion", OLD."RequestId",
                    OLD."Answer", OLD."Number", OLD."HintsUsed", OLD."SimplificationsUsed", OLD."Outcome", OLD."ValidationType", OLD."SubmittedAt")
                    IS DISTINCT FROM ROW(NEW."Id", NEW."SessionId", NEW."StudentId", NEW."ExerciseId", NEW."ExerciseVersion", NEW."RequestId",
                    NEW."Answer", NEW."Number", NEW."HintsUsed", NEW."SimplificationsUsed", NEW."Outcome", NEW."ValidationType", NEW."SubmittedAt")
                  THEN RAISE EXCEPTION 'Immutable primary attempt evidence'; END IF;
                  RETURN NEW;
                END; $$;
                CREATE TRIGGER primary_attempt_immutable BEFORE UPDATE ON "ExerciseAttempts"
                FOR EACH ROW EXECUTE FUNCTION protect_primary_attempt();
                CREATE TRIGGER primary_attempt_retained BEFORE DELETE ON "ExerciseAttempts"
                FOR EACH ROW EXECUTE FUNCTION reject_lesson_snapshot_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER primary_attempt_retained ON "ExerciseAttempts";
                DROP TRIGGER primary_attempt_immutable ON "ExerciseAttempts";
                DROP TRIGGER teacher_review_evidence_target ON "ReviewEvidence";
                DROP TRIGGER teacher_review_evidence_history ON "ReviewEvidence";
                DROP TRIGGER teacher_review_history ON "TeacherReviewEntries";
                DROP TRIGGER teacher_review_source ON "TeacherReviews";
                DROP FUNCTION protect_primary_attempt();
                DROP FUNCTION protect_review_evidence_target();
                DROP FUNCTION protect_teacher_review_source();
                """);
            migrationBuilder.DropTable(
                name: "ReviewEvidence");

            migrationBuilder.DropTable(
                name: "TeacherReviewEntries");

            migrationBuilder.DropTable(
                name: "TeacherReviews");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_ExerciseAttempts_Id_SessionId",
                table: "ExerciseAttempts");
        }
    }
}

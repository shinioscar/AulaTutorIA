using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AulaTutor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LearningEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TutoringTurn_Action",
                table: "TutoringTurns");

            migrationBuilder.AddColumn<string>(
                name: "ContextJson",
                table: "TutoringTurns",
                type: "character varying(28000)",
                maxLength: 28000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExerciseId",
                table: "TutoringTurns",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestText",
                table: "TutoringTurns",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "EvidenceRevision",
                table: "TutoringSessions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceRecommendationId",
                table: "TutoringSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceReportId",
                table: "TutoringSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_TutoringSessions_Id_StudentId",
                table: "TutoringSessions",
                columns: new[] { "Id", "StudentId" });

            migrationBuilder.CreateTable(
                name: "DiagnosticAssessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    EvidenceRevision = table.Column<long>(type: "bigint", nullable: false),
                    Preliminary = table.Column<bool>(type: "boolean", nullable: false),
                    ContentJson = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: false),
                    ProviderId = table.Column<string>(type: "text", nullable: false),
                    ModelId = table.Column<string>(type: "text", nullable: false),
                    PromptVersion = table.Column<string>(type: "text", nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiagnosticAssessments", x => x.Id);
                    table.UniqueConstraint("AK_DiagnosticAssessments_Id_SessionId", x => new { x.Id, x.SessionId });
                    table.ForeignKey(
                        name: "FK_DiagnosticAssessments_TutoringSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "TutoringSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LearningReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    EvidenceRevision = table.Column<long>(type: "bigint", nullable: false),
                    ContentJson = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: false),
                    StatisticsJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ProviderId = table.Column<string>(type: "text", nullable: false),
                    ModelId = table.Column<string>(type: "text", nullable: false),
                    PromptVersion = table.Column<string>(type: "text", nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningReports", x => x.Id);
                    table.UniqueConstraint("AK_LearningReports_Id_SessionId", x => new { x.Id, x.SessionId });
                    table.ForeignKey(
                        name: "FK_LearningReports_TutoringSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "TutoringSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LearningPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiagnosticId = table.Column<Guid>(type: "uuid", nullable: false),
                    DiagnosticVersion = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    CurrentActivity = table.Column<int>(type: "integer", nullable: false),
                    ContentJson = table.Column<string>(type: "character varying(24000)", maxLength: 24000, nullable: false),
                    ProviderId = table.Column<string>(type: "text", nullable: false),
                    ModelId = table.Column<string>(type: "text", nullable: false),
                    PromptVersion = table.Column<string>(type: "text", nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningPlans", x => x.Id);
                    table.UniqueConstraint("AK_LearningPlans_Id_SessionId", x => new { x.Id, x.SessionId });
                    table.CheckConstraint("CK_Plan_State", "\"State\" BETWEEN 0 AND 3 AND \"CurrentActivity\" >= 0");
                    table.ForeignKey(
                        name: "FK_LearningPlans_AspNetUsers_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LearningPlans_DiagnosticAssessments_DiagnosticId_SessionId",
                        columns: x => new { x.DiagnosticId, x.SessionId },
                        principalTable: "DiagnosticAssessments",
                        principalColumns: new[] { "Id", "SessionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LearningPlans_TutoringSessions_SessionId_StudentId",
                        columns: x => new { x.SessionId, x.StudentId },
                        principalTable: "TutoringSessions",
                        principalColumns: new[] { "Id", "StudentId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LearningRecommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    Goal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsGeneral = table.Column<bool>(type: "boolean", nullable: false),
                    EvidenceIdsJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningRecommendations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningRecommendations_LearningReports_ReportId_SessionId",
                        columns: x => new { x.ReportId, x.SessionId },
                        principalTable: "LearningReports",
                        principalColumns: new[] { "Id", "SessionId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsentVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportShares_AspNetUsers_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportShares_LearningReports_ReportId_SessionId",
                        columns: x => new { x.ReportId, x.SessionId },
                        principalTable: "LearningReports",
                        principalColumns: new[] { "Id", "SessionId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LearningExercises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    ObjectiveOrder = table.Column<int>(type: "integer", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    IsDiagnostic = table.Column<bool>(type: "boolean", nullable: false),
                    IsPresented = table.Column<bool>(type: "boolean", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Prompt = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: false),
                    OptionsJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AcceptedAnswersJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Rubric = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    HintCount = table.Column<int>(type: "integer", nullable: false),
                    SimplificationCount = table.Column<int>(type: "integer", nullable: false),
                    Completed = table.Column<bool>(type: "boolean", nullable: false),
                    OriginRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningExercises", x => x.Id);
                    table.UniqueConstraint("AK_LearningExercises_Id_SessionId", x => new { x.Id, x.SessionId });
                    table.CheckConstraint("CK_Exercise_Limits", "\"Version\" > 0 AND \"HintCount\" >= 0 AND \"Kind\" BETWEEN 0 AND 4");
                    table.ForeignKey(
                        name: "FK_LearningExercises_LearningPlans_PlanId_SessionId",
                        columns: x => new { x.PlanId, x.SessionId },
                        principalTable: "LearningPlans",
                        principalColumns: new[] { "Id", "SessionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LearningExercises_TutoringSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "TutoringSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExerciseAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseVersion = table.Column<int>(type: "integer", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Answer = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    HintsUsed = table.Column<int>(type: "integer", nullable: false),
                    SimplificationsUsed = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    ValidationType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Feedback = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: false),
                    ProviderId = table.Column<string>(type: "text", nullable: true),
                    ModelId = table.Column<string>(type: "text", nullable: true),
                    PromptVersion = table.Column<string>(type: "text", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExerciseAttempts", x => x.Id);
                    table.CheckConstraint("CK_Attempt_Limits", "\"Number\" BETWEEN 1 AND 10 AND \"HintsUsed\" >= 0 AND \"Outcome\" BETWEEN 0 AND 4");
                    table.ForeignKey(
                        name: "FK_ExerciseAttempts_AspNetUsers_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExerciseAttempts_LearningExercises_ExerciseId_SessionId",
                        columns: x => new { x.ExerciseId, x.SessionId },
                        principalTable: "LearningExercises",
                        principalColumns: new[] { "Id", "SessionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExerciseAttempts_TutoringSessions_SessionId_StudentId",
                        columns: x => new { x.SessionId, x.StudentId },
                        principalTable: "TutoringSessions",
                        principalColumns: new[] { "Id", "StudentId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExerciseAttempts_TutoringTurns_SessionId_RequestId",
                        columns: x => new { x.SessionId, x.RequestId },
                        principalTable: "TutoringTurns",
                        principalColumns: new[] { "SessionId", "RequestId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_TutoringTurn_Action",
                table: "TutoringTurns",
                sql: "\"Action\" BETWEEN 0 AND 11");

            migrationBuilder.CreateIndex(
                name: "IX_TutoringSessions_SourceRecommendationId",
                table: "TutoringSessions",
                column: "SourceRecommendationId");

            migrationBuilder.CreateIndex(
                name: "IX_TutoringSessions_SourceReportId",
                table: "TutoringSessions",
                column: "SourceReportId");

            migrationBuilder.CreateIndex(
                name: "IX_DiagnosticAssessments_SessionId_Version",
                table: "DiagnosticAssessments",
                columns: new[] { "SessionId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseAttempts_ExerciseId_Number",
                table: "ExerciseAttempts",
                columns: new[] { "ExerciseId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseAttempts_ExerciseId_SessionId",
                table: "ExerciseAttempts",
                columns: new[] { "ExerciseId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseAttempts_SessionId_RequestId",
                table: "ExerciseAttempts",
                columns: new[] { "SessionId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseAttempts_SessionId_StudentId",
                table: "ExerciseAttempts",
                columns: new[] { "SessionId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExerciseAttempts_StudentId",
                table: "ExerciseAttempts",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningExercises_PlanId_SessionId",
                table: "LearningExercises",
                columns: new[] { "PlanId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_LearningExercises_SessionId_PlanId_Order",
                table: "LearningExercises",
                columns: new[] { "SessionId", "PlanId", "Order" },
                unique: true,
                filter: "\"PlanId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LearningPlans_DiagnosticId_SessionId",
                table: "LearningPlans",
                columns: new[] { "DiagnosticId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_LearningPlans_SessionId_StudentId",
                table: "LearningPlans",
                columns: new[] { "SessionId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_LearningPlans_SessionId_Version",
                table: "LearningPlans",
                columns: new[] { "SessionId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearningPlans_StudentId",
                table: "LearningPlans",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningRecommendations_ReportId_SessionId",
                table: "LearningRecommendations",
                columns: new[] { "ReportId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_LearningReports_SessionId_Version",
                table: "LearningReports",
                columns: new[] { "SessionId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportShares_RecipientId",
                table: "ReportShares",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportShares_ReportId_SessionId",
                table: "ReportShares",
                columns: new[] { "ReportId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportShares_SessionId_RequestId",
                table: "ReportShares",
                columns: new[] { "SessionId", "RequestId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TutoringSessions_LearningRecommendations_SourceRecommendati~",
                table: "TutoringSessions",
                column: "SourceRecommendationId",
                principalTable: "LearningRecommendations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TutoringSessions_LearningReports_SourceReportId",
                table: "TutoringSessions",
                column: "SourceReportId",
                principalTable: "LearningReports",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TutoringSessions_LearningRecommendations_SourceRecommendati~",
                table: "TutoringSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_TutoringSessions_LearningReports_SourceReportId",
                table: "TutoringSessions");

            migrationBuilder.DropTable(
                name: "ExerciseAttempts");

            migrationBuilder.DropTable(
                name: "LearningRecommendations");

            migrationBuilder.DropTable(
                name: "ReportShares");

            migrationBuilder.DropTable(
                name: "LearningExercises");

            migrationBuilder.DropTable(
                name: "LearningReports");

            migrationBuilder.DropTable(
                name: "LearningPlans");

            migrationBuilder.DropTable(
                name: "DiagnosticAssessments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TutoringTurn_Action",
                table: "TutoringTurns");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_TutoringSessions_Id_StudentId",
                table: "TutoringSessions");

            migrationBuilder.DropIndex(
                name: "IX_TutoringSessions_SourceRecommendationId",
                table: "TutoringSessions");

            migrationBuilder.DropIndex(
                name: "IX_TutoringSessions_SourceReportId",
                table: "TutoringSessions");

            migrationBuilder.DropColumn(
                name: "ContextJson",
                table: "TutoringTurns");

            migrationBuilder.DropColumn(
                name: "ExerciseId",
                table: "TutoringTurns");

            migrationBuilder.DropColumn(
                name: "RequestText",
                table: "TutoringTurns");

            migrationBuilder.DropColumn(
                name: "EvidenceRevision",
                table: "TutoringSessions");

            migrationBuilder.DropColumn(
                name: "SourceRecommendationId",
                table: "TutoringSessions");

            migrationBuilder.DropColumn(
                name: "SourceReportId",
                table: "TutoringSessions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TutoringTurn_Action",
                table: "TutoringTurns",
                sql: "\"Action\" BETWEEN 0 AND 3");
        }
    }
}

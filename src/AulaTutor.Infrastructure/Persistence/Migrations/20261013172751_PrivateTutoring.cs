using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AulaTutor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PrivateTutoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TutoringSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreationKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Topic = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Goal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DirectReview = table.Column<bool>(type: "boolean", nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ProviderId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ModelId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    NextSequence = table.Column<int>(type: "integer", nullable: false),
                    PendingRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentFocus = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Activity = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutoringSessions", x => x.Id);
                    table.CheckConstraint("CK_TutoringSession_Revision", "\"Revision\" >= 0");
                    table.CheckConstraint("CK_TutoringSession_Sequence", "\"NextSequence\" BETWEEN 0 AND 100");
                    table.CheckConstraint("CK_TutoringSession_State", "\"State\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_TutoringSessions_AspNetUsers_StudentId_OrganizationId",
                        columns: x => new { x.StudentId, x.OrganizationId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TutoringTurns",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    InputHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    RetryNotBefore = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    InputTokens = table.Column<int>(type: "integer", nullable: true),
                    OutputTokens = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutoringTurns", x => new { x.SessionId, x.RequestId });
                    table.CheckConstraint("CK_TutoringTurn_Action", "\"Action\" BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_TutoringTurn_Attempts", "\"Attempts\" BETWEEN 1 AND 3");
                    table.CheckConstraint("CK_TutoringTurn_State", "\"State\" BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_TutoringTurns_TutoringSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "TutoringSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TutoringMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "character varying(6000)", maxLength: 6000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProviderId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ModelId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PromptVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    InputTokens = table.Column<int>(type: "integer", nullable: true),
                    OutputTokens = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TutoringMessages", x => x.Id);
                    table.CheckConstraint("CK_TutoringMessage_Role", "\"Role\" IN (0, 1)");
                    table.ForeignKey(
                        name: "FK_TutoringMessages_TutoringTurns_SessionId_RequestId",
                        columns: x => new { x.SessionId, x.RequestId },
                        principalTable: "TutoringTurns",
                        principalColumns: new[] { "SessionId", "RequestId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TutoringMessages_SessionId_RequestId_Role",
                table: "TutoringMessages",
                columns: new[] { "SessionId", "RequestId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TutoringMessages_SessionId_Sequence",
                table: "TutoringMessages",
                columns: new[] { "SessionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TutoringSessions_StudentId_CreationKey",
                table: "TutoringSessions",
                columns: new[] { "StudentId", "CreationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TutoringSessions_StudentId_LastActivityAt",
                table: "TutoringSessions",
                columns: new[] { "StudentId", "LastActivityAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TutoringSessions_StudentId_OrganizationId",
                table: "TutoringSessions",
                columns: new[] { "StudentId", "OrganizationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TutoringMessages");

            migrationBuilder.DropTable(
                name: "TutoringTurns");

            migrationBuilder.DropTable(
                name: "TutoringSessions");
        }
    }
}

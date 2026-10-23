using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AulaTutor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OperationalResilience : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    Provider = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RetryNotBefore = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EstimatedTokens = table.Column<int>(type: "integer", nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: true),
                    OutputTokens = table.Column<int>(type: "integer", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiReservations", x => x.Id);
                    table.CheckConstraint("CK_AiReservation_State", "\"State\" IN ('Reserved','Succeeded','Failed','Uncertain')");
                    table.CheckConstraint("CK_AiReservation_Usage", "\"EstimatedTokens\" >= 0 AND (\"InputTokens\" IS NULL OR \"InputTokens\" >= 0) AND (\"OutputTokens\" IS NULL OR \"OutputTokens\" >= 0)");
                    table.ForeignKey(
                        name: "FK_AiReservations_AspNetUsers_ActorId_OrganizationId",
                        columns: x => new { x.ActorId, x.OrganizationId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiReservations_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResourceId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Result = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Change = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiReservations_ActorId_CreatedAt",
                table: "AiReservations",
                columns: new[] { "ActorId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiReservations_ActorId_OrganizationId",
                table: "AiReservations",
                columns: new[] { "ActorId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_AiReservations_GroupId_CreatedAt",
                table: "AiReservations",
                columns: new[] { "GroupId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiReservations_LeaseUntil",
                table: "AiReservations",
                column: "LeaseUntil");

            migrationBuilder.CreateIndex(
                name: "IX_AiReservations_OrganizationId_CreatedAt",
                table: "AiReservations",
                columns: new[] { "OrganizationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiReservations_Provider_CreatedAt",
                table: "AiReservations",
                columns: new[] { "Provider", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ActorId_CreatedAt",
                table: "AuditEvents",
                columns: new[] { "ActorId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_CreatedAt_Id",
                table: "AuditEvents",
                columns: new[] { "CreatedAt", "Id" });
            migrationBuilder.Sql("""
                CREATE TRIGGER audit_events_immutable BEFORE UPDATE OR DELETE ON "AuditEvents"
                    FOR EACH ROW EXECUTE FUNCTION reject_lesson_snapshot_mutation();
                CREATE FUNCTION guard_ai_reservation() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'AI usage history is immutable'; END IF;
                    IF OLD."State" <> 'Reserved' OR NEW."State" = 'Reserved' OR NEW."FinishedAt" IS NULL OR
                        ROW(NEW."Id",NEW."ActorId",NEW."OrganizationId",NEW."GroupId",NEW."Provider",NEW."Model",NEW."CorrelationId",NEW."CreatedAt",NEW."LeaseUntil",NEW."EstimatedTokens") IS DISTINCT FROM
                        ROW(OLD."Id",OLD."ActorId",OLD."OrganizationId",OLD."GroupId",OLD."Provider",OLD."Model",OLD."CorrelationId",OLD."CreatedAt",OLD."LeaseUntil",OLD."EstimatedTokens") THEN
                        RAISE EXCEPTION 'AI reservation source or final state is immutable';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER ai_reservation_integrity BEFORE UPDATE OR DELETE ON "AiReservations"
                    FOR EACH ROW EXECUTE FUNCTION guard_ai_reservation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS guard_ai_reservation() CASCADE;");
            migrationBuilder.DropTable(
                name: "AiReservations");

            migrationBuilder.DropTable(
                name: "AuditEvents");
        }
    }
}

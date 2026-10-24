using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AulaTutor.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersonalAiConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AiBindingId",
                table: "TutoringSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AiBindingId",
                table: "LessonSimulations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AiBindingId",
                table: "LessonOperations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConnectionId",
                table: "AiReservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CredentialFingerprint",
                table: "AiReservations",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CredentialRevision",
                table: "AiReservations",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalAccount",
                table: "AiReservations",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Operation",
                table: "AiReservations",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "legacy");

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                table: "AiReservations",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Institutional");

            migrationBuilder.CreateTable(
                name: "PersonalAiConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ProtectedCredential = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CredentialRevision = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ExternalAccount = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Workspace = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PreferredModel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalAiConnections", x => x.Id);
                    table.UniqueConstraint("AK_PersonalAiConnections_Id_OwnerId_OrganizationId", x => new { x.Id, x.OwnerId, x.OrganizationId });
                    table.CheckConstraint("CK_PersonalConnection_Provider", "\"Provider\" IN ('OpenRouter','Groq','Gemini','Anthropic')");
                    table.CheckConstraint("CK_PersonalConnection_State", "\"Status\" IN ('Connected','Invalid','Disconnected') AND \"CredentialRevision\" > 0 AND \"Method\" IN ('Manual','OAuth')");
                    table.ForeignKey(
                        name: "FK_PersonalAiConnections_AspNetUsers_OwnerId_OrganizationId",
                        columns: x => new { x.OwnerId, x.OrganizationId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PersonalAiModels",
                columns: table => new
                {
                    Provider = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Compatible = table.Column<bool>(type: "boolean", nullable: false),
                    Free = table.Column<bool>(type: "boolean", nullable: false),
                    PriceKnown = table.Column<bool>(type: "boolean", nullable: false),
                    StrictSchema = table.Column<bool>(type: "boolean", nullable: false),
                    Availability = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalAiModels", x => new { x.Provider, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "PersonalAiOAuths",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StateHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    BrowserHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SessionHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StampHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProtectedVerifier = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    ProtectedCode = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: true),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalAiOAuths", x => x.Id);
                    table.CheckConstraint("CK_PersonalOAuth_State", "\"Status\" IN ('Pending','Returned','Exchanging','Succeeded','Failed','Canceled','Expired')");
                    table.ForeignKey(
                        name: "FK_PersonalAiOAuths_AspNetUsers_OwnerId_OrganizationId",
                        columns: x => new { x.OwnerId, x.OrganizationId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiInferenceBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialRevision = table.Column<long>(type: "bigint", nullable: false),
                    Provider = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PaidAccepted = table.Column<bool>(type: "boolean", nullable: false),
                    StrictSchema = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiInferenceBindings", x => x.Id);
                    table.UniqueConstraint("AK_AiInferenceBindings_Id_OwnerId_OrganizationId", x => new { x.Id, x.OwnerId, x.OrganizationId });
                    table.ForeignKey(
                        name: "FK_AiInferenceBindings_PersonalAiConnections_ConnectionId_Owne~",
                        columns: x => new { x.ConnectionId, x.OwnerId, x.OrganizationId },
                        principalTable: "PersonalAiConnections",
                        principalColumns: new[] { "Id", "OwnerId", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PersonalAiSelections",
                columns: table => new
                {
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Mode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Model = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PaidAccepted = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalAiSelections", x => x.OwnerId);
                    table.CheckConstraint("CK_PersonalSelection_Mode", "(\"Mode\" = 'Personal' AND \"ConnectionId\" IS NOT NULL AND \"Model\" IS NOT NULL) OR (\"Mode\" IN ('Institutional','None') AND \"ConnectionId\" IS NULL AND \"Model\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_PersonalAiSelections_AspNetUsers_OwnerId_OrganizationId",
                        columns: x => new { x.OwnerId, x.OrganizationId },
                        principalTable: "AspNetUsers",
                        principalColumns: new[] { "Id", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonalAiSelections_PersonalAiConnections_ConnectionId_Own~",
                        columns: x => new { x.ConnectionId, x.OwnerId, x.OrganizationId },
                        principalTable: "PersonalAiConnections",
                        principalColumns: new[] { "Id", "OwnerId", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TutoringSessions_AiBindingId_StudentId_OrganizationId",
                table: "TutoringSessions",
                columns: new[] { "AiBindingId", "StudentId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_LessonSimulations_AiBindingId",
                table: "LessonSimulations",
                column: "AiBindingId");

            migrationBuilder.CreateIndex(
                name: "IX_LessonOperations_AiBindingId",
                table: "LessonOperations",
                column: "AiBindingId");

            migrationBuilder.CreateIndex(
                name: "IX_AiReservations_ConnectionId_ActorId_OrganizationId",
                table: "AiReservations",
                columns: new[] { "ConnectionId", "ActorId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_AiReservations_Scope_Provider_CreatedAt",
                table: "AiReservations",
                columns: new[] { "Scope", "Provider", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiInferenceBindings_ConnectionId_OwnerId_OrganizationId",
                table: "AiInferenceBindings",
                columns: new[] { "ConnectionId", "OwnerId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalAiConnections_OwnerId_OrganizationId",
                table: "PersonalAiConnections",
                columns: new[] { "OwnerId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalAiConnections_OwnerId_Provider",
                table: "PersonalAiConnections",
                columns: new[] { "OwnerId", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonalAiOAuths_BrowserHash",
                table: "PersonalAiOAuths",
                column: "BrowserHash");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalAiOAuths_OwnerId_ExpiresAt",
                table: "PersonalAiOAuths",
                columns: new[] { "OwnerId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalAiOAuths_OwnerId_OrganizationId",
                table: "PersonalAiOAuths",
                columns: new[] { "OwnerId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalAiOAuths_StateHash",
                table: "PersonalAiOAuths",
                column: "StateHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonalAiSelections_ConnectionId_OwnerId_OrganizationId",
                table: "PersonalAiSelections",
                columns: new[] { "ConnectionId", "OwnerId", "OrganizationId" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalAiSelections_OwnerId_OrganizationId",
                table: "PersonalAiSelections",
                columns: new[] { "OwnerId", "OrganizationId" });

            migrationBuilder.AddForeignKey(
                name: "FK_AiReservations_PersonalAiConnections_ConnectionId_ActorId_O~",
                table: "AiReservations",
                columns: new[] { "ConnectionId", "ActorId", "OrganizationId" },
                principalTable: "PersonalAiConnections",
                principalColumns: new[] { "Id", "OwnerId", "OrganizationId" },
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                CREATE FUNCTION guard_personal_connection() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Connection history is immutable'; END IF;
                  IF TG_OP='UPDATE' THEN
                    IF ROW(NEW."Id",NEW."OwnerId",NEW."OrganizationId",NEW."Provider",NEW."CreatedAt") IS DISTINCT FROM
                       ROW(OLD."Id",OLD."OwnerId",OLD."OrganizationId",OLD."Provider",OLD."CreatedAt") OR
                       NEW."CredentialRevision" < OLD."CredentialRevision" OR NEW."CredentialRevision" > OLD."CredentialRevision"+1 THEN RAISE EXCEPTION 'Immutable connection owner'; END IF;
                    IF ROW(NEW."ProtectedCredential",NEW."Fingerprint",NEW."Workspace",NEW."Method") IS DISTINCT FROM
                       ROW(OLD."ProtectedCredential",OLD."Fingerprint",OLD."Workspace",OLD."Method") AND NEW."CredentialRevision"<>OLD."CredentialRevision"+1 THEN
                       RAISE EXCEPTION 'Credential change requires a new revision'; END IF;
                  END IF;
                  IF (NEW."Status"='Disconnected' AND NEW."ProtectedCredential"<>'') OR (NEW."Status"='Connected' AND NEW."ProtectedCredential"='') THEN RAISE EXCEPTION 'Invalid credential custody'; END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER personal_connection_integrity BEFORE INSERT OR UPDATE OR DELETE ON "PersonalAiConnections" FOR EACH ROW EXECUTE FUNCTION guard_personal_connection();
                CREATE FUNCTION guard_personal_oauth() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Authorization history is immutable'; END IF;
                  IF TG_OP='UPDATE' THEN
                    IF ROW(NEW."Id",NEW."OwnerId",NEW."OrganizationId",NEW."StateHash",NEW."BrowserHash",NEW."SessionHash",NEW."StampHash",NEW."CreatedAt") IS DISTINCT FROM
                       ROW(OLD."Id",OLD."OwnerId",OLD."OrganizationId",OLD."StateHash",OLD."BrowserHash",OLD."SessionHash",OLD."StampHash",OLD."CreatedAt") OR NEW."ExpiresAt">OLD."ExpiresAt" THEN RAISE EXCEPTION 'Immutable authorization source'; END IF;
                    IF NEW."Status"<>OLD."Status" AND NOT
                      (OLD."Status"='Pending' AND NEW."Status" IN ('Returned','Canceled','Expired') OR OLD."Status"='Returned' AND NEW."Status" IN ('Exchanging','Canceled','Expired') OR OLD."Status"='Exchanging' AND NEW."Status" IN ('Succeeded','Failed','Canceled')) THEN RAISE EXCEPTION 'Invalid authorization transition'; END IF;
                  END IF;
                  IF NEW."Status" IN ('Exchanging','Succeeded','Failed','Canceled','Expired') AND (NEW."ProtectedVerifier"<>'' OR NEW."ProtectedCode" IS NOT NULL) THEN RAISE EXCEPTION 'Consumed authorization retains secret material'; END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER personal_oauth_integrity BEFORE INSERT OR UPDATE OR DELETE ON "PersonalAiOAuths" FOR EACH ROW EXECUTE FUNCTION guard_personal_oauth();
                CREATE FUNCTION guard_ai_binding_source() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF NOT EXISTS (SELECT 1 FROM "PersonalAiConnections" c WHERE c."Id"=NEW."ConnectionId" AND c."OwnerId"=NEW."OwnerId" AND c."OrganizationId"=NEW."OrganizationId" AND c."Provider"=NEW."Provider" AND c."CredentialRevision"=NEW."CredentialRevision" AND c."Status"='Connected') THEN RAISE EXCEPTION 'Invalid credential snapshot'; END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER ai_binding_source BEFORE INSERT ON "AiInferenceBindings" FOR EACH ROW EXECUTE FUNCTION guard_ai_binding_source();
                CREATE TRIGGER ai_binding_immutable BEFORE UPDATE OR DELETE ON "AiInferenceBindings"
                  FOR EACH ROW EXECUTE FUNCTION reject_lesson_snapshot_mutation();
                CREATE FUNCTION guard_personal_ai_source() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE owner_id uuid; organization_id uuid;
                BEGIN
                  IF TG_OP = 'UPDATE' AND NEW."AiBindingId" IS DISTINCT FROM OLD."AiBindingId" THEN
                    RAISE EXCEPTION 'Inference binding is immutable';
                  END IF;
                  IF NEW."AiBindingId" IS NOT NULL THEN
                    IF TG_TABLE_NAME = 'TutoringSessions' THEN owner_id := NEW."StudentId"; organization_id := NEW."OrganizationId";
                    ELSE SELECT "TeacherId","OrganizationId" INTO owner_id,organization_id FROM "Lessons" WHERE "Id"=NEW."LessonId"; END IF;
                    IF NOT EXISTS (SELECT 1 FROM "AiInferenceBindings" b WHERE b."Id"=NEW."AiBindingId" AND b."OwnerId"=owner_id AND b."OrganizationId"=organization_id
                      AND b."Provider"=NEW."ProviderId" AND b."Model"=NEW."ModelId") THEN RAISE EXCEPTION 'Invalid inference owner or model'; END IF;
                  END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER tutoring_ai_source BEFORE INSERT OR UPDATE ON "TutoringSessions" FOR EACH ROW EXECUTE FUNCTION guard_personal_ai_source();
                CREATE TRIGGER lesson_operation_ai_source BEFORE INSERT OR UPDATE ON "LessonOperations" FOR EACH ROW EXECUTE FUNCTION guard_personal_ai_source();
                CREATE TRIGGER lesson_simulation_ai_source BEFORE INSERT OR UPDATE ON "LessonSimulations" FOR EACH ROW EXECUTE FUNCTION guard_personal_ai_source();
                CREATE FUNCTION guard_personal_ai_reservation() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF ROW(NEW."Scope",NEW."ConnectionId",NEW."CredentialRevision",NEW."CredentialFingerprint",NEW."ExternalAccount",NEW."Operation") IS DISTINCT FROM
                     ROW(OLD."Scope",OLD."ConnectionId",OLD."CredentialRevision",OLD."CredentialFingerprint",OLD."ExternalAccount",OLD."Operation") THEN
                    RAISE EXCEPTION 'Inference attribution is immutable'; END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER personal_ai_reservation_immutable BEFORE UPDATE ON "AiReservations" FOR EACH ROW EXECUTE FUNCTION guard_personal_ai_reservation();
                ALTER TABLE "AiReservations" ADD CONSTRAINT "CK_AiReservation_Scope" CHECK
                  (("Scope"='Institutional' AND "ConnectionId" IS NULL AND "CredentialRevision" IS NULL) OR
                   ("Scope" IN ('Personal','PersonalQa') AND "ConnectionId" IS NOT NULL AND "CredentialRevision">0 AND "CredentialFingerprint" IS NOT NULL));
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_LessonOperations_AiInferenceBindings_AiBindingId",
                table: "LessonOperations",
                column: "AiBindingId",
                principalTable: "AiInferenceBindings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LessonSimulations_AiInferenceBindings_AiBindingId",
                table: "LessonSimulations",
                column: "AiBindingId",
                principalTable: "AiInferenceBindings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TutoringSessions_AiInferenceBindings_AiBindingId_StudentId_~",
                table: "TutoringSessions",
                columns: new[] { "AiBindingId", "StudentId", "OrganizationId" },
                principalTable: "AiInferenceBindings",
                principalColumns: new[] { "Id", "OwnerId", "OrganizationId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER ai_binding_immutable ON "AiInferenceBindings";
                DROP FUNCTION guard_personal_connection() CASCADE;
                DROP FUNCTION guard_personal_oauth() CASCADE;
                DROP FUNCTION guard_ai_binding_source() CASCADE;
                DROP FUNCTION guard_personal_ai_source() CASCADE;
                DROP FUNCTION guard_personal_ai_reservation() CASCADE;
                ALTER TABLE "AiReservations" DROP CONSTRAINT "CK_AiReservation_Scope";
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_AiReservations_PersonalAiConnections_ConnectionId_ActorId_O~",
                table: "AiReservations");

            migrationBuilder.DropForeignKey(
                name: "FK_LessonOperations_AiInferenceBindings_AiBindingId",
                table: "LessonOperations");

            migrationBuilder.DropForeignKey(
                name: "FK_LessonSimulations_AiInferenceBindings_AiBindingId",
                table: "LessonSimulations");

            migrationBuilder.DropForeignKey(
                name: "FK_TutoringSessions_AiInferenceBindings_AiBindingId_StudentId_~",
                table: "TutoringSessions");

            migrationBuilder.DropTable(
                name: "AiInferenceBindings");

            migrationBuilder.DropTable(
                name: "PersonalAiModels");

            migrationBuilder.DropTable(
                name: "PersonalAiOAuths");

            migrationBuilder.DropTable(
                name: "PersonalAiSelections");

            migrationBuilder.DropTable(
                name: "PersonalAiConnections");

            migrationBuilder.DropIndex(
                name: "IX_TutoringSessions_AiBindingId_StudentId_OrganizationId",
                table: "TutoringSessions");

            migrationBuilder.DropIndex(
                name: "IX_LessonSimulations_AiBindingId",
                table: "LessonSimulations");

            migrationBuilder.DropIndex(
                name: "IX_LessonOperations_AiBindingId",
                table: "LessonOperations");

            migrationBuilder.DropIndex(
                name: "IX_AiReservations_ConnectionId_ActorId_OrganizationId",
                table: "AiReservations");

            migrationBuilder.DropIndex(
                name: "IX_AiReservations_Scope_Provider_CreatedAt",
                table: "AiReservations");

            migrationBuilder.DropColumn(
                name: "AiBindingId",
                table: "TutoringSessions");

            migrationBuilder.DropColumn(
                name: "AiBindingId",
                table: "LessonSimulations");

            migrationBuilder.DropColumn(
                name: "AiBindingId",
                table: "LessonOperations");

            migrationBuilder.DropColumn(
                name: "ConnectionId",
                table: "AiReservations");

            migrationBuilder.DropColumn(
                name: "CredentialFingerprint",
                table: "AiReservations");

            migrationBuilder.DropColumn(
                name: "CredentialRevision",
                table: "AiReservations");

            migrationBuilder.DropColumn(
                name: "ExternalAccount",
                table: "AiReservations");

            migrationBuilder.DropColumn(
                name: "Operation",
                table: "AiReservations");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "AiReservations");
        }
    }
}

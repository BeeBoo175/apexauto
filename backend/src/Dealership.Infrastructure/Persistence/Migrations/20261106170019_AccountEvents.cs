using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Changes = table.Column<string>(type: "jsonb", nullable: true),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountEvents", x => x.Id);
                    table.CheckConstraint("CK_AccountEvents_Shape", "isfinite(\"RecordedAt\") AND \"Kind\" IN ('UsernameChanged', 'PictureRemoved', 'ProfileEdited', 'EmailChanged', 'BranchTransferred', 'Suspended', 'SuspensionLifted', 'Deactivated', 'Reactivated') AND ((\"Kind\" IN ('Suspended', 'Deactivated', 'BranchTransferred') AND \"Reason\" IS NOT NULL AND \"Reason\" = btrim(\"Reason\") AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" !~ '[[:cntrl:]]') OR (\"Kind\" NOT IN ('Suspended', 'Deactivated', 'BranchTransferred') AND \"Reason\" IS NULL)) AND (\"Changes\" IS NULL OR jsonb_typeof(\"Changes\") = 'array')");
                    table.ForeignKey(
                        name: "FK_AccountEvents_AspNetUsers_ActorId",
                        column: x => x.ActorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountEvents_AspNetUsers_TargetId",
                        column: x => x.TargetId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountEvents_ActorId",
                table: "AccountEvents",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountEvents_TargetId_RecordedAt_Id",
                table: "AccountEvents",
                columns: new[] { "TargetId", "RecordedAt", "Id" });

            migrationBuilder.Sql("""
                CREATE FUNCTION forbid_account_event_change() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'Account history is append-only'
                        USING ERRCODE = '23514', CONSTRAINT = 'CK_AccountEvents_Immutable';
                END $$;
                CREATE TRIGGER account_events_append_only BEFORE UPDATE OR DELETE ON "AccountEvents"
                    FOR EACH ROW EXECUTE FUNCTION forbid_account_event_change();
                CREATE TRIGGER account_events_no_truncate BEFORE TRUNCATE ON "AccountEvents"
                    FOR EACH STATEMENT EXECUTE FUNCTION forbid_account_event_change();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER account_events_no_truncate ON "AccountEvents";
                DROP TRIGGER account_events_append_only ON "AccountEvents";
                DROP FUNCTION forbid_account_event_change();
                """);

            migrationBuilder.DropTable(
                name: "AccountEvents");
        }
    }
}

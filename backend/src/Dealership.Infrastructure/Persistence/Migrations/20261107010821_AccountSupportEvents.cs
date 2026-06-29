using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dealership.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountSupportEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountEvents_Shape",
                table: "AccountEvents");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountEvents_Shape",
                table: "AccountEvents",
                sql: "isfinite(\"RecordedAt\") AND \"Kind\" IN ('UsernameChanged', 'PictureRemoved', 'ProfileEdited', 'EmailChanged', 'BranchTransferred', 'Suspended', 'SuspensionLifted', 'Deactivated', 'Reactivated', 'NricRevealed', 'SessionsRevoked', 'VerificationResent', 'ResetSent') AND ((\"Kind\" IN ('Suspended', 'Deactivated', 'BranchTransferred', 'NricRevealed', 'SessionsRevoked', 'VerificationResent', 'ResetSent', 'ProfileEdited', 'EmailChanged') AND \"Reason\" IS NOT NULL AND \"Reason\" = btrim(\"Reason\") AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" !~ '[[:cntrl:]]') OR (\"Kind\" NOT IN ('Suspended', 'Deactivated', 'BranchTransferred', 'NricRevealed', 'SessionsRevoked', 'VerificationResent', 'ResetSent', 'ProfileEdited', 'EmailChanged') AND \"Reason\" IS NULL)) AND (\"Changes\" IS NULL OR jsonb_typeof(\"Changes\") = 'array')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountEvents_Shape",
                table: "AccountEvents");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountEvents_Shape",
                table: "AccountEvents",
                sql: "isfinite(\"RecordedAt\") AND \"Kind\" IN ('UsernameChanged', 'PictureRemoved', 'ProfileEdited', 'EmailChanged', 'BranchTransferred', 'Suspended', 'SuspensionLifted', 'Deactivated', 'Reactivated') AND ((\"Kind\" IN ('Suspended', 'Deactivated', 'BranchTransferred') AND \"Reason\" IS NOT NULL AND \"Reason\" = btrim(\"Reason\") AND length(\"Reason\") BETWEEN 1 AND 1000 AND \"Reason\" !~ '[[:cntrl:]]') OR (\"Kind\" NOT IN ('Suspended', 'Deactivated', 'BranchTransferred') AND \"Reason\" IS NULL)) AND (\"Changes\" IS NULL OR jsonb_typeof(\"Changes\") = 'array')");
        }
    }
}

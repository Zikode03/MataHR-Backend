using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MataHR.EntityFrameworkCore.Workforce.Migrations
{
    /// <inheritdoc />
    public partial class EnforcePresenceTenantRelationships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_MataPresenceChallenge_TenantId_Id",
                table: "MataPresenceChallenge",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_MataWorkSession_TenantId_PresenceChallengeId",
                table: "MataWorkSession",
                columns: new[] { "TenantId", "PresenceChallengeId" });

            migrationBuilder.AddForeignKey(
                name: "FK_MataPresenceChallenge_MataClockLocation_TenantId_LocationId",
                table: "MataPresenceChallenge",
                columns: new[] { "TenantId", "LocationId" },
                principalTable: "MataClockLocation",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MataWorkSession_MataPresenceChallenge_TenantId_PresenceChallengeId",
                table: "MataWorkSession",
                columns: new[] { "TenantId", "PresenceChallengeId" },
                principalTable: "MataPresenceChallenge",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MataPresenceChallenge_MataClockLocation_TenantId_LocationId",
                table: "MataPresenceChallenge");

            migrationBuilder.DropForeignKey(
                name: "FK_MataWorkSession_MataPresenceChallenge_TenantId_PresenceChallengeId",
                table: "MataWorkSession");

            migrationBuilder.DropIndex(
                name: "IX_MataWorkSession_TenantId_PresenceChallengeId",
                table: "MataWorkSession");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_MataPresenceChallenge_TenantId_Id",
                table: "MataPresenceChallenge");
        }
    }
}

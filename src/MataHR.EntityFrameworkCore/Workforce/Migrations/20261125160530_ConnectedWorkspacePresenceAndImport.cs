using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MataHR.EntityFrameworkCore.Workforce.Migrations
{
    /// <inheritdoc />
    public partial class ConnectedWorkspacePresenceAndImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AccuracyMeters",
                table: "MataWorkSession",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DistanceMeters",
                table: "MataWorkSession",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExceptionReason",
                table: "MataWorkSession",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "PresenceChallengeId",
                table: "MataWorkSession",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "MataClockLocation",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "MataClockLocation",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RadiusMeters",
                table: "MataClockLocation",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MataLegacyImportBatch",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PreparedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CommittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MataLegacyImportBatch", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MataOrganisationCanvas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    X = table.Column<double>(type: "float", nullable: false),
                    Y = table.Column<double>(type: "float", nullable: false),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MataOrganisationCanvas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MataPresenceChallenge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MataPresenceChallenge", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MataWorkSession_TenantId_EmployeeId_PresenceChallengeId",
                table: "MataWorkSession",
                columns: new[] { "TenantId", "EmployeeId", "PresenceChallengeId" },
                unique: true,
                filter: "[PresenceChallengeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MataOrganisationCanvas_TenantId",
                table: "MataOrganisationCanvas",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MataPresenceChallenge_TenantId_LocationId_ExpiresAt",
                table: "MataPresenceChallenge",
                columns: new[] { "TenantId", "LocationId", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MataLegacyImportBatch");

            migrationBuilder.DropTable(
                name: "MataOrganisationCanvas");

            migrationBuilder.DropTable(
                name: "MataPresenceChallenge");

            migrationBuilder.DropIndex(
                name: "IX_MataWorkSession_TenantId_EmployeeId_PresenceChallengeId",
                table: "MataWorkSession");

            migrationBuilder.DropColumn(
                name: "AccuracyMeters",
                table: "MataWorkSession");

            migrationBuilder.DropColumn(
                name: "DistanceMeters",
                table: "MataWorkSession");

            migrationBuilder.DropColumn(
                name: "ExceptionReason",
                table: "MataWorkSession");

            migrationBuilder.DropColumn(
                name: "PresenceChallengeId",
                table: "MataWorkSession");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "MataClockLocation");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "MataClockLocation");

            migrationBuilder.DropColumn(
                name: "RadiusMeters",
                table: "MataClockLocation");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MataHR.EntityFrameworkCore.Workforce.Migrations
{
    /// <inheritdoc />
    public partial class AddEmploymentHistoryAndLeaveLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AfterStateJson",
                table: "MataEmploymentChange",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BeforeStateJson",
                table: "MataEmploymentChange",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM MataAbsencePolicy WHERE LEN(Name)>160) THROW 51000, 'Policy names exceeding 160 characters require reconciliation before migration.', 1;");
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "MataAbsencePolicy",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_MataLeaveAllocation_TenantId_Id",
                table: "MataLeaveAllocation",
                columns: new[] { "TenantId", "Id" });

            migrationBuilder.CreateTable(
                name: "MataLeaveBalanceEntry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Delta = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BalanceAfter = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BeforeStateJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AfterStateJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "{}"),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MataLeaveBalanceEntry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MataLeaveBalanceEntry_MataLeaveAllocation_TenantId_AllocationId",
                        columns: x => new { x.TenantId, x.AllocationId },
                        principalTable: "MataLeaveAllocation",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MataAbsencePolicy_TenantId_Name_Version",
                table: "MataAbsencePolicy",
                columns: new[] { "TenantId", "Name", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MataLeaveBalanceEntry_TenantId_AllocationId",
                table: "MataLeaveBalanceEntry",
                columns: new[] { "TenantId", "AllocationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MataLeaveBalanceEntry");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_MataLeaveAllocation_TenantId_Id",
                table: "MataLeaveAllocation");

            migrationBuilder.DropIndex(
                name: "IX_MataAbsencePolicy_TenantId_Name_Version",
                table: "MataAbsencePolicy");

            migrationBuilder.DropColumn(
                name: "AfterStateJson",
                table: "MataEmploymentChange");

            migrationBuilder.DropColumn(
                name: "BeforeStateJson",
                table: "MataEmploymentChange");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "MataAbsencePolicy",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160);
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CpPrinting.Api.Migrations
{
    /// <inheritdoc />
    public partial class RestructureAuditRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdNo",
                table: "AuditRecords");

            migrationBuilder.DropColumn(
                name: "AdviceNoteId",
                table: "AuditRecords");

            migrationBuilder.DropColumn(
                name: "DeliveryStatus",
                table: "AuditRecords");

            migrationBuilder.DropColumn(
                name: "DeliveryTrackerReportId",
                table: "AuditRecords");

            migrationBuilder.DropColumn(
                name: "ProductionRecordId",
                table: "AuditRecords");

            migrationBuilder.RenameColumn(
                name: "TotalQty",
                table: "AuditRecords",
                newName: "ReleaseQty");

            migrationBuilder.AlterColumn<string>(
                name: "Bundles",
                table: "AuditRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ReleaseQty",
                table: "AuditRecords",
                newName: "TotalQty");

            migrationBuilder.AlterColumn<string>(
                name: "Bundles",
                table: "AuditRecords",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "AdNo",
                table: "AuditRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AdviceNoteId",
                table: "AuditRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryStatus",
                table: "AuditRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryTrackerReportId",
                table: "AuditRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProductionRecordId",
                table: "AuditRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}

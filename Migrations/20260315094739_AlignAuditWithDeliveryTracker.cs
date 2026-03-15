using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CpPrinting.Api.Migrations
{
    /// <inheritdoc />
    public partial class AlignAuditWithDeliveryTracker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "AuditorName",
                table: "AuditRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
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

            migrationBuilder.AddColumn<int>(
                name: "RevisionNo",
                table: "AuditRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StoreInRecordId",
                table: "AuditRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SubmissionId",
                table: "AuditRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdNo",
                table: "AuditRecords");

            migrationBuilder.DropColumn(
                name: "AdviceNoteId",
                table: "AuditRecords");

            migrationBuilder.DropColumn(
                name: "AuditorName",
                table: "AuditRecords");

            migrationBuilder.DropColumn(
                name: "CustomerName",
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

            migrationBuilder.DropColumn(
                name: "RevisionNo",
                table: "AuditRecords");

            migrationBuilder.DropColumn(
                name: "StoreInRecordId",
                table: "AuditRecords");

            migrationBuilder.DropColumn(
                name: "SubmissionId",
                table: "AuditRecords");
        }
    }
}

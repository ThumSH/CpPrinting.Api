using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CpPrinting.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnforceGatepassBeforeDeliveryTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdNo",
                table: "DeliveryTrackers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AdviceNoteId",
                table: "DeliveryTrackers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "BalanceQty",
                table: "DeliveryTrackers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "DeliveryTrackers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DeliveryQty",
                table: "DeliveryTrackers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryStatus",
                table: "DeliveryTrackers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProductionRecordId",
                table: "DeliveryTrackers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RevisionNo",
                table: "DeliveryTrackers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StoreInRecordId",
                table: "DeliveryTrackers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SubmissionId",
                table: "DeliveryTrackers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdNo",
                table: "DeliveryTrackers");

            migrationBuilder.DropColumn(
                name: "AdviceNoteId",
                table: "DeliveryTrackers");

            migrationBuilder.DropColumn(
                name: "BalanceQty",
                table: "DeliveryTrackers");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "DeliveryTrackers");

            migrationBuilder.DropColumn(
                name: "DeliveryQty",
                table: "DeliveryTrackers");

            migrationBuilder.DropColumn(
                name: "DeliveryStatus",
                table: "DeliveryTrackers");

            migrationBuilder.DropColumn(
                name: "ProductionRecordId",
                table: "DeliveryTrackers");

            migrationBuilder.DropColumn(
                name: "RevisionNo",
                table: "DeliveryTrackers");

            migrationBuilder.DropColumn(
                name: "StoreInRecordId",
                table: "DeliveryTrackers");

            migrationBuilder.DropColumn(
                name: "SubmissionId",
                table: "DeliveryTrackers");
        }
    }
}

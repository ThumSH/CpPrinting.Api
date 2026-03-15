using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CpPrinting.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnforceProductionBeforeGatepass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BalanceQty",
                table: "AdviceNotes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "AdviceNotes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DispatchQty",
                table: "AdviceNotes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ProductionRecordId",
                table: "AdviceNotes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RevisionNo",
                table: "AdviceNotes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StoreInRecordId",
                table: "AdviceNotes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SubmissionId",
                table: "AdviceNotes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BalanceQty",
                table: "AdviceNotes");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "AdviceNotes");

            migrationBuilder.DropColumn(
                name: "DispatchQty",
                table: "AdviceNotes");

            migrationBuilder.DropColumn(
                name: "ProductionRecordId",
                table: "AdviceNotes");

            migrationBuilder.DropColumn(
                name: "RevisionNo",
                table: "AdviceNotes");

            migrationBuilder.DropColumn(
                name: "StoreInRecordId",
                table: "AdviceNotes");

            migrationBuilder.DropColumn(
                name: "SubmissionId",
                table: "AdviceNotes");
        }
    }
}

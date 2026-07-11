using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CpPrinting.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkerJobCompletionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompletedAt",
                table: "DailyOutputRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CompletedBy",
                table: "DailyOutputRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsJobCompleted",
                table: "DailyOutputRecords",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "DailyOutputRecords");

            migrationBuilder.DropColumn(
                name: "CompletedBy",
                table: "DailyOutputRecords");

            migrationBuilder.DropColumn(
                name: "IsJobCompleted",
                table: "DailyOutputRecords");
        }
    }
}

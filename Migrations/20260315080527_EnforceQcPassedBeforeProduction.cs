using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CpPrinting.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnforceQcPassedBeforeProduction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "StoreProductionRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RevisionNo",
                table: "StoreProductionRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StoreInRecordId",
                table: "StoreProductionRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SubmissionId",
                table: "StoreProductionRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "StoreProductionRecords");

            migrationBuilder.DropColumn(
                name: "RevisionNo",
                table: "StoreProductionRecords");

            migrationBuilder.DropColumn(
                name: "StoreInRecordId",
                table: "StoreProductionRecords");

            migrationBuilder.DropColumn(
                name: "SubmissionId",
                table: "StoreProductionRecords");
        }
    }
}

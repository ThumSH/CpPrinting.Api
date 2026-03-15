using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CpPrinting.Api.Migrations
{
    /// <inheritdoc />
    public partial class LinkCpiToStoreInAndAddInspectionStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InspectionStatus",
                table: "CpiReports",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RevisionNo",
                table: "CpiReports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StoreInRecordId",
                table: "CpiReports",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SubmissionId",
                table: "CpiReports",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InspectionStatus",
                table: "CpiReports");

            migrationBuilder.DropColumn(
                name: "RevisionNo",
                table: "CpiReports");

            migrationBuilder.DropColumn(
                name: "StoreInRecordId",
                table: "CpiReports");

            migrationBuilder.DropColumn(
                name: "SubmissionId",
                table: "CpiReports");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CpPrinting.Api.Migrations
{
    /// <inheritdoc />
    public partial class RestructureCpiReport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "InspectionRows",
                table: "CpiReports",
                newName: "CutInspections");

            migrationBuilder.AddColumn<string>(
                name: "CpiAuditor",
                table: "CpiReports",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CpiAuditor",
                table: "CpiReports");

            migrationBuilder.RenameColumn(
                name: "CutInspections",
                table: "CpiReports",
                newName: "InspectionRows");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CpPrinting.Api.Migrations
{
    /// <inheritdoc />
    public partial class ComponentArchitecture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Placements",
                table: "SampleStyles",
                newName: "Component");

            migrationBuilder.RenameColumn(
                name: "Placements",
                table: "DevelopmentJobs",
                newName: "Component");

            migrationBuilder.AddColumn<string>(
                name: "SubmissionId",
                table: "CutRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubmissionId",
                table: "CutRecords");

            migrationBuilder.RenameColumn(
                name: "Component",
                table: "SampleStyles",
                newName: "Placements");

            migrationBuilder.RenameColumn(
                name: "Component",
                table: "DevelopmentJobs",
                newName: "Placements");
        }
    }
}

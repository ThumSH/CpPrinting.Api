using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CpPrinting.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBundleOrderToBundleRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BundleOrder",
                table: "BundleRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BundleOrder",
                table: "BundleRecords");
        }
    }
}

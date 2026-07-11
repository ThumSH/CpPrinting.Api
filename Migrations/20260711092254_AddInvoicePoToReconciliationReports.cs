using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CpPrinting.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoicePoToReconciliationReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InvoiceNo",
                table: "ReconciliationReports",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PoNo",
                table: "ReconciliationReports",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InvoiceNo",
                table: "ReconciliationReports");

            migrationBuilder.DropColumn(
                name: "PoNo",
                table: "ReconciliationReports");
        }
    }
}

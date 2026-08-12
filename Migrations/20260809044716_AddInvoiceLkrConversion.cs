using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CpPrinting.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceLkrConversion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExchangeRate",
                table: "TaxInvoices",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TotalAmountIncludingVatLkr",
                table: "TaxInvoices",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TotalValueOfSupplyLkr",
                table: "TaxInvoices",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "VatAmountLkr",
                table: "TaxInvoices",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "TaxInvoices");

            migrationBuilder.DropColumn(
                name: "TotalAmountIncludingVatLkr",
                table: "TaxInvoices");

            migrationBuilder.DropColumn(
                name: "TotalValueOfSupplyLkr",
                table: "TaxInvoices");

            migrationBuilder.DropColumn(
                name: "VatAmountLkr",
                table: "TaxInvoices");
        }
    }
}

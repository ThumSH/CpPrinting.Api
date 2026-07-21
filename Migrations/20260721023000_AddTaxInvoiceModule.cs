using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CpPrinting.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTaxInvoiceModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvoiceSecuritySettings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false, defaultValue: "invoice-security"),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceSecuritySettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaxInvoices",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    InvoiceDate = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SupplierTin = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SupplierName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SupplierAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SupplierTelephone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PurchaserTin = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PurchaserName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PurchaserAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PurchaserTelephone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeliveryDate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlaceOfSupply = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdditionalInformation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalValueOfSupply = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VatAmount = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalAmountIncludingVat = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalAmountInWords = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModeOfPayment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxInvoices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaxInvoiceItems",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TaxInvoiceId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RowOrder = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UnitPrice = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AmountExcludingVat = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxInvoiceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxInvoiceItems_TaxInvoices_TaxInvoiceId",
                        column: x => x.TaxInvoiceId,
                        principalTable: "TaxInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaxInvoiceItems_TaxInvoiceId",
                table: "TaxInvoiceItems",
                column: "TaxInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxInvoices_CreatedAt",
                table: "TaxInvoices",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TaxInvoices_InvoiceDate",
                table: "TaxInvoices",
                column: "InvoiceDate");

            migrationBuilder.CreateIndex(
                name: "IX_TaxInvoices_InvoiceNumber",
                table: "TaxInvoices",
                column: "InvoiceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaxInvoices_PurchaserTin",
                table: "TaxInvoices",
                column: "PurchaserTin");

            migrationBuilder.CreateIndex(
                name: "IX_TaxInvoices_SupplierTin",
                table: "TaxInvoices",
                column: "SupplierTin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceSecuritySettings");

            migrationBuilder.DropTable(
                name: "TaxInvoiceItems");

            migrationBuilder.DropTable(
                name: "TaxInvoices");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CpPrinting.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddQcModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CpiReports",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Date = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Customer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StyleNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScheduleNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BodyColour = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrintColour = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReceivedQty = table.Column<int>(type: "int", nullable: false),
                    CpiQty = table.Column<int>(type: "int", nullable: false),
                    InspectionRows = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CuttingQty = table.Column<int>(type: "int", nullable: false),
                    CheckedQty = table.Column<int>(type: "int", nullable: false),
                    RejDamageQty = table.Column<int>(type: "int", nullable: false),
                    RejectionPercentage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BalanceQty = table.Column<int>(type: "int", nullable: false),
                    AppRej = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CheckedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SummaryDate = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CpiReports", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CpiReports");
        }
    }
}

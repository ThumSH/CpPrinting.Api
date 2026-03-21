using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CpPrinting.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkerDailyOutput : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyOutputRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StoreInRecordId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmissionId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Date = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StyleNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Component = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OrderQty = table.Column<int>(type: "int", nullable: false),
                    TableNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Target = table.Column<int>(type: "int", nullable: false),
                    DailyTarget = table.Column<int>(type: "int", nullable: false),
                    TimeSlots = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalSeating = table.Column<int>(type: "int", nullable: false),
                    TotalPrinting = table.Column<int>(type: "int", nullable: false),
                    TotalCuring = table.Column<int>(type: "int", nullable: false),
                    TotalChecking = table.Column<int>(type: "int", nullable: false),
                    TotalPacking = table.Column<int>(type: "int", nullable: false),
                    TotalDispatch = table.Column<int>(type: "int", nullable: false),
                    WorkerName = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyOutputRecords", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyOutputRecords");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CpPrinting.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkerCutReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkerCutReports",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StoreInRecordId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProductionRecordId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmissionId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RevisionNo = table.Column<int>(type: "int", nullable: false),
                    StyleNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BodyColour = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrintColour = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Component = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Season = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InAdNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScheduleNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    JobNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CutInDate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InQty = table.Column<int>(type: "int", nullable: false),
                    TotalCutQty = table.Column<int>(type: "int", nullable: false),
                    CutNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CutQty = table.Column<int>(type: "int", nullable: false),
                    BundleCount = table.Column<int>(type: "int", nullable: false),
                    ReportDate = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    WorkerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkerCutReports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkerCutReports_CustomerName",
                table: "WorkerCutReports",
                column: "CustomerName");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerCutReports_CutNo",
                table: "WorkerCutReports",
                column: "CutNo");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerCutReports_ReportDate",
                table: "WorkerCutReports",
                column: "ReportDate");

            migrationBuilder.CreateIndex(
                name: "IX_WorkerCutReports_StoreInRecordId_CutNo",
                table: "WorkerCutReports",
                columns: new[] { "StoreInRecordId", "CutNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkerCutReports_StyleNo",
                table: "WorkerCutReports",
                column: "StyleNo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkerCutReports");
        }
    }
}

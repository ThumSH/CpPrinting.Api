using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CpPrinting.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDowntimeRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DowntimeRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Date = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StyleNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StoreInRecordId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmissionId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WorkerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Entries = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalHours = table.Column<double>(type: "float", nullable: false),
                    FullyAcknowledged = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DowntimeRecords", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DowntimeRecords");
        }
    }
}

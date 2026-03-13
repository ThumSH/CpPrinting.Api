using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CpPrinting.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Approvals",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SubmissionId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StyleNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Level = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BoardSet = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovalCard = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RaMeetingDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BulkOrderQty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewedAt = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Approvals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DevelopmentJobs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Customer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StyleNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Season = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrintingTechnique = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArtworkFileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ArtworkPreviewUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WashingStandard = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BodyColour = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrintColour = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrintColourQty = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SampleOrderedDate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SampleDeliveryDate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Placements = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DevelopmentJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoreInRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StyleNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScheduleNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CutNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BodyColour = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrintColour = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Components = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Season = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CutInDate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BulkQty = table.Column<int>(type: "int", nullable: false),
                    InQty = table.Column<int>(type: "int", nullable: false),
                    BalanceBulkQty = table.Column<int>(type: "int", nullable: false),
                    CutQty = table.Column<int>(type: "int", nullable: false),
                    AvailableQty = table.Column<int>(type: "int", nullable: false),
                    BundleQty = table.Column<int>(type: "int", nullable: false),
                    NumberRange = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Size = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreInRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Submissions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StyleNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmissionDate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Level = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Submissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Approvals");

            migrationBuilder.DropTable(
                name: "DevelopmentJobs");

            migrationBuilder.DropTable(
                name: "StoreInRecords");

            migrationBuilder.DropTable(
                name: "Submissions");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}

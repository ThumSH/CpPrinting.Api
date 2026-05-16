using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CpPrinting.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSampleStyle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SampleStyles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DevelopmentJobId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Customer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StyleNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Season = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrintingTechnique = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BodyColour = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrintColour = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrintColourQty = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WashingStandard = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Placements = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImagePath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClientApproved = table.Column<bool>(type: "bit", nullable: false),
                    ClientApprovedAt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClientApprovedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdminStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdminRemarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdminActionAt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdminActionBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RcMeetingDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AcNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BoardSet = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BulkQty = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubmittedToAdmin = table.Column<bool>(type: "bit", nullable: false),
                    SubmittedAt = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SampleStyles", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SampleStyles");
        }
    }
}

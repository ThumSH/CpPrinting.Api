using System;
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
                name: "AdviceNotes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProductionRecordId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StoreInRecordId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmissionId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RevisionNo = table.Column<int>(type: "int", nullable: false),
                    AdNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeliveryDate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Attn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StyleNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScheduleNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CutNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Component = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DispatchQty = table.Column<int>(type: "int", nullable: false),
                    BalanceQty = table.Column<int>(type: "int", nullable: false),
                    Rows = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReceivedByName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrepByName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuthByName = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdviceNotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Approvals",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SubmissionId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StyleNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Level = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RevisionNo = table.Column<int>(type: "int", nullable: false),
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
                name: "AuditRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DeliveryTrackerReportId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdviceNoteId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductionRecordId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StoreInRecordId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmissionId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RevisionNo = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StyleNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ScheduleNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CutNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Colour = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeliveryStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sizes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalQty = table.Column<int>(type: "int", nullable: false),
                    AuditQty = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AuditorName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Bundles = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CpiReports",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StoreInRecordId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmissionId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RevisionNo = table.Column<int>(type: "int", nullable: false),
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
                    InspectionStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AppRej = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CheckedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SummaryDate = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CpiReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryTrackers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AdviceNoteId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductionRecordId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StoreInRecordId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmissionId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RevisionNo = table.Column<int>(type: "int", nullable: false),
                    StyleNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AdNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FpoNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OrderQty = table.Column<int>(type: "int", nullable: false),
                    DeliveryQty = table.Column<int>(type: "int", nullable: false),
                    BalanceQty = table.Column<int>(type: "int", nullable: false),
                    DeliveryStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Rows = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryTrackers", x => x.Id);
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
                name: "StoreInEntries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SubmissionId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RevisionNo = table.Column<int>(type: "int", nullable: false),
                    StyleNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BodyColour = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrintColour = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Components = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Season = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScheduleNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CutInDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BulkQty = table.Column<int>(type: "int", nullable: false),
                    InQty = table.Column<int>(type: "int", nullable: false),
                    BalanceBulkQty = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreInEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoreInRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SubmissionId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RevisionNo = table.Column<int>(type: "int", nullable: false),
                    StyleNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScheduleNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CutNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BodyColour = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrintColour = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Components = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Season = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CutInDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BulkQty = table.Column<int>(type: "int", nullable: false),
                    InQty = table.Column<int>(type: "int", nullable: false),
                    BalanceBulkQty = table.Column<int>(type: "int", nullable: false),
                    CutQty = table.Column<int>(type: "int", nullable: false),
                    AvailableQty = table.Column<int>(type: "int", nullable: false),
                    BundleQty = table.Column<int>(type: "int", nullable: false),
                    NumberRange = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Size = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreInRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoreProductionRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StoreInRecordId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubmissionId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RevisionNo = table.Column<int>(type: "int", nullable: false),
                    IssueDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StyleNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Components = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CutNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IssueQty = table.Column<int>(type: "int", nullable: false),
                    BalanceQty = table.Column<int>(type: "int", nullable: false),
                    LineNo = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreProductionRecords", x => x.Id);
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
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RevisionNo = table.Column<int>(type: "int", nullable: false),
                    IsLatestRevision = table.Column<bool>(type: "bit", nullable: false)
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
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoreInCutEntries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StoreInEntryId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CutNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Size = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CutQty = table.Column<int>(type: "int", nullable: false),
                    RemainingCutQty = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreInCutEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoreInCutEntries_StoreInEntries_StoreInEntryId",
                        column: x => x.StoreInEntryId,
                        principalTable: "StoreInEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoreInBundleEntries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StoreInCutEntryId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BundleNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BundleQty = table.Column<int>(type: "int", nullable: false),
                    NumberRange = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreInBundleEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoreInBundleEntries_StoreInCutEntries_StoreInCutEntryId",
                        column: x => x.StoreInCutEntryId,
                        principalTable: "StoreInCutEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoreInBundleEntries_StoreInCutEntryId_BundleNo",
                table: "StoreInBundleEntries",
                columns: new[] { "StoreInCutEntryId", "BundleNo" });

            migrationBuilder.CreateIndex(
                name: "IX_StoreInCutEntries_StoreInEntryId_CutNo",
                table: "StoreInCutEntries",
                columns: new[] { "StoreInEntryId", "CutNo" });

            migrationBuilder.CreateIndex(
                name: "IX_StoreInEntries_SubmissionId_ScheduleNo",
                table: "StoreInEntries",
                columns: new[] { "SubmissionId", "ScheduleNo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdviceNotes");

            migrationBuilder.DropTable(
                name: "Approvals");

            migrationBuilder.DropTable(
                name: "AuditRecords");

            migrationBuilder.DropTable(
                name: "CpiReports");

            migrationBuilder.DropTable(
                name: "DeliveryTrackers");

            migrationBuilder.DropTable(
                name: "DevelopmentJobs");

            migrationBuilder.DropTable(
                name: "StoreInBundleEntries");

            migrationBuilder.DropTable(
                name: "StoreInRecords");

            migrationBuilder.DropTable(
                name: "StoreProductionRecords");

            migrationBuilder.DropTable(
                name: "Submissions");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "StoreInCutEntries");

            migrationBuilder.DropTable(
                name: "StoreInEntries");
        }
    }
}

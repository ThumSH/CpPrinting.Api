using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CpPrinting.Api.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeStoreInCutsBundles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoreInBundleEntries");

            migrationBuilder.DropTable(
                name: "StoreInCutEntries");

            migrationBuilder.DropTable(
                name: "StoreInEntries");

            migrationBuilder.DropColumn(
                name: "CutNo",
                table: "StoreInRecords");

            migrationBuilder.DropColumn(
                name: "NumberRange",
                table: "StoreInRecords");

            migrationBuilder.DropColumn(
                name: "Size",
                table: "StoreInRecords");

            migrationBuilder.RenameColumn(
                name: "CutQty",
                table: "StoreInRecords",
                newName: "UncutBalance");

            migrationBuilder.RenameColumn(
                name: "BundleQty",
                table: "StoreInRecords",
                newName: "TotalCutQty");

            migrationBuilder.CreateTable(
                name: "CutRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StoreInRecordId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CutNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CutQty = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CutRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CutRecords_StoreInRecords_StoreInRecordId",
                        column: x => x.StoreInRecordId,
                        principalTable: "StoreInRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BundleRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CutRecordId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BundleNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BundleQty = table.Column<int>(type: "int", nullable: false),
                    Size = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NumberRange = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BundleRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BundleRecords_CutRecords_CutRecordId",
                        column: x => x.CutRecordId,
                        principalTable: "CutRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BundleRecords_CutRecordId",
                table: "BundleRecords",
                column: "CutRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CutRecords_StoreInRecordId",
                table: "CutRecords",
                column: "StoreInRecordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BundleRecords");

            migrationBuilder.DropTable(
                name: "CutRecords");

            migrationBuilder.RenameColumn(
                name: "UncutBalance",
                table: "StoreInRecords",
                newName: "CutQty");

            migrationBuilder.RenameColumn(
                name: "TotalCutQty",
                table: "StoreInRecords",
                newName: "BundleQty");

            migrationBuilder.AddColumn<string>(
                name: "CutNo",
                table: "StoreInRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "NumberRange",
                table: "StoreInRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Size",
                table: "StoreInRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StoreInEntries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BalanceBulkQty = table.Column<int>(type: "int", nullable: false),
                    BodyColour = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BulkQty = table.Column<int>(type: "int", nullable: false),
                    Components = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CutInDate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InQty = table.Column<int>(type: "int", nullable: false),
                    PrintColour = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RevisionNo = table.Column<int>(type: "int", nullable: false),
                    ScheduleNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Season = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StyleNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubmissionId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreInEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoreInCutEntries",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StoreInEntryId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CutNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CutQty = table.Column<int>(type: "int", nullable: false),
                    RemainingCutQty = table.Column<int>(type: "int", nullable: false),
                    Size = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CpPrinting.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSessionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserSessionEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SessionId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserRole = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Timestamp = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IdleSeconds = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSessionEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserSessions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserRole = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginAt = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LogoutAt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastSeenAt = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalLoginSeconds = table.Column<int>(type: "int", nullable: false),
                    TotalActiveSeconds = table.Column<int>(type: "int", nullable: false),
                    TotalIdleSeconds = table.Column<int>(type: "int", nullable: false),
                    LogoutType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LogoutReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSessionEvents_SessionId",
                table: "UserSessionEvents",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessionEvents_Timestamp",
                table: "UserSessionEvents",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessionEvents_UserId",
                table: "UserSessionEvents",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_IsActive",
                table: "UserSessions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_LoginAt",
                table: "UserSessions",
                column: "LoginAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_UserId",
                table: "UserSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_UserRole",
                table: "UserSessions",
                column: "UserRole");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserSessionEvents");

            migrationBuilder.DropTable(
                name: "UserSessions");
        }
    }
}

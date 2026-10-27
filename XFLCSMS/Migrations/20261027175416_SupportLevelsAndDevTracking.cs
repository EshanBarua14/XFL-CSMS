using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFLCSMS.Migrations
{
    public partial class SupportLevelsAndDevTracking : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IX_Issues_ClosedOn and IX_Issues_TDate already exist (TicketDateIndexes); only the model snapshot had lost them.

            migrationBuilder.AddColumn<int>(
                name: "SupportLevel",
                table: "Users",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DevEta",
                table: "Issues",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DevOwnerUserId",
                table: "Issues",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DevReference",
                table: "Issues",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DevStage",
                table: "Issues",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DevStageSince",
                table: "Issues",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LevelSince",
                table: "Issues",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SupportLevel",
                table: "Issues",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // every existing Support Engineer starts at L1; the administrator sets the real levels under Setup > Support Levels
            migrationBuilder.Sql("UPDATE Users SET SupportLevel = 1 WHERE Department = 'Support Engineer' AND SupportLevel IS NULL");

            migrationBuilder.CreateTable(
                name: "SupportLevels",
                columns: table => new
                {
                    Level = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    MaxHoursAtLevel = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportLevels", x => x.Level);
                });

            migrationBuilder.CreateTable(
                name: "TicketDevUpdates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IssueId = table.Column<int>(type: "int", nullable: false),
                    FromStage = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Stage = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OwnerUserId = table.Column<int>(type: "int", nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Eta = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    VisibleToClient = table.Column<bool>(type: "bit", nullable: false),
                    ByUserId = table.Column<int>(type: "int", nullable: true),
                    ByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    On = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketDevUpdates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketDevUpdates_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "IssueId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketEscalations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IssueId = table.Column<int>(type: "int", nullable: false),
                    FromLevel = table.Column<int>(type: "int", nullable: false),
                    ToLevel = table.Column<int>(type: "int", nullable: false),
                    FromEngineerUserId = table.Column<int>(type: "int", nullable: true),
                    FromEngineerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ToEngineerUserId = table.Column<int>(type: "int", nullable: true),
                    ToEngineerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ByUserId = table.Column<int>(type: "int", nullable: true),
                    ByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    On = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketEscalations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketEscalations_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "IssueId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "SupportLevels",
                columns: new[] { "Level", "Description", "MaxHoursAtLevel", "Name" },
                values: new object[,]
                {
                    { 1, "First contact: logs and classifies the ticket, answers how-to questions, applies known fixes and workarounds.", 4, "Service Desk" },
                    { 2, "Deeper troubleshooting: configuration, accounts and access, logs, reproducing the problem.", 24, "Technical Support" },
                    { 3, "Expert analysis of the product: data fixes, integrations, root cause of recurring problems.", 72, "Product Specialist" },
                    { 4, "Engineering: bug fixes and changes to the code, followed through the development stages to deployment.", 240, "Development" },
                    { 5, "Architects, infrastructure and third parties (exchange, depository, vendors) for problems outside XFL's code.", 0, "Architecture & Vendors" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Issues_DevOwnerUserId",
                table: "Issues",
                column: "DevOwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Issues_DevStage",
                table: "Issues",
                column: "DevStage");

            migrationBuilder.CreateIndex(
                name: "IX_Issues_SupportLevel",
                table: "Issues",
                column: "SupportLevel");

            migrationBuilder.CreateIndex(
                name: "IX_TicketDevUpdates_IssueId_On",
                table: "TicketDevUpdates",
                columns: new[] { "IssueId", "On" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketEscalations_IssueId_On",
                table: "TicketEscalations",
                columns: new[] { "IssueId", "On" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketEscalations_On",
                table: "TicketEscalations",
                column: "On");

            migrationBuilder.AddForeignKey(
                name: "FK_Issues_Users_DevOwnerUserId",
                table: "Issues",
                column: "DevOwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Issues_Users_DevOwnerUserId",
                table: "Issues");

            migrationBuilder.DropTable(
                name: "SupportLevels");

            migrationBuilder.DropTable(
                name: "TicketDevUpdates");

            migrationBuilder.DropTable(
                name: "TicketEscalations");

            migrationBuilder.DropIndex(
                name: "IX_Issues_DevOwnerUserId",
                table: "Issues");

            migrationBuilder.DropIndex(
                name: "IX_Issues_DevStage",
                table: "Issues");

            migrationBuilder.DropIndex(
                name: "IX_Issues_SupportLevel",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "SupportLevel",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DevEta",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "DevOwnerUserId",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "DevReference",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "DevStage",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "DevStageSince",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "LevelSince",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "SupportLevel",
                table: "Issues");
        }
    }
}

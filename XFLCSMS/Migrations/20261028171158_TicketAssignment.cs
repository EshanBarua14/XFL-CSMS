using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFLCSMS.Migrations
{
    public partial class TicketAssignment : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanAssignOthers",
                table: "SupportLevels",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanClose",
                table: "SupportLevels",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanEscalate",
                table: "SupportLevels",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanHandBack",
                table: "SupportLevels",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanRequestTransfer",
                table: "SupportLevels",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanSelfAssign",
                table: "SupportLevels",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EscalateUpTo",
                table: "SupportLevels",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxOpenTickets",
                table: "SupportLevels",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "ReceivesAutoAssign",
                table: "SupportLevels",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AssignmentSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    SelfAssignEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ReleaseEnabled = table.Column<bool>(type: "bit", nullable: false),
                    StatusOnTake = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AutoAssignEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AutoAssignIdleBelow = table.Column<int>(type: "int", nullable: false),
                    AutoAssignIntervalMinutes = table.Column<int>(type: "int", nullable: false),
                    AutoAssignLevelRule = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AutoAssignOrder = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AutoAssignOnlyOnline = table.Column<bool>(type: "bit", nullable: false),
                    OnlineWindowMinutes = table.Column<int>(type: "int", nullable: false),
                    AutoAssignDelayMinutes = table.Column<int>(type: "int", nullable: false),
                    TransferEnabled = table.Column<bool>(type: "bit", nullable: false),
                    TransferNeedsManagerApproval = table.Column<bool>(type: "bit", nullable: false),
                    TransferExpiryHours = table.Column<int>(type: "int", nullable: false),
                    TransferRespectsCapacity = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketTransferRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IssueId = table.Column<int>(type: "int", nullable: false),
                    FromUserId = table.Column<int>(type: "int", nullable: false),
                    FromName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ToUserId = table.Column<int>(type: "int", nullable: false),
                    ToName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedByUserId = table.Column<int>(type: "int", nullable: true),
                    DecidedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketTransferRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketTransferRequests_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "IssueId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TicketTransferRequests_Users_FromUserId",
                        column: x => x.FromUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketTransferRequests_Users_ToUserId",
                        column: x => x.ToUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "AssignmentSettings",
                columns: new[] { "Id", "AutoAssignDelayMinutes", "AutoAssignEnabled", "AutoAssignIdleBelow", "AutoAssignIntervalMinutes", "AutoAssignLevelRule", "AutoAssignOnlyOnline", "AutoAssignOrder", "OnlineWindowMinutes", "ReleaseEnabled", "SelfAssignEnabled", "StatusOnTake", "TransferEnabled", "TransferExpiryHours", "TransferNeedsManagerApproval", "TransferRespectsCapacity", "UpdatedBy", "UpdatedOn" },
                values: new object[] { 1, 0, true, 1, 2, "OwnLevelFirst", true, "Oldest", 15, true, true, null, true, 24, false, true, null, null });

            migrationBuilder.UpdateData(
                table: "SupportLevels",
                keyColumn: "Level",
                keyValue: 1,
                columns: new[] { "CanClose", "CanEscalate", "CanRequestTransfer", "CanSelfAssign", "EscalateUpTo", "ReceivesAutoAssign" },
                values: new object[] { true, true, true, true, 5, true });

            migrationBuilder.UpdateData(
                table: "SupportLevels",
                keyColumn: "Level",
                keyValue: 2,
                columns: new[] { "CanClose", "CanEscalate", "CanRequestTransfer", "CanSelfAssign", "EscalateUpTo", "ReceivesAutoAssign" },
                values: new object[] { true, true, true, true, 5, true });

            migrationBuilder.UpdateData(
                table: "SupportLevels",
                keyColumn: "Level",
                keyValue: 3,
                columns: new[] { "CanClose", "CanEscalate", "CanRequestTransfer", "CanSelfAssign", "EscalateUpTo", "ReceivesAutoAssign" },
                values: new object[] { true, true, true, true, 5, true });

            migrationBuilder.UpdateData(
                table: "SupportLevels",
                keyColumn: "Level",
                keyValue: 4,
                columns: new[] { "CanClose", "CanEscalate", "CanRequestTransfer", "CanSelfAssign", "EscalateUpTo", "ReceivesAutoAssign" },
                values: new object[] { true, true, true, true, 5, true });

            migrationBuilder.UpdateData(
                table: "SupportLevels",
                keyColumn: "Level",
                keyValue: 5,
                columns: new[] { "CanClose", "CanEscalate", "CanRequestTransfer", "CanSelfAssign", "EscalateUpTo", "ReceivesAutoAssign" },
                values: new object[] { true, true, true, true, 5, true });

            migrationBuilder.CreateIndex(
                name: "IX_TicketTransferRequests_FromUserId_Status",
                table: "TicketTransferRequests",
                columns: new[] { "FromUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketTransferRequests_IssueId_Status",
                table: "TicketTransferRequests",
                columns: new[] { "IssueId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketTransferRequests_ToUserId_Status",
                table: "TicketTransferRequests",
                columns: new[] { "ToUserId", "Status" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssignmentSettings");

            migrationBuilder.DropTable(
                name: "TicketTransferRequests");

            migrationBuilder.DropColumn(
                name: "CanAssignOthers",
                table: "SupportLevels");

            migrationBuilder.DropColumn(
                name: "CanClose",
                table: "SupportLevels");

            migrationBuilder.DropColumn(
                name: "CanEscalate",
                table: "SupportLevels");

            migrationBuilder.DropColumn(
                name: "CanHandBack",
                table: "SupportLevels");

            migrationBuilder.DropColumn(
                name: "CanRequestTransfer",
                table: "SupportLevels");

            migrationBuilder.DropColumn(
                name: "CanSelfAssign",
                table: "SupportLevels");

            migrationBuilder.DropColumn(
                name: "EscalateUpTo",
                table: "SupportLevels");

            migrationBuilder.DropColumn(
                name: "MaxOpenTickets",
                table: "SupportLevels");

            migrationBuilder.DropColumn(
                name: "ReceivesAutoAssign",
                table: "SupportLevels");
        }
    }
}

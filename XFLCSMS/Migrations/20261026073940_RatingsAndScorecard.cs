using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFLCSMS.Migrations
{
    public partial class RatingsAndScorecard : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignedToUserId",
                table: "Issues",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstResponseOn",
                table: "Issues",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EngineerAwards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Month = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EngineerUserId = table.Column<int>(type: "int", nullable: true),
                    EngineerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Score = table.Column<double>(type: "float", nullable: true),
                    TopRankedName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NominatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DecidedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EngineerAwards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MonthlyEngineerScores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Month = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EngineerUserId = table.Column<int>(type: "int", nullable: false),
                    EngineerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Eligible = table.Column<bool>(type: "bit", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: true),
                    Score = table.Column<double>(type: "float", nullable: false),
                    TicketsClosed = table.Column<int>(type: "int", nullable: false),
                    WeightedVolume = table.Column<double>(type: "float", nullable: false),
                    RatingsCount = table.Column<int>(type: "int", nullable: false),
                    AverageStars = table.Column<double>(type: "float", nullable: true),
                    AdjustedRating = table.Column<double>(type: "float", nullable: true),
                    SlaCompliance = table.Column<double>(type: "float", nullable: true),
                    ResolutionRatio = table.Column<double>(type: "float", nullable: true),
                    MedianResolutionHours = table.Column<double>(type: "float", nullable: true),
                    ReopenRate = table.Column<double>(type: "float", nullable: true),
                    MedianFirstResponseMinutes = table.Column<double>(type: "float", nullable: true),
                    BacklogOverdueShare = table.Column<double>(type: "float", nullable: true),
                    LowRatingsUnreviewed = table.Column<int>(type: "int", nullable: false),
                    RatingPoints = table.Column<double>(type: "float", nullable: false),
                    SlaPoints = table.Column<double>(type: "float", nullable: false),
                    ResolutionPoints = table.Column<double>(type: "float", nullable: false),
                    VolumePoints = table.Column<double>(type: "float", nullable: false),
                    ReopenPoints = table.Column<double>(type: "float", nullable: false),
                    FirstResponsePoints = table.Column<double>(type: "float", nullable: false),
                    BacklogPoints = table.Column<double>(type: "float", nullable: false),
                    FrozenOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyEngineerScores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SlaTargets",
                columns: table => new
                {
                    Priority = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FirstResponseHours = table.Column<double>(type: "float", nullable: false),
                    ResolutionHours = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlaTargets", x => x.Priority);
                });

            migrationBuilder.CreateTable(
                name: "TicketRatings",
                columns: table => new
                {
                    RatingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IssueId = table.Column<int>(type: "int", nullable: false),
                    RatedByUserId = table.Column<int>(type: "int", nullable: false),
                    EngineerUserId = table.Column<int>(type: "int", nullable: true),
                    EngineerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Stars = table.Column<byte>(type: "tinyint", nullable: false),
                    SpeedScore = table.Column<byte>(type: "tinyint", nullable: true),
                    CommunicationScore = table.Column<byte>(type: "tinyint", nullable: true),
                    QualityScore = table.Column<byte>(type: "tinyint", nullable: true),
                    FullySolved = table.Column<bool>(type: "bit", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReviewedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketRatings", x => x.RatingId);
                    table.ForeignKey(
                        name: "FK_TicketRatings_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "IssueId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TicketRatings_Users_EngineerUserId",
                        column: x => x.EngineerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketRatings_Users_RatedByUserId",
                        column: x => x.RatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TicketStatusHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IssueId = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    AssignedToUserId = table.Column<int>(type: "int", nullable: true),
                    ChangedByUserId = table.Column<int>(type: "int", nullable: true),
                    ChangedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ChangedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketStatusHistories_Issues_IssueId",
                        column: x => x.IssueId,
                        principalTable: "Issues",
                        principalColumn: "IssueId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "SlaTargets",
                columns: new[] { "Priority", "FirstResponseHours", "ResolutionHours" },
                values: new object[] { "High", 1.0, 8.0 });

            migrationBuilder.InsertData(
                table: "SlaTargets",
                columns: new[] { "Priority", "FirstResponseHours", "ResolutionHours" },
                values: new object[] { "Low", 24.0, 120.0 });

            migrationBuilder.InsertData(
                table: "SlaTargets",
                columns: new[] { "Priority", "FirstResponseHours", "ResolutionHours" },
                values: new object[] { "Medium", 4.0, 48.0 });

            migrationBuilder.CreateIndex(
                name: "IX_Issues_AssignedToUserId",
                table: "Issues",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EngineerAwards_Month",
                table: "EngineerAwards",
                column: "Month",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyEngineerScores_Month_EngineerUserId",
                table: "MonthlyEngineerScores",
                columns: new[] { "Month", "EngineerUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketRatings_EngineerUserId_CreatedOn",
                table: "TicketRatings",
                columns: new[] { "EngineerUserId", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketRatings_IssueId",
                table: "TicketRatings",
                column: "IssueId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketRatings_RatedByUserId",
                table: "TicketRatings",
                column: "RatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketStatusHistories_IssueId_ChangedOn",
                table: "TicketStatusHistories",
                columns: new[] { "IssueId", "ChangedOn" });

            migrationBuilder.AddForeignKey(
                name: "FK_Issues_Users_AssignedToUserId",
                table: "Issues",
                column: "AssignedToUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Backfill the assignee id from the stored name (the assignee drop-down saves the full name).
            // When two users share a name, prefer a Support Engineer, then XFL staff, then the oldest account.
            migrationBuilder.Sql(@"
UPDATE i SET AssignedToUserId = (
    SELECT TOP 1 u.Id FROM Users u
    WHERE u.FullName = i.AssignBy
    ORDER BY CASE WHEN u.Department = 'Support Engineer' THEN 0 ELSE 1 END, CASE WHEN u.UType = 1 THEN 0 ELSE 1 END, u.Id)
FROM Issues i
WHERE i.AssignBy IS NOT NULL AND i.AssignBy <> ''");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Issues_Users_AssignedToUserId",
                table: "Issues");

            migrationBuilder.DropTable(
                name: "EngineerAwards");

            migrationBuilder.DropTable(
                name: "MonthlyEngineerScores");

            migrationBuilder.DropTable(
                name: "SlaTargets");

            migrationBuilder.DropTable(
                name: "TicketRatings");

            migrationBuilder.DropTable(
                name: "TicketStatusHistories");

            migrationBuilder.DropIndex(
                name: "IX_Issues_AssignedToUserId",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "AssignedToUserId",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "FirstResponseOn",
                table: "Issues");
        }
    }
}

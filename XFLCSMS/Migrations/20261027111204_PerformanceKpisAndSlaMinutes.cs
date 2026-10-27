using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFLCSMS.Migrations
{
    public partial class PerformanceKpisAndSlaMinutes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.RenameColumn(
                name: "MedianResolutionHours",
                table: "MonthlyEngineerScores",
                newName: "MedianResolutionMinutes");

            // stored scores: hours to minutes
            migrationBuilder.Sql("UPDATE MonthlyEngineerScores SET MedianResolutionMinutes = MedianResolutionMinutes * 60 WHERE MedianResolutionMinutes IS NOT NULL");

            migrationBuilder.AddColumn<string>(
                name: "ClientReply",
                table: "TicketRatings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientReplyBy",
                table: "TicketRatings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClientReplyOn",
                table: "TicketRatings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "EffortScore",
                table: "TicketRatings",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExcludeReason",
                table: "TicketRatings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExcludedBy",
                table: "TicketRatings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ExcludedFromScore",
                table: "TicketRatings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "FirstContactResolved",
                table: "TicketRatings",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "KnowledgeScore",
                table: "TicketRatings",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "ProfessionalismScore",
                table: "TicketRatings",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "RecommendScore",
                table: "TicketRatings",
                type: "tinyint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "TicketRatings",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FirstResponseMinutes",
                table: "SlaTargets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ResolutionMinutes",
                table: "SlaTargets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "AverageEffort",
                table: "MonthlyEngineerScores",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CsatPercent",
                table: "MonthlyEngineerScores",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "FcrPoints",
                table: "MonthlyEngineerScores",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "FcrRate",
                table: "MonthlyEngineerScores",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ResponseSlaCompliance",
                table: "MonthlyEngineerScores",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Nps",
                table: "MonthlyEngineerScores",
                type: "float",
                nullable: true);

            // SLA targets: keep each priority's current target, converted from hours to minutes
            migrationBuilder.Sql("UPDATE SlaTargets SET FirstResponseMinutes = CAST(ROUND(FirstResponseHours * 60, 0) AS int), ResolutionMinutes = CAST(ROUND(ResolutionHours * 60, 0) AS int)");

            migrationBuilder.DropColumn(
                name: "FirstResponseHours",
                table: "SlaTargets");

            migrationBuilder.DropColumn(
                name: "ResolutionHours",
                table: "SlaTargets");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientReply",
                table: "TicketRatings");

            migrationBuilder.DropColumn(
                name: "ClientReplyBy",
                table: "TicketRatings");

            migrationBuilder.DropColumn(
                name: "ClientReplyOn",
                table: "TicketRatings");

            migrationBuilder.DropColumn(
                name: "EffortScore",
                table: "TicketRatings");

            migrationBuilder.DropColumn(
                name: "ExcludeReason",
                table: "TicketRatings");

            migrationBuilder.DropColumn(
                name: "ExcludedBy",
                table: "TicketRatings");

            migrationBuilder.DropColumn(
                name: "ExcludedFromScore",
                table: "TicketRatings");

            migrationBuilder.DropColumn(
                name: "FirstContactResolved",
                table: "TicketRatings");

            migrationBuilder.DropColumn(
                name: "KnowledgeScore",
                table: "TicketRatings");

            migrationBuilder.DropColumn(
                name: "ProfessionalismScore",
                table: "TicketRatings");

            migrationBuilder.DropColumn(
                name: "RecommendScore",
                table: "TicketRatings");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "TicketRatings");

            migrationBuilder.DropColumn(
                name: "AverageEffort",
                table: "MonthlyEngineerScores");

            migrationBuilder.DropColumn(
                name: "CsatPercent",
                table: "MonthlyEngineerScores");

            migrationBuilder.DropColumn(
                name: "FcrPoints",
                table: "MonthlyEngineerScores");

            migrationBuilder.DropColumn(
                name: "FcrRate",
                table: "MonthlyEngineerScores");

            migrationBuilder.DropColumn(
                name: "Nps",
                table: "MonthlyEngineerScores");

            migrationBuilder.DropColumn(
                name: "ResponseSlaCompliance",
                table: "MonthlyEngineerScores");

            migrationBuilder.Sql("UPDATE MonthlyEngineerScores SET MedianResolutionMinutes = MedianResolutionMinutes / 60.0 WHERE MedianResolutionMinutes IS NOT NULL");

            migrationBuilder.RenameColumn(
                name: "MedianResolutionMinutes",
                table: "MonthlyEngineerScores",
                newName: "MedianResolutionHours");

            migrationBuilder.AddColumn<double>(
                name: "FirstResponseHours",
                table: "SlaTargets",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "ResolutionHours",
                table: "SlaTargets",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.Sql("UPDATE SlaTargets SET FirstResponseHours = FirstResponseMinutes / 60.0, ResolutionHours = ResolutionMinutes / 60.0");

            migrationBuilder.DropColumn(
                name: "FirstResponseMinutes",
                table: "SlaTargets");

            migrationBuilder.DropColumn(
                name: "ResolutionMinutes",
                table: "SlaTargets");
        }
    }
}

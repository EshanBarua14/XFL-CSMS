using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace XFLCSMS.Migrations
{
    public partial class TicketDateIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Issues_ClosedOn",
                table: "Issues",
                column: "ClosedOn");

            migrationBuilder.CreateIndex(
                name: "IX_Issues_TDate",
                table: "Issues",
                column: "TDate");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Issues_ClosedOn",
                table: "Issues");

            migrationBuilder.DropIndex(
                name: "IX_Issues_TDate",
                table: "Issues");
        }
    }
}

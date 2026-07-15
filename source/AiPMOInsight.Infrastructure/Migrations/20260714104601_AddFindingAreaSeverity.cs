using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiPMOInsight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFindingAreaSeverity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "area",
                table: "findings",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "severity",
                table: "findings",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "area",
                table: "findings");

            migrationBuilder.DropColumn(
                name: "severity",
                table: "findings");
        }
    }
}

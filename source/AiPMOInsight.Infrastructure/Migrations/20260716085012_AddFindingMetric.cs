using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiPMOInsight.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFindingMetric : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "metric_detail",
                table: "findings",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "metric_unit",
                table: "findings",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "metric_value",
                table: "findings",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "metric_detail",
                table: "findings");

            migrationBuilder.DropColumn(
                name: "metric_unit",
                table: "findings");

            migrationBuilder.DropColumn(
                name: "metric_value",
                table: "findings");
        }
    }
}
